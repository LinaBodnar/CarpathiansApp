using Carpathians.Reviews.Application.Common.Behaviors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using FluentValidationException = FluentValidation.ValidationException;

namespace Carpathians.Reviews.WebApi.Middleware;

/// <summary>
/// Глобальний обробник невиловлених винятків: перетворює їх на стандартизований ProblemDetails
/// (RFC 9457) з коректним HTTP-статусом. Помилки валідації повертаються як ValidationProblemDetails
/// з переліком помилок по полях. Деталі серверних помилок показуються лише в Development.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    IHostEnvironment env,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        var (status, title) = ExceptionStatusMapper.Map(ex);

        // Винятки з обробників MediatR уже залоговано в UnhandledExceptionBehavior.
        if (!ex.IsLogged())
        {
            if (status >= StatusCodes.Status500InternalServerError)
                logger.LogError(ex, "Request {Method} {Path} failed", http.Request.Method, http.Request.Path);
            else
                logger.LogWarning("Request {Method} {Path} rejected: {ExceptionType} {Message}",
                    http.Request.Method, http.Request.Path, ex.GetType().Name, ex.Message);
        }

        http.Response.StatusCode = status;

        var problem = ex is FluentValidationException validation
            ? CreateValidationProblem(validation)
            : new ProblemDetails { Detail = BuildDetail(ex, status) };

        problem.Status = status;
        problem.Title = title;
        problem.Type = $"https://httpstatuses.io/{status}";
        problem.Instance = http.Request.Path;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = ex,
            ProblemDetails = problem
        });
    }

    private static ValidationProblemDetails CreateValidationProblem(FluentValidationException ex)
    {
        var errors = ex.Errors
            .GroupBy(e => string.IsNullOrEmpty(e.PropertyName) ? "request" : e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

        return new ValidationProblemDetails(errors)
        {
            Detail = "One or more validation errors occurred."
        };
    }

    private string BuildDetail(Exception ex, int status)
    {
        if (status < StatusCodes.Status500InternalServerError || env.IsDevelopment())
            return ex.Message;                                        // 4xx або Development — повне повідомлення

        return status == StatusCodes.Status503ServiceUnavailable      // Production: без внутрішніх деталей
            ? "The database is temporarily unavailable. Please try again later."
            : "An unexpected error occurred. Please try again later.";
    }
}
