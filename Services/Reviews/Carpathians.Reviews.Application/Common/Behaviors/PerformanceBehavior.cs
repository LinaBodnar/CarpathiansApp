using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Carpathians.Reviews.Application.Common.Behaviors;

/// <summary>
/// Найближчий до обробника behavior: вимірює час виконання обробника (разом зі зверненнями
/// до MongoDB) і пише попередження, якщо він перевищує поріг.
/// </summary>
public sealed class PerformanceBehavior<TRequest, TResponse>(
    ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public const int SlowRequestThresholdMs = 500;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        if (sw.ElapsedMilliseconds > SlowRequestThresholdMs)
            logger.LogWarning("Slow request {RequestName}: {ElapsedMs} ms (threshold {ThresholdMs} ms) {@Request}",
                typeof(TRequest).Name, sw.ElapsedMilliseconds, SlowRequestThresholdMs, request);

        return response;
    }
}
