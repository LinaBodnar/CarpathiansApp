using Carpathians.Platform.ServiceDefaults.Security;
using Pulumi;
using Pulumi.Keycloak;

namespace Carpathians.Infra.Identity;

/// <summary>
/// Realm DevRealmDrivers платформи Carpathians:
/// <list type="number">
/// <item>resource servers booking-api, catalog-api, payments-api — ресурс, scopes (= permissions сервісів),
/// client-ролі permissions;</item>
/// <item>realm-ролі admin, manager, user, guest — composite-ролі над client-ролями за матрицею
/// <see cref="RolePermissionMatrix"/>; role-політики і scope-permissions (Permission Policies) за тією ж матрицею;</item>
/// <item>клієнти (фабрика <see cref="ClientFactory"/>): postman (Authorization Code), swagger (публічний, PKCE),
/// aggregator (Client Credentials, service account) з mappers ролей, audience і permissions;</item>
/// <item>тестові користувачі — по одному на роль, з тимчасовим паролем і підтвердженим email.</item>
/// </list>
/// </summary>
public sealed class KeycloakStack : Stack
{
    public const string RealmName = "DevRealmDrivers";

    /// <summary>Тестові користувачі: по одному на кожну роль матриці.</summary>
    public static readonly (string UserName, string FirstName, string Role)[] TestUsers =
    [
        ("alice", "Alice", PlatformRoleNames.Admin),
        ("bob", "Bob", PlatformRoleNames.Manager),
        ("carol", "Carol", PlatformRoleNames.User),
        ("dave", "Dave", PlatformRoleNames.Guest)
    ];

    public KeycloakStack()
    {
        var config = new Pulumi.Config();
        var keycloakUrl = new Pulumi.Config("keycloak").Require("url").TrimEnd('/');
        var testUserPassword = config.RequireSecret("testUserPassword");
        var postmanRedirectUri = config.Get("postmanRedirectUri") ?? "https://oauth.pstmn.io/v1/callback";
        var swaggerOrigins = config.GetObject<string[]>("swaggerOrigins") ?? ["https://localhost:7047"];
        var temporaryPasswords = config.GetBoolean("temporaryPasswords") ?? true;

        // ---------- Realm ----------
        var realm = new Realm("realm", new RealmArgs
        {
            RealmName = RealmName,
            DisplayName = "Carpathians",
            Enabled = true,
            RegistrationAllowed = true,         // самостійна реєстрація
            RememberMe = true,
            VerifyEmail = false,                // листи в dev-realm не надсилаються
            LoginTheme = "keycloak.v2",
            AccountTheme = "keycloak.v3",
            AdminTheme = "keycloak.v2",
            EmailTheme = "keycloak",
            LoginWithEmailAllowed = true,
            DuplicateEmailsAllowed = false,
            ResetPasswordAllowed = true,
            SslRequired = "external",
            AccessTokenLifespan = "15m",        // як у Auth Service
            SsoSessionIdleTimeout = "30m",
            SsoSessionMaxLifespan = "10h",
            RevokeRefreshToken = true,          // ротація refresh token
            RefreshTokenMaxReuse = 0
        });

        // ---------- Resource servers: client + scopes + ресурс + client-ролі permissions ----------
        var resourceServers = new[]
        {
            realm.AddResourceServer(Permissions.Bookings.ResourceServer, "bookings", "Бронювання", Permissions.Bookings.All),
            realm.AddResourceServer(Permissions.Routes.ResourceServer, "routes", "Маршрути", Permissions.Routes.All),
            realm.AddResourceServer(Permissions.Payments.ResourceServer, "payments", "Платежі", Permissions.Payments.All)
        };
        var audiences = resourceServers.Select(r => r.ClientId).ToArray();
        var permissionRoles = resourceServers
            .SelectMany(r => r.PermissionRoles)
            .ToDictionary(p => p.Key, p => p.Value);

        // ---------- Realm-ролі: composite над client-ролями permissions за матрицею ----------
        var roles = PlatformRoleNames.All.ToDictionary(
            name => name,
            name => new Role($"role-{name}", new RoleArgs
            {
                RealmId = realm.Id,
                Name = name,
                Description = RoleDescriptions[name],
                CompositeRoles = RolePermissionMatrix.For(name).Select(p => permissionRoles[p].Id).ToArray()
            }));

        // ---------- Permission Policies: role-політики і scope-permissions ----------
        foreach (var server in resourceServers)
            server.WithRolePermissions(realm, roles);

        // ---------- Клієнти ----------
        var postman = realm.AddConfidentialClient("postman", "Postman (Authorization Code)", [postmanRedirectUri])
            .WithRealmRolesClaim(realm, "postman")
            .WithAudiences(realm, "postman", audiences)
            .WithPermissionsClaim(realm, "postman", audiences);

        var swagger = realm.AddPublicPkceClient("swagger", "Swagger UI (Authorization Code + PKCE)", swaggerOrigins)
            .WithRealmRolesClaim(realm, "swagger")
            .WithAudiences(realm, "swagger", audiences)
            .WithPermissionsClaim(realm, "swagger", audiences);

        // Aggregator — Client Credentials: лише читання бронювань і маршрутів (composite client-роль service account)
        var aggregatorAudiences = new[] { Permissions.Bookings.ResourceServer, Permissions.Routes.ResourceServer };
        var aggregator = realm.AddServiceAccountClient("aggregator", "Aggregator (Client Credentials)")
            .WithServiceAccountPermissions(realm, "aggregator", "service-reader",
                [permissionRoles[Permissions.Bookings.Read], permissionRoles[Permissions.Routes.Read]])
            .WithAudiences(realm, "aggregator", aggregatorAudiences)
            .WithPermissionsClaim(realm, "aggregator", aggregatorAudiences);

        // ---------- Тестові користувачі ----------
        foreach (var (userName, firstName, role) in TestUsers)
            realm.AddTestUser(userName, firstName, testUserPassword, roles[role], temporaryPasswords);

        // ---------- Outputs ----------
        RealmId = realm.RealmName;
        RealmUrl = Output.Create($"{keycloakUrl}/realms/{RealmName}");
        AdminConsoleUrl = Output.Create($"{keycloakUrl}/admin/master/console/#/{RealmName}");
        ClientIds = Output.Tuple(postman.ClientId, swagger.ClientId, aggregator.ClientId)
            .Apply(ids => new Dictionary<string, string>
            {
                ["authorizationCode"] = ids.Item1,
                ["authorizationCodePkce"] = ids.Item2,
                ["clientCredentials"] = ids.Item3
            });
        ClientSecrets = Output.CreateSecret(Output.Tuple(postman.ClientSecret, aggregator.ClientSecret)
            .Apply(secrets => new Dictionary<string, string>
            {
                ["postman"] = secrets.Item1,
                ["aggregator"] = secrets.Item2
            }));
        Issuer = Output.Create($"{keycloakUrl}/realms/{RealmName}");
        TokenEndpoint = Output.Create($"{keycloakUrl}/realms/{RealmName}/protocol/openid-connect/token");
        AuthorizationEndpoint = Output.Create($"{keycloakUrl}/realms/{RealmName}/protocol/openid-connect/auth");
        ResourceServers = Output.Create(audiences);
        PostmanClientId = postman.ClientId;
        PostmanClientSecret = Output.CreateSecret(postman.ClientSecret);
        SwaggerClientId = swagger.ClientId;
        Users = Output.Create(TestUsers.ToDictionary(u => u.UserName, u => u.Role));
        RolePermissions = Output.Create(PlatformRoleNames.All.ToDictionary(
            role => role, role => string.Join(", ", RolePermissionMatrix.For(role))));
    }

