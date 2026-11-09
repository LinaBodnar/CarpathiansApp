namespace Carpathians.Platform.ServiceDefaults.Caching;

/// <summary>
/// Контекст поточного виклику для кешу (AsyncLocal — свій для кожного запиту):
/// через який протокол прийшов запит і на якому рівні його обслужив кеш.
/// gRPC-interceptor позначає виклик як "grpc" і після відповіді читає <see cref="LastResult"/>,
/// щоб передати ознаку попадання клієнту в trailers і залогувати її окремо від HTTP.
/// </summary>
public static class CacheCallContext
{
    public const string Http = "http";
    public const string Grpc = "grpc";
    public const string Background = "background";

    private static readonly AsyncLocal<Scope?> Current = new();

    /// <summary>Протокол поточного запиту (за замовчуванням http).</summary>
    public static string Transport => Current.Value?.Transport ?? Http;

    /// <summary>Результат останнього звернення до кешу в межах виклику: l1, l2 або source.</summary>
    public static string? LastResult => Current.Value?.LastResult;

    /// <summary>Почати контекст виклику (gRPC-interceptor, фонове прогрівання).</summary>
    public static IDisposable Begin(string transport)
    {
        var previous = Current.Value;
        Current.Value = new Scope(transport);
        return new Restore(previous);
    }

    internal static void Record(string level)
    {
        if (Current.Value is { } scope)
            scope.LastResult = level;
    }

    private sealed class Scope(string transport)
    {
        public string Transport { get; } = transport;
        public string? LastResult { get; set; }
    }

    private sealed class Restore(Scope? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
