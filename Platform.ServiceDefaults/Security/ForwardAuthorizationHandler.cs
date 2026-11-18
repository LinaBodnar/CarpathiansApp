using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Carpathians.Platform.ServiceDefaults.Security;

/// <summary>
/// DelegatingHandler: передає access token користувача з вхідного запиту у вихідний (Aggregator -> мікросервіси
/// по HTTP). Мікросервіс сам перевіряє той самий токен — ідентичність і ролі користувача зберігаються.
/// </summary>
public sealed class ForwardAuthorizationHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Headers.Authorization is null
            && accessor.HttpContext?.Request.Headers[HeaderNames.Authorization].ToString() is { Length: > 0 } authorization)
        {
            request.Headers.TryAddWithoutValidation(HeaderNames.Authorization, authorization);
        }

        return base.SendAsync(request, ct);
    }
}
