using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Carpathians.Reviews.Application.Common.Behaviors;

/// <summary>
/// Зовнішній behavior: структуроване логування кожної команди й запиту — початок, успішне
/// завершення і тривалість. Якщо валідація відхилила запит, запису «Handled» у лозі не буде.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Handling {RequestName} {@Request}", requestName, request);

        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        logger.LogInformation("Handled {RequestName} in {ElapsedMs} ms", requestName, sw.ElapsedMilliseconds);
        return response;
    }
}
