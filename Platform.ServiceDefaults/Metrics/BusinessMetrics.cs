using System.Diagnostics;
using Grpc.Core;

namespace Carpathians.Platform.ServiceDefaults.Metrics;

/// <summary>
/// Назви Meter бізнес-метрик сервісів (Лаб. 07). Назва Meter = назва сервісу: так метрики в Dashboard
/// однозначно атрибутуються сервісу. Усі вони додаються в OpenTelemetry через AddMeter у <c>ConfigureOpenTelemetry</c>.
/// </summary>
public static class BusinessMeters
{
    public const string Booking = "Booking.Api";
    public const string Catalog = "Catalog.Api";
    public const string Reviews = "Reviews.Api";
    public const string Aggregator = "Aggregator.Api";

    public static readonly string[] All = [Booking, Catalog, Reviews, Aggregator];
}

/// <summary>
/// Однакові назви й значення тегів (вимірів) в усіх сервісах. Кожен тег має невелику фіксовану множину значень
/// (низька кардинальність): ідентифікатори, часові мітки й довільний текст у теги не потрапляють.
/// </summary>
public static class MetricTags
{
    /// <summary>Бізнес-операція: create, confirm, pay, search... (фіксований перелік у кожному сервісі).</summary>
    public const string Operation = "operation";

    /// <summary>Результат операції: success | failure.</summary>
    public const string Status = "status";

    /// <summary>Категорія помилки (лише для failure): not_found | conflict | validation | business_rule | cancelled | unavailable | error.</summary>
    public const string ErrorType = "error.type";

    public const string Success = "success";
    public const string Failure = "failure";

    /// <summary>
    /// Категорія помилки за типом винятку. Доменні винятки сервісів мають однакові назви класів
    /// (NotFoundException, ValidationException, BusinessConflictException...), тож класифікація спільна.
    /// </summary>
    public static string ClassifyError(Exception exception) => exception switch
    {
        OperationCanceledException => "cancelled",
        RpcException { StatusCode: StatusCode.NotFound } => "not_found",
        RpcException { StatusCode: StatusCode.Unavailable or StatusCode.DeadlineExceeded } => "unavailable",
        _ when exception.GetType().Name.Contains("NotFound", StringComparison.Ordinal) => "not_found",
        _ when exception.GetType().Name.Contains("Validation", StringComparison.Ordinal) => "validation",
        _ when exception.GetType().Name.Contains("Conflict", StringComparison.Ordinal)
               || exception.GetType().Name.Contains("Concurrency", StringComparison.Ordinal) => "conflict",
        _ when exception.GetType().Name == "DomainException" => "business_rule",
        _ when exception.GetType().Name.Contains("Unavailable", StringComparison.Ordinal) => "unavailable",
        _ => "error"
    };

    /// <summary>Теги вимірювання тривалості: operation, status і (для збою) error.type.</summary>
    public static TagList ForOperation(string operation, Exception? error)
    {
        var tags = new TagList
        {
            { Operation, operation },
            { Status, error is null ? Success : Failure }
        };
        if (error is not null)
            tags.Add(ErrorType, ClassifyError(error));
        return tags;
    }
}

/// <summary>
/// Межі кошиків гістограм тривалості бізнес-операцій (мс), задаються через OpenTelemetry View.
/// Стандартні межі OpenTelemetry (0, 5, 10, 25, 50, 75, 100, 250, 500, 750, 1000, 2500...) погано розрізняють
/// повільні операції, а для нас важливий хвіст 1–10 с (повтори й таймаути resilience-політик).
/// </summary>
public static class OperationDurationBuckets
{
    public static readonly double[] Boundaries = [5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000];

    /// <summary>Гістограма тривалості бізнес-операції: назва *.duration у Meter одного з сервісів.</summary>
    public static bool Applies(string meterName, string instrumentName)
        => Array.IndexOf(BusinessMeters.All, meterName) >= 0
           && instrumentName.EndsWith(".duration", StringComparison.Ordinal);
}
