using Carpathians.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Carpathians.Api.Middleware;

public sealed class DomainExceptionHandler(
    IProblemDetailsService problemDetails,
    IHostEnvironment env,
    ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails = problemDetails;
    private readonly IHostEnvironment _env = env;
    private readonly ILogger<DomainExceptionHandler> _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext http, Exception ex, CancellationToken ct)
    {
        var (status, title) = ex switch
        {
            EntityNotFoundException   => (StatusCodes.Status404NotFound, "Resource not found"),
            ConflictException         => (StatusCodes.Status409Conflict, "Business rule conflict"),
            DomainException           => (StatusCodes.Status409Conflict, "Domain invariant violation"),
            ValidationException       => (StatusCodes.Status400BadRequest, "Validation failed"),
            OperationCanceledException when http.RequestAborted.IsCancellationRequested
                => (499, "Client closed request"),
            _                         => (StatusCodes.Status500InternalServerError, "Unexpected error")
        };

        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(ex, "Unhandled server exception occurred: {Message}", ex.Message);
        else
            _logger.LogWarning("{ExceptionType}: {Message}", ex.GetType().Name, ex.Message);

        http.Response.StatusCode = status;

        string detail = status == StatusCodes.Status500InternalServerError && !_env.IsDevelopment()
            ? "An unexpected error occurred. Please try again later."
            : ex.Message;

        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = ex,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail
            }
        });
    }
}
