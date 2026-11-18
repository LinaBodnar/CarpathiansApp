namespace Carpathians.Platform.Aggregator.Services;

/// <summary>Обов'язковий для агрегації сервіс недоступний — відповідь неможливо сформувати (503).</summary>
public class UpstreamUnavailableException(string service, Exception inner)
    : Exception($"{service} is unavailable", inner)
{
    public string Service { get; } = service;
}
