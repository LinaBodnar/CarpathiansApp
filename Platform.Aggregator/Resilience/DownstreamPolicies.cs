namespace Carpathians.Platform.Aggregator.Resilience;

/// <summary>
/// Параметри resilience для одного мікросервісу, від якого залежить Aggregator.
/// Одні й ті самі значення застосовуються до HTTP-клієнта (Лаб. 05) і gRPC-клієнта (Лаб. 06) сервісу.
/// </summary>
public sealed record DownstreamPolicy
{
    /// <summary>Логічне ім'я сервісу (ресурс AppHost).</summary>
    public required string Service { get; init; }

    /// <summary>Критичний: без нього картку не зібрати — більше повторів і чутливіший circuit breaker.</summary>
    public required bool IsCritical { get; init; }

    // ---------- Retry (Polly, рівень HTTP) ----------
    public required int MaxRetryAttempts { get; init; }
    public required TimeSpan RetryDelay { get; init; }

    // ---------- Таймаути ----------
    /// <summary>Таймаут однієї спроби.</summary>
    public required TimeSpan AttemptTimeout { get; init; }

    /// <summary>Загальний таймаут разом з усіма повторами; він же — deadline gRPC-виклику.</summary>
    public required TimeSpan TotalTimeout { get; init; }

    // ---------- Circuit breaker ----------
    public required double FailureRatio { get; init; }
    public required int MinimumThroughput { get; init; }
    public required TimeSpan SamplingDuration { get; init; }
    public required TimeSpan BreakDuration { get; init; }

    // ---------- Retry gRPC (ServiceConfig, за gRPC-статусами) ----------
    public required int GrpcMaxAttempts { get; init; }
    public required TimeSpan GrpcInitialBackoff { get; init; }
    public required TimeSpan GrpcMaxBackoff { get; init; }

    /// <summary>Deadline gRPC-виклику — не довший за загальний таймаут HTTP-рівня.</summary>
    public TimeSpan CallDeadline => TotalTimeout;

    /// <summary>
    /// Найгірший час HTTP-рівня: усі спроби по AttemptTimeout + експоненційні затримки між ними
    /// (без jitter). Загальний таймаут має бути не меншим, інакше повтори не встигнуть виконатися.
    /// </summary>
    public TimeSpan WorstCaseRetryTime
    {
        get
        {
            var delays = TimeSpan.Zero;
            for (var attempt = 0; attempt < MaxRetryAttempts; attempt++)
                delays += RetryDelay * Math.Pow(2, attempt);
            return AttemptTimeout * (MaxRetryAttempts + 1) + delays;
        }
    }

    /// <summary>Узгодженість параметрів — перевіряється під час старту Aggregator.</summary>
    public void Validate()
    {
        if (TotalTimeout < WorstCaseRetryTime)
            throw new InvalidOperationException(
                $"{Service}: total timeout {TotalTimeout} is shorter than attempts with retries ({WorstCaseRetryTime})");
        if (SamplingDuration < AttemptTimeout * 2)
            throw new InvalidOperationException(
                $"{Service}: circuit breaker sampling duration must be at least twice the attempt timeout");
    }
}

/// <summary>
/// Політики resilience Aggregator відповідно до критичності сервісів (обґрунтування — README.md у корені, розділ «Проєкт №7»).
/// <list type="bullet">
/// <item><b>catalog-api</b> — критичний: без маршрутів картка бронювання неповна. Найбільше повторів,
/// чутливий circuit breaker (30 % збоїв), довший загальний таймаут.</item>
/// <item><b>booking-api</b> — критичний і обов'язковий: без бронювання відповіді немає (503). Повтори й чутливий breaker,
/// трохи коротший таймаут — це перший виклик, від якого залежить решта.</item>
/// <item><b>reviews-api</b> — некритичний: Aggregator і без відгуків поверне часткову відповідь.
/// Один повтор, короткі таймаути, толерантний breaker (70 %) з довшою паузою — швидко здатися, не затримуючи картку.</item>
/// </list>
/// MinimumThroughput = 10 — навчальний стенд з малим трафіком (за замовчуванням 100, і breaker не спрацював би).
/// </summary>
public static class DownstreamPolicies
{
    public static readonly DownstreamPolicy Catalog = new()
    {
        Service = "catalog-api",
        IsCritical = true,
        MaxRetryAttempts = 3,
        RetryDelay = TimeSpan.FromMilliseconds(200),
        AttemptTimeout = TimeSpan.FromSeconds(2),
        TotalTimeout = TimeSpan.FromSeconds(10),          // 4 спроби × 2 с + 0.2 + 0.4 + 0.8 с = 9.4 с
        FailureRatio = 0.3,
        MinimumThroughput = 10,
        SamplingDuration = TimeSpan.FromSeconds(30),
        BreakDuration = TimeSpan.FromSeconds(15),
        GrpcMaxAttempts = 4,
        GrpcInitialBackoff = TimeSpan.FromMilliseconds(200),
        GrpcMaxBackoff = TimeSpan.FromSeconds(2)
    };

    public static readonly DownstreamPolicy Booking = new()
    {
        Service = "booking-api",
        IsCritical = true,
        MaxRetryAttempts = 2,
        RetryDelay = TimeSpan.FromMilliseconds(200),
        AttemptTimeout = TimeSpan.FromSeconds(2),
        TotalTimeout = TimeSpan.FromSeconds(8),           // 3 спроби × 2 с + 0.2 + 0.4 с = 6.6 с
        FailureRatio = 0.3,
        MinimumThroughput = 10,
        SamplingDuration = TimeSpan.FromSeconds(30),
        BreakDuration = TimeSpan.FromSeconds(15),
        GrpcMaxAttempts = 3,
        GrpcInitialBackoff = TimeSpan.FromMilliseconds(200),
        GrpcMaxBackoff = TimeSpan.FromSeconds(2)
    };

    public static readonly DownstreamPolicy Reviews = new()
    {
        Service = "reviews-api",
        IsCritical = false,
        MaxRetryAttempts = 1,
        RetryDelay = TimeSpan.FromMilliseconds(100),
        AttemptTimeout = TimeSpan.FromSeconds(1),
        TotalTimeout = TimeSpan.FromSeconds(3),           // 2 спроби × 1 с + 0.1 с = 2.1 с
        FailureRatio = 0.7,
        MinimumThroughput = 10,
        SamplingDuration = TimeSpan.FromSeconds(20),
        BreakDuration = TimeSpan.FromSeconds(30),
        GrpcMaxAttempts = 2,
        GrpcInitialBackoff = TimeSpan.FromMilliseconds(100),
        GrpcMaxBackoff = TimeSpan.FromMilliseconds(500)
    };

    public static IReadOnlyList<DownstreamPolicy> All { get; } = [Booking, Catalog, Reviews];
}