    private static readonly IReadOnlyDictionary<string, string> RoleDescriptions = new Dictionary<string, string>
    {
        [PlatformRoleNames.Admin] = "Адміністратор: усі permissions усіх resource servers",
        [PlatformRoleNames.Manager] = "Менеджер: перегляд, зміна й підтвердження бронювань; зміна й публікація маршрутів",
        [PlatformRoleNames.User] = "Турист: перегляд і створення власних бронювань, перегляд маршрутів",
        [PlatformRoleNames.Guest] = "Гість: лише перегляд маршрутів"
    };

    [Output("realm")] public Output<string> RealmId { get; set; }
    [Output("realmUrl")] public Output<string> RealmUrl { get; set; }
    [Output("adminConsoleUrl")] public Output<string> AdminConsoleUrl { get; set; }
    [Output("clientIds")] public Output<Dictionary<string, string>> ClientIds { get; set; }
    [Output("clientSecrets")] public Output<Dictionary<string, string>> ClientSecrets { get; set; }
    [Output("issuer")] public Output<string> Issuer { get; set; }
    [Output("tokenEndpoint")] public Output<string> TokenEndpoint { get; set; }
    [Output("authorizationEndpoint")] public Output<string> AuthorizationEndpoint { get; set; }
    [Output("resourceServers")] public Output<string[]> ResourceServers { get; set; }
    [Output("postmanClientId")] public Output<string> PostmanClientId { get; set; }
    [Output("postmanClientSecret")] public Output<string> PostmanClientSecret { get; set; }
    [Output("swaggerClientId")] public Output<string> SwaggerClientId { get; set; }
    [Output("testUsers")] public Output<Dictionary<string, string>> Users { get; set; }
    [Output("rolePermissions")] public Output<Dictionary<string, string>> RolePermissions { get; set; }
}
