using System.Text.Json;
using Carpathians.Reviews.Application.Common.Behaviors;
using Carpathians.Reviews.Domain.Exceptions;
using Carpathians.Reviews.WebApi.Middleware;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using FluentValidationException = FluentValidation.ValidationException;

namespace Carpathians.Reviews.Tests.WebApi;

[TestFixture]
public class ExceptionHandlingTests
{
    private static IEnumerable<TestCaseData> Mappings()
    {
        yield return new TestCaseData(new FluentValidationException("invalid"), 400).SetName("Validation -> 400");
        yield return new TestCaseData(new DomainException("rule"), 400).SetName("Domain -> 400");
        yield return new TestCaseData(new FormatException("If-Match"), 400).SetName("Format -> 400");
        yield return new TestCaseData(new NotFoundException("missing"), 404).SetName("NotFound -> 404");
        yield return new TestCaseData(new ConflictException("version"), 409).SetName("Conflict -> 409");
        yield return new TestCaseData(new TimeoutException("server selection"), 503).SetName("Mongo timeout -> 503");
        yield return new TestCaseData(new InvalidOperationException("boom"), 500).SetName("Other -> 500");
    }

    [TestCaseSource(nameof(Mappings))]
    public void Map_ReturnsExpectedStatus(Exception ex, int expected)
        => Assert.That(ExceptionStatusMapper.Map(ex).Status, Is.EqualTo(expected));

    [Test]
    public async Task Handler_ValidationException_WritesValidationProblemDetailsWithFieldErrors()
    {
        var (handler, context) = CreateHandler(isDevelopment: false);
        var ex = new FluentValidationException(new[]
        {
            new ValidationFailure("Rating", "'Rating' must be between 1 and 5. You entered 10."),
            new ValidationFailure("Text", "'Text' must not be empty.")
        });

        var handled = await handler.TryHandleAsync(context, ex, CancellationToken.None);
        var body = await ReadBodyAsync(context);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(context.Response.StatusCode, Is.EqualTo(400));
            Assert.That(body.GetProperty("errors").TryGetProperty("Rating", out _), Is.True);
            Assert.That(body.GetProperty("errors").TryGetProperty("Text", out _), Is.True);
        });
    }

    [Test]
    public async Task Handler_UnexpectedExceptionInProduction_HidesInternalDetails()
    {
        var (handler, context) = CreateHandler(isDevelopment: false);

        await handler.TryHandleAsync(context, new InvalidOperationException("secret connection string"),
            CancellationToken.None);
        var body = await ReadBodyAsync(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(500));
            Assert.That(body.GetProperty("detail").GetString(), Does.Not.Contain("secret"));
        });
    }

    [Test]
    public async Task Handler_NotFound_Writes404ProblemDetails()
    {
        var (handler, context) = CreateHandler(isDevelopment: true);
        var ex = new NotFoundException("Review '66f0c2000000000000000099' was not found");
        ex.MarkLogged();

        await handler.TryHandleAsync(context, ex, CancellationToken.None);
        var body = await ReadBodyAsync(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(404));
            Assert.That(body.GetProperty("status").GetInt32(), Is.EqualTo(404));
            Assert.That(body.GetProperty("detail").GetString(), Does.Contain("was not found"));
        });
    }

    private static (GlobalExceptionHandler Handler, HttpContext Context) CreateHandler(bool isDevelopment)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        var provider = services.BuildServiceProvider();

        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(isDevelopment ? Environments.Development : Environments.Production);

        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Path = "/api/reviews";
        context.Request.Headers.Accept = "application/json";
        context.Response.Body = new MemoryStream();

        var handler = new GlobalExceptionHandler(
            provider.GetRequiredService<Microsoft.AspNetCore.Http.IProblemDetailsService>(),
            env,
            NullLogger<GlobalExceptionHandler>.Instance);
        return (handler, context);
    }

    private static async Task<JsonElement> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return document.RootElement.Clone();
    }
}
