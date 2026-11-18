using System.Diagnostics;
using Carpathians.Platform.ServiceDefaults.Correlation;
using Microsoft.AspNetCore.Http;

namespace Carpathians.Platform.EventBus.Messaging;

/// <summary>
/// Поточний CorrelationId для подій, що створюються в цьому потоці виконання:
/// у HTTP-запиті — заголовок X-Correlation-Id (його виставляє Gateway або CorrelationIdMiddleware),
/// під час обробки повідомлення — CorrelationId отриманої події (ланцюжок подій зберігає той самий id).
/// </summary>
public sealed class CorrelationContext(IHttpContextAccessor httpContextAccessor)
{
    private static readonly AsyncLocal<string?> MessageCorrelationId = new();

    public string? Current
        => MessageCorrelationId.Value
           ?? (httpContextAccessor.HttpContext?.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString()
               is { Length: > 0 } fromRequest ? fromRequest : null)
           ?? Activity.Current?.GetTagItem(CorrelationIdMiddleware.ActivityTag) as string;

    /// <summary>Встановлює CorrelationId на час обробки повідомлення (consumer).</summary>
    public static IDisposable BeginMessageScope(string? correlationId)
    {
        var previous = MessageCorrelationId.Value;
        MessageCorrelationId.Value = correlationId;
        return new Restore(previous);
    }

    private sealed class Restore(string? previous) : IDisposable
    {
        public void Dispose() => MessageCorrelationId.Value = previous;
    }
}
