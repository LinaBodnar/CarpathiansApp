using Carpathians.Reviews.WebApi.Http;
using Microsoft.AspNetCore.Http;

namespace Carpathians.Reviews.Tests.WebApi;

[TestFixture]
public class ETagHeadersTests
{
    private static HttpRequest RequestWithIfMatch(string? value)
    {
        var context = new DefaultHttpContext();
        if (value is not null)
            context.Request.Headers.IfMatch = value;
        return context.Request;
    }

    [TestCase("\"3\"", 3)]
    [TestCase("W/\"5\"", 5)]
    [TestCase("0", 0)]
    public void GetIfMatchVersion_ParsesVersion(string header, int expected)
        => Assert.That(RequestWithIfMatch(header).GetIfMatchVersion(), Is.EqualTo(expected));

    [TestCase(null)]
    [TestCase("*")]
    public void GetIfMatchVersion_NoHeaderOrAny_ReturnsNull(string? header)
        => Assert.That(RequestWithIfMatch(header).GetIfMatchVersion(), Is.Null);

    [Test]
    public void GetIfMatchVersion_InvalidValue_ThrowsFormatException()
        => Assert.That(() => RequestWithIfMatch("\"abc\"").GetIfMatchVersion(), Throws.TypeOf<FormatException>());

    [Test]
    public void Format_WrapsVersionInQuotes() => Assert.That(ETagHeaders.Format(7), Is.EqualTo("\"7\""));
}
