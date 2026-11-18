using System.Net.Http.Headers;
using System.Text.Json.Serialization;

namespace Carpathians.Platform.Aggregator.Security;

/// <summary>Налаштування Client Credentials для викликів мікросервісів від імені Aggregator (без користувача).</summary>
public sealed class ClientCredentialsOptions
{
    /// <summary>Адреса IdentityServer (IdentityServer:Authority від AppHost).</summary>
    public string? Authority { get; set; }
    public string ClientId { get; set; } = "aggregator_service";
    public string? ClientSecret { get; set; }
    public string Scope { get; set; } = "bookings.read routes.read reviews.read";

    /// <summary>За скільки секунд до закінчення терміну дії токен оновлюється заздалегідь.</summary>
    public int RefreshBeforeExpirySeconds { get; set; } = 60;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Authority) && !string.IsNullOrWhiteSpace(ClientSecret);
}

/// <summary>
/// Лаб. 09, OAuth 2.0 Client Credentials: Aggregator отримує access token IdentityServer (клієнт aggregator_service,
/// scopes bookings.read routes.read reviews.read) і додає його до кожного gRPC-виклику мікросервісів.
/// Токен кешується й оновлюється за <see cref="ClientCredentialsOptions.RefreshBeforeExpirySeconds"/> с до закінчення;
/// одночасні запити чекають на один виклик /connect/token (SemaphoreSlim).
/// У токені немає claim <c>sub</c> — це сервіс, а не користувач.
/// </summary>
public sealed class ClientCredentialsTokenProvider(
    IHttpClientFactory httpClientFactory,
    ClientCredentialsOptions options,
    TimeProvider timeProvider,
    ILogger<ClientCredentialsTokenProvider> logger)
{
    public const string HttpClientName = "identityserver-token";

    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _refreshAt;

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!options.IsConfigured)
            return null;                                   // Aggregator запущено без IdentityServer

        if (_accessToken is not null && timeProvider.GetUtcNow() < _refreshAt)
            return _accessToken;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is not null && timeProvider.GetUtcNow() < _refreshAt)
                return _accessToken;

            var client = httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.Authority!.TrimEnd('/')}/connect/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["scope"] = options.Scope
                })
            };
            // client_secret_basic: облікові дані клієнта в заголовку Authorization (RFC 6749, 2.3.1)
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(
                    $"{Uri.EscapeDataString(options.ClientId)}:{Uri.EscapeDataString(options.ClientSecret!)}")));

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Client Credentials: IdentityServer повернув {StatusCode}: {Body}", (int)response.StatusCode, body);
                throw new InvalidOperationException($"Не вдалося отримати access token Aggregator ({(int)response.StatusCode}).");
            }

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                        ?? throw new InvalidOperationException("Порожня відповідь /connect/token.");

            _accessToken = token.AccessToken;
            var lifetime = TimeSpan.FromSeconds(Math.Max(token.ExpiresIn - options.RefreshBeforeExpirySeconds, 0));
            _refreshAt = timeProvider.GetUtcNow() + lifetime;
            logger.LogInformation("Client Credentials: отримано access token {ClientId} (scope: {Scope}), дійсний {ExpiresIn} с",
                options.ClientId, token.Scope, token.ExpiresIn);
            return _accessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("scope")] string? Scope);
}
