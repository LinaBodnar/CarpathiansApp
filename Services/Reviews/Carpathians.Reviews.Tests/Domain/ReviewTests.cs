using Carpathians.Reviews.Domain.Entities;
using Carpathians.Reviews.Domain.Exceptions;
using Carpathians.Reviews.Domain.ValueObjects;

namespace Carpathians.Reviews.Tests.Domain;

[TestFixture]
public class ReviewTests
{
    private static readonly Author Olena = new("1", "Олена");

    private static Review CreateReview(int rating = 5)
        => new(1, "Заросляк – Говерла", Olena, rating, "Чудовий маршрут");

    [TestCase(0)]
    [TestCase(6)]
    [TestCase(10)]
    public void Constructor_RatingOutOfRange_ThrowsDomainException(int rating)
    {
        Assert.That(() => CreateReview(rating), Throws.TypeOf<DomainException>());
    }

    [Test]
    public void Constructor_ValidData_SetsFieldsAndDefaults()
    {
        var review = CreateReview(4);

        Assert.Multiple(() =>
        {
            Assert.That(review.RouteId, Is.EqualTo(1));
            Assert.That(review.Rating, Is.EqualTo(4));
            Assert.That(review.Author, Is.EqualTo(new Author("1", "Олена")));
            Assert.That(review.Version, Is.EqualTo(0));
            Assert.That(review.Comments, Is.Empty);
            Assert.That(review.Weather, Is.Null);
            Assert.That(review.UpdatedAt, Is.Null);
        });
    }

    [Test]
    public void Constructor_EmptyText_ThrowsDomainException()
    {
        Assert.That(() => new Review(1, "Route", Olena, 5, "   "), Throws.TypeOf<DomainException>());
    }

    [Test]
    public void Constructor_NonPositiveRouteId_ThrowsDomainException()
    {
        Assert.That(() => new Review(0, "Route", Olena, 5, "text"), Throws.TypeOf<DomainException>());
    }

    [Test]
    public void AddComment_AppendsEmbeddedCommentAndTouches()
    {
        var review = CreateReview();

        review.AddComment(new Author("2", "Андрій"), "Згоден");

        Assert.Multiple(() =>
        {
            Assert.That(review.Comments, Has.Count.EqualTo(1));
            Assert.That(review.Comments.Single().Author.DisplayName, Is.EqualTo("Андрій"));
            Assert.That(review.UpdatedAt, Is.Not.Null);
        });
    }

    [Test]
    public void AddComment_EmptyText_ThrowsDomainException()
    {
        var review = CreateReview();
        Assert.That(() => review.AddComment(Olena, ""), Throws.TypeOf<DomainException>());
    }

    [Test]
    public void Update_InvalidRating_DoesNotChangeState()
    {
        var review = CreateReview(5);

        Assert.That(() => review.Update(7, "інший текст"), Throws.TypeOf<DomainException>());
        Assert.That(review.Rating, Is.EqualTo(5));
    }

    [Test]
    public void SetTags_NormalizesAndRemovesDuplicates()
    {
        var review = CreateReview();

        review.SetTags(new[] { "Lake", "lake", " photography ", "" });

        Assert.That(review.Tags, Is.EquivalentTo(new[] { "lake", "photography" }));
    }

    [Test]
    public void SetGpsTrack_SinglePoint_ThrowsDomainException()
    {
        var review = CreateReview();
        Assert.That(() => review.SetGpsTrack(new[] { new GeoPoint(48.16, 24.50) }),
            Throws.TypeOf<DomainException>());
    }

    [Test]
    public void BumpVersion_IncrementsVersion()
    {
        var review = CreateReview();
        review.BumpVersion();
        Assert.That(review.Version, Is.EqualTo(1));
    }
}
