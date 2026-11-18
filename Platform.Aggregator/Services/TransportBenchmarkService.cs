using System.Diagnostics;
using Carpathians.Platform.Aggregator.Models;

namespace Carpathians.Platform.Aggregator.Services;

/// <summary>
/// Порівняння затримок агрегації через HTTP REST (Лаб. 05, <see cref="HttpAggregationService"/>)
/// і gRPC (Лаб. 06, <see cref="AggregationService"/>) на тій самій картці бронювання.
/// <list type="bullet">
/// <item>кеш агрегованих відповідей обходиться — вимірюються саме виклики мікросервісів;</item>
/// <item>спершу прогрів (з'єднання, JIT, кеші сервісів), далі виклики чергуються HTTP/gRPC,
/// щоб обидва транспорти були в однакових умовах;</item>
/// <item>кожен виклик — окремий span, тож результати можна звірити з Traces в Aspire Dashboard.</item>
/// </list>
/// </summary>
public sealed class TransportBenchmarkService(
    HttpAggregationService http,
    AggregationService grpc,
    ILogger<TransportBenchmarkService> logger)
{
    public const int MaxIterations = 200;
    private const int WarmupIterations = 3;

    private static readonly ActivitySource Telemetry = new("Carpathians.Aggregator.Benchmark");

    /// <summary>null — бронювання не існує.</summary>
    public async Task<TransportComparisonDto?> CompareFullBookingAsync(long bookingId, int iterations, CancellationToken ct)
    {
        iterations = Math.Clamp(iterations, 1, MaxIterations);
        using var activity = Telemetry.StartActivity("CompareTransports");
        activity?.SetTag("booking.id", bookingId);
        activity?.SetTag("benchmark.iterations", iterations);

        if (await grpc.AggregateFullBookingAsync(bookingId, ct) is null)
            return null;

        for (var i = 0; i < WarmupIterations; i++)
        {
            await http.GetFullBookingAsync(bookingId, ct);
            await grpc.AggregateFullBookingAsync(bookingId, ct);
        }

        var httpTimes = new List<double>(iterations);
        var grpcTimes = new List<double>(iterations);
        for (var i = 0; i < iterations; i++)
        {
            // Чергування порядку: жоден транспорт не отримує систематичної переваги «другого» виклику.
            if (i % 2 == 0)
            {
                httpTimes.Add(await MeasureAsync(() => http.GetFullBookingAsync(bookingId, ct)));
                grpcTimes.Add(await MeasureAsync(() => grpc.AggregateFullBookingAsync(bookingId, ct)));
            }
            else
            {
                grpcTimes.Add(await MeasureAsync(() => grpc.AggregateFullBookingAsync(bookingId, ct)));
                httpTimes.Add(await MeasureAsync(() => http.GetFullBookingAsync(bookingId, ct)));
            }
        }

        var httpStats = Stats("http", httpTimes);
        var grpcStats = Stats("grpc", grpcTimes);
        var speedup = grpcStats.AvgMs > 0 ? Math.Round(httpStats.AvgMs / grpcStats.AvgMs, 2) : 0;

        activity?.SetTag("benchmark.http.avg_ms", httpStats.AvgMs);
        activity?.SetTag("benchmark.grpc.avg_ms", grpcStats.AvgMs);
        logger.LogInformation(
            "Transport comparison for booking {BookingId} ({Iterations} iterations): HTTP avg {HttpAvgMs} ms, p95 {HttpP95Ms} ms; gRPC avg {GrpcAvgMs} ms, p95 {GrpcP95Ms} ms; speed-up x{Speedup}",
            bookingId, iterations, httpStats.AvgMs, httpStats.P95Ms, grpcStats.AvgMs, grpcStats.P95Ms, speedup);

        return new TransportComparisonDto(bookingId, iterations, httpStats, grpcStats, speedup);
    }

    private static async Task<double> MeasureAsync<T>(Func<Task<T>> call)
    {
        var started = Stopwatch.GetTimestamp();
        await call();
        return Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }

    private static TransportStatsDto Stats(string transport, List<double> times)
    {
        var sorted = times.Order().ToList();
        return new TransportStatsDto(
            transport,
            Math.Round(sorted.Average(), 2),
            Math.Round(Percentile(sorted, 0.50), 2),
            Math.Round(Percentile(sorted, 0.95), 2),
            Math.Round(sorted[0], 2),
            Math.Round(sorted[^1], 2));
    }

    /// <summary>Перцентиль методом найближчого рангу.</summary>
    private static double Percentile(IReadOnlyList<double> sorted, double p)
        => sorted[Math.Clamp((int)Math.Ceiling(p * sorted.Count) - 1, 0, sorted.Count - 1)];
}
