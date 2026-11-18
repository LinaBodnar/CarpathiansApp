using Carpathians.Reviews.Application.Common.Interfaces;
using Carpathians.Reviews.Application.Reviews.Commands.CreateReview;
using Carpathians.Reviews.Application.Reviews.Commands.DeleteReview;
using Carpathians.Reviews.Application.Reviews.Commands.UpdateReview;
using Carpathians.Reviews.Domain.Entities;
using Carpathians.Reviews.Domain.Exceptions;
using Carpathians.Reviews.Domain.Interfaces;
using Carpathians.Reviews.Domain.ValueObjects;
using MongoDB.Bson;
using NSubstitute;

namespace Carpathians.Reviews.Tests.Application;

[TestFixture]
public class ReviewCommandHandlerTests
{
    private IReviewRepository _reviews = null!;
    private IRatingService _ratings = null!;

    [SetUp]
    public void SetUp()
    {
        _reviews = Substitute.For<IReviewRepository>();
        _ratings = Substitute.For<IRatingService>();
    }

    private static Review ExistingReview()
        => new(1, "Заросляк – Говерла", new Author("1", "Олена"), 5, "Чудовий маршрут");

    [Test]
    public async Task CreateReview_SavesDocumentAndRecalculatesRating()
    {
        var handler = new CreateReviewCommandHandler(_reviews, _ratings);
        var command = new CreateReviewCommand(1, "Заросляк – Говерла", "7", "Ірина", 4, "Гарний маршрут",
            Tags: new[] { "family" }, Weather: new WeatherInput(12, "light", "sunny"));

        var id = await handler.Handle(command, CancellationToken.None);

        Assert.That(ObjectId.TryParse(id, out _), Is.True);
        await _reviews.Received(1).AddAsync(
            Arg.Is<Review>(r => r.Rating == 4 && r.Tags.Contains("family") && r.Weather != null),
            Arg.Any<CancellationToken>());
        await _ratings.Received(1).RecalculateAsync(1, Arg.Any<CancellationToken>());
    }

    [Test]
    public void CreateReview_InvalidRating_ThrowsDomainExceptionWithoutSaving()
    {
        var handler = new CreateReviewCommandHandler(_reviews, _ratings);
        var command = new CreateReviewCommand(1, "Route", "7", "Ірина", 10, "text");

        Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
        _reviews.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Test]
    public void UpdateReview_NotExisting_ThrowsNotFoundException()
    {
        _reviews.GetByIdAsync(Arg.Any<ObjectId>(), Arg.Any<CancellationToken>()).Returns((Review?)null);
        var handler = new UpdateReviewCommandHandler(_reviews, _ratings);

        Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new UpdateReviewCommand(ObjectId.GenerateNewId().ToString(), 4, "text"), CancellationToken.None));
    }

    [Test]
    public void UpdateReview_StaleExpectedVersion_ThrowsConflictException()
    {
        var review = ExistingReview();
        review.BumpVersion();                                   // версія в БД = 1
        _reviews.GetByIdAsync(review.Id, Arg.Any<CancellationToken>()).Returns(review);
        var handler = new UpdateReviewCommandHandler(_reviews, _ratings);

        Assert.ThrowsAsync<ConflictException>(() => handler.Handle(
            new UpdateReviewCommand(review.Id.ToString(), 4, "новий текст", ExpectedVersion: 0),
            CancellationToken.None));
        _reviews.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Test]
    public async Task UpdateReview_RatingChanged_UpdatesAndRecalculatesRating()
    {
        var review = ExistingReview();
        _reviews.GetByIdAsync(review.Id, Arg.Any<CancellationToken>()).Returns(review);
        var handler = new UpdateReviewCommandHandler(_reviews, _ratings);

        await handler.Handle(new UpdateReviewCommand(review.Id.ToString(), 3, "Оновлений текст", ExpectedVersion: 0),
            CancellationToken.None);

        Assert.That(review.Rating, Is.EqualTo(3));
        await _reviews.Received(1).UpdateAsync(review, Arg.Any<CancellationToken>());
        await _ratings.Received(1).RecalculateAsync(review.RouteId, Arg.Any<CancellationToken>());
    }

    [Test]
    public void DeleteReview_NotExisting_ThrowsNotFoundException()
    {
        _reviews.GetByIdAsync(Arg.Any<ObjectId>(), Arg.Any<CancellationToken>()).Returns((Review?)null);
        var handler = new DeleteReviewCommandHandler(_reviews, _ratings);

        Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new DeleteReviewCommand(ObjectId.GenerateNewId().ToString()), CancellationToken.None));
    }
}
