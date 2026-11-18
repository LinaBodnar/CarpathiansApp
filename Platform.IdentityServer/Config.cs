using Carpathians.Platform.ServiceDefaults.Security;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;

namespace Carpathians.Platform.IdentityServer;

/// <summary>
/// Конфігурація IdentityServer: identity resources (OpenID Connect), API scopes (дозволи), API resources
/// (мікросервіси — audience токена) і три clients. Записується в ConfigurationDb під час старту (<see cref="Seeding.IdentityServerSeeder"/>).
/// </summary>
public static class Config
{
    public const string PostmanClientId = "postman";
    public const string SwaggerClientId = "swagger";
    public const string AggregatorClientId = "aggregator_service";

    /// <summary>Claim з ролями користувача (identity resource "roles" і claims API resources).</summary>
    public const string RolesResource = "roles";

    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
        new IdentityResources.Email(),
        new IdentityResource(RolesResource, "Ролі користувача", [JwtClaimTypes.Role])
    ];

    /// <summary>
    /// Scopes — гранульовані дозволи («read» окремо від «write»), а не «все або нічого».
    /// Мікросервіси зіставляють їх з permissions (ScopePermissionMap) і перевіряють scope-політиками.
    /// </summary>
    public static IEnumerable<ApiScope> ApiScopes =>
    [
        new ApiScope(ScopePermissionMap.BookingsRead, "Бронювання: читання"),
        new ApiScope(ScopePermissionMap.BookingsWrite, "Бронювання: створення, зміна, підтвердження, видалення"),
        new ApiScope(ScopePermissionMap.RoutesRead, "Маршрути: читання"),
        new ApiScope(ScopePermissionMap.RoutesWrite, "Маршрути: створення, зміна, публікація, видалення"),
        new ApiScope(ScopePermissionMap.PaymentsRead, "Платежі: читання"),
        new ApiScope(ScopePermissionMap.PaymentsWrite, "Платежі: проведення й повернення"),
        new ApiScope(ScopePermissionMap.ReviewsRead, "Відгуки: читання")
    ];

    /// <summary>
    /// API resource на кожен мікросервіс: його назва — audience токена, який сервіс перевіряє.
    /// UserClaims — claims користувача, що потрапляють в access token (ролі й email для бізнес-логіки).
    /// </summary>
    public static IEnumerable<ApiResource> ApiResources =>
    [
        Resource(Permissions.Bookings.ResourceServer, "Бронювання", ScopePermissionMap.BookingsRead, ScopePermissionMap.BookingsWrite),
        Resource(Permissions.Routes.ResourceServer, "Каталог маршрутів", ScopePermissionMap.RoutesRead, ScopePermissionMap.RoutesWrite),
        Resource(Permissions.Payments.ResourceServer, "Платежі", ScopePermissionMap.PaymentsRead, ScopePermissionMap.PaymentsWrite),
        Resource("reviews-api", "Відгуки", ScopePermissionMap.ReviewsRead)
    ];

    public static IEnumerable<Client> Clients(IConfiguration configuration)
    {
        var clients = configuration.GetSection("Clients");
        var apiScopes = ApiScopes.Select(s => s.Name).ToList();

        return
        [
            // Postman — Authorization Code (+ PKCE, яку Postman підтримує): користувач входить на сторінці логіну.
            new Client
            {
                ClientId = PostmanClientId,
                ClientName = "Postman",
                AllowedGrantTypes = GrantTypes.Code,
                ClientSecrets = { new Secret((clients["PostmanSecret"] ?? "postman-secret").Sha256()) },
                RedirectUris = { clients["PostmanRedirectUri"] ?? "https://oauth.pstmn.io/v1/callback" },
                AllowedScopes = [IdentityServerConstants.StandardScopes.OpenId, IdentityServerConstants.StandardScopes.Profile,
                                 IdentityServerConstants.StandardScopes.Email, RolesResource, .. apiScopes],
                AllowOfflineAccess = true,                        // refresh token (scope offline_access)
                AccessTokenLifetime = 900                         // 15 хвилин, як в Auth Service
            },

            // Aggregator — Client Credentials: сервіс-до-сервісу, без користувача (у токені немає sub).
            new Client
            {
                ClientId = AggregatorClientId,
                ClientName = "Aggregator (service-to-service)",
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                ClientSecrets = { new Secret((clients["AggregatorSecret"] ?? "aggregator-secret").Sha256()) },
                AllowedScopes = [ScopePermissionMap.BookingsRead, ScopePermissionMap.RoutesRead, ScopePermissionMap.ReviewsRead],
                AccessTokenLifetime = 900
            },

            // Swagger UI — public client: секрет у браузері зберегти неможливо, тому обов'язкова PKCE.
            new Client
            {
                ClientId = SwaggerClientId,
                ClientName = "Swagger UI",
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RequireClientSecret = false,
                RequireConsent = false,
                RedirectUris = SwaggerOrigins(clients).Select(o => $"{o}/swagger/oauth2-redirect.html").ToList(),
                AllowedCorsOrigins = SwaggerOrigins(clients).ToList(),
                AllowedScopes = [IdentityServerConstants.StandardScopes.OpenId, IdentityServerConstants.StandardScopes.Profile,
                                 RolesResource, ScopePermissionMap.BookingsRead, ScopePermissionMap.BookingsWrite,
                                 ScopePermissionMap.RoutesRead, ScopePermissionMap.RoutesWrite],
                AccessTokenLifetime = 900
            }
        ];
    }

    private static IEnumerable<string> SwaggerOrigins(IConfigurationSection clients)
        => clients.GetSection("SwaggerOrigins").Get<string[]>() ?? ["https://localhost:7047"];

    private static ApiResource Resource(string name, string displayName, params string[] scopes) =>
        new(name, displayName, [JwtClaimTypes.Role, JwtClaimTypes.Email, JwtClaimTypes.Name])
        {
            Scopes = scopes
        };
}
