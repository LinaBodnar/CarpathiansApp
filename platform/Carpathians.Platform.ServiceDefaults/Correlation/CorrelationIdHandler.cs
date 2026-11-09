using Microsoft.AspNetCore.Http;

namespace Carpathians.Platform.ServiceDefaults.Correlation;

/// <summary>
/// DelegatingHandler для всіх HttpClient: додає CorrelationId поточного вхідного запиту
/// у вихідні запити до інших сервісів (Aggregator -> мікросервіси).
/// </summary>
public sealed class CorrelationIdHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var correlationId = accessor.HttpContext?.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();

        if (!string.IsNullOrWhiteSpace(correlationId)
            && !request.Headers.Contains(CorrelationIdMiddleware.HeaderName))
        {
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);
        }

        return base.SendAsync(request, ct);
    }
}
