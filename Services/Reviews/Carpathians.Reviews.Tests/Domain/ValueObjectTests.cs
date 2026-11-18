using Carpathians.Reviews.Domain.Exceptions;
using Carpathians.Reviews.Domain.ValueObjects;

namespace Carpathians.Reviews.Tests.Domain;

[TestFixture]
public class ValueObjectTests
{
    [Test]
    public void Author_WithSameValues_AreEqual()
    {
        var a = new Author("17", "Олена");
        var b = new Author("17", "Олена");

        Assert.Multiple(() =>
        {
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a == b, Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(ReferenceEquals(a, b), Is.False);
        });
    }

    [Test]
    public void Author_WithDifferentValues_AreNotEqual()
    {
        Assert.That(new Author("17", "Олена") != new Author("18", "Олена"), Is.True);
    }

    [TestCase("", "Олена")]
    [TestCase("17", " ")]
    public void Author_MissingValues_ThrowsDomainException(string userId, string displayName)
    {
        Assert.That(() => new Author(userId, displayName), Throws.TypeOf<DomainException>());
    }

    [TestCase(91, 24)]
    [TestCase(48, -181)]
    public void GeoPoint_OutOfRange_ThrowsDomainException(double lat, double lng)
    {
        Assert.That(() => new GeoPoint(lat, lng), Throws.TypeOf<DomainException>());
    }

    [Test]
    public void WeatherInfo_WithoutAnyValue_ThrowsDomainException()
    {
        Assert.That(() => new WeatherInfo(null, " ", null), Throws.TypeOf<DomainException>());
    }

    [Test]
    public void GeoPoint_WithSameCoordinates_AreEqual()
    {
        Assert.That(new GeoPoint(48.16, 24.50, 2061), Is.EqualTo(new GeoPoint(48.16, 24.50, 2061)));
    }
}
