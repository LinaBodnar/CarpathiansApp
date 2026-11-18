using Carpathians.Reviews.Application.Common.Behaviors;
using Carpathians.Reviews.Application.Reviews.Commands.CreateReview;
using Carpathians.Reviews.Domain.Interfaces;
using FluentValidation;
using MediatR;
using NSubstitute;

namespace Carpathians.Reviews.Tests.Application;

[TestFixture]
public class ValidationBehaviorTests
{
    private IReviewRepository _reviews = null!;

    [SetUp]
    public void SetUp()
    {
        _reviews = Substitute.For<IReviewRepository>();
        _reviews.ExistsAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
    }

    private ValidationBehavior<CreateReviewCommand, string> CreateBehavior()
        => new(new IValidator<CreateReviewCommand>[] { new CreateReviewCommandValidator(_reviews) });

    [Test]
    public void InvalidCommand_ThrowsValidationException_AndHandlerIsNotCalled()
    {
        var handlerCalled = false;
        RequestHandlerDelegate<string> next = _ =>
        {
            handlerCalled = true;
            return Task.FromResult("id");
        };
        var command = new CreateReviewCommand(1, "Заросляк – Говерла", "1", "Олена", 10, "текст");

        var ex = Assert.ThrowsAsync<ValidationException>(
            () => CreateBehavior().Handle(command, next, CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(handlerCalled, Is.False);
            Assert.That(ex!.Errors.Select(e => e.PropertyName), Does.Contain("Rating"));
        });
    }

    [Test]
    public void SeveralInvalidFields_AllErrorsAreCollectedTogether()
    {
        RequestHandlerDelegate<string> next = _ => Task.FromResult("id");
        var command = new CreateReviewCommand(0, "", "", "", 0, "");

        var ex = Assert.ThrowsAsync<ValidationException>(
            () => CreateBehavior().Handle(command, next, CancellationToken.None));

        Assert.That(ex!.Errors.Select(e => e.PropertyName).Distinct().Count(), Is.GreaterThanOrEqualTo(5));
    }

    [Test]
    public async Task ValidCommand_CallsHandler()
    {
        RequestHandlerDelegate<string> next = _ => Task.FromResult("created-id");
        var command = new CreateReviewCommand(1, "Заросляк – Говерла", "9", "Ірина", 5, "Чудово");

        var result = await CreateBehavior().Handle(command, next, CancellationToken.None);

        Assert.That(result, Is.EqualTo("created-id"));
    }

    [Test]
    public void DuplicateReview_FailsAsyncValidation()
    {
        _reviews.ExistsAsync(1, "1", Arg.Any<CancellationToken>()).Returns(true);
        RequestHandlerDelegate<string> next = _ => Task.FromResult("id");
        var command = new CreateReviewCommand(1, "Заросляк – Говерла", "1", "Олена", 5, "Ще один відгук");

        var ex = Assert.ThrowsAsync<ValidationException>(
            () => CreateBehavior().Handle(command, next, CancellationToken.None));

        Assert.That(ex!.Errors.Single().PropertyName, Is.EqualTo("AuthorId"));
    }
}
