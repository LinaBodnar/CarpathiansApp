namespace Carpathians.Platform.ServiceDefaults.Caching;

/// <summary>
/// Контекст поточного виклику для кешу (AsyncLocal — свій для кожного запиту):
/// через який протокол прийшов запит і на яких рівнях кеш його обслужив.
/// gRPC-interceptor позначає виклик як "grpc" і після відповіді читає <see cref="Summary"/>,
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

    /// <summary>
    /// Де були знайдені дані в межах виклику: "l1", "l2", "source" або кілька з кількістю
    /// ("l1=2,source=1" — для пакетних методів). null — кеш не використовувався.
    /// </summary>
    public static string? Summary => Current.Value?.Summary();

    /// <summary>Почати контекст виклику (gRPC-interceptor, фонове прогрівання).</summary>
    public static IDisposable Begin(string transport)
    {
        var previous = Current.Value;
        Current.Value = new Scope(transport);
        return new Restore(previous);
    }

    internal static void Record(string level) => Current.Value?.Record(level);

    private sealed class Scope(string transport)
    {
        private readonly Dictionary<string, int> _results = new(StringComparer.Ordinal);

        public string Transport { get; } = transport;

        public void Record(string level)
        {
            lock (_results)
                _results[level] = _results.GetValueOrDefault(level) + 1;
        }

        public string? Summary()
        {
            lock (_results)
            {
                return _results.Count switch
                {
                    0 => null,
                    1 when _results.Values.Single() == 1 => _results.Keys.Single(),
                    _ => string.Join(',', _results.OrderBy(r => r.Key).Select(r => $"{r.Key}={r.Value}"))
                };
            }
        }
    }

    private sealed class Restore(Scope? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
