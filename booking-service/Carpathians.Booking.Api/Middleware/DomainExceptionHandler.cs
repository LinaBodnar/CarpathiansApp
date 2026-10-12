using Carpathians.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Carpathians.Api.Middleware;

public sealed class DomainExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext http, Exception ex, CancellationToken ct)
    {
        var (status, title) = ex switch
        {
            NotFoundException         => (StatusCodes.Status404NotFound, "Resource not found"),
            BusinessConflictException => (StatusCodes.Status409Conflict, "Business rule conflict"),
            ValidationException       => (StatusCodes.Status400BadRequest, "Validation failed"),
            OperationCanceledException when http.RequestAborted.IsCancellationRequested
                => (499, "Client closed request"),
            _                         => (StatusCodes.Status500InternalServerError, "Unexpected error")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(ex, "Unhandled exception");
        else
            logger.LogWarning("{ExceptionType}: {Message}", ex.GetType().Name, ex.Message);

        http.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = ex,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status == 500 ? "An unexpected error occurred." : ex.Message
            }
        });
    }
}