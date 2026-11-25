using Pulumi;
using Pulumi.Keycloak;
using Pulumi.Keycloak.OpenId;

namespace Carpathians.Infra.Identity;

/// <summary>
/// Фабрика клієнтів realm (extension methods): типові конфігурації в одному місці,
/// а mappers додаються ланцюжком — <c>realm.AddConfidentialClient(...).WithRealmRolesClaim(...).WithAudiences(...)</c>.
/// </summary>
public static class ClientFactory
{
    /// <summary>
    /// Confidential client (секрет генерує Keycloak, див. output postmanClientSecret): Authorization Code;
    /// Direct Access Grants — лише для швидкої перевірки матриці доступу в dev-realm.
    /// </summary>
    public static Client AddConfidentialClient(this Realm realm, string clientId, string name, string[] redirectUris)
        => new(clientId, new ClientArgs
        {
            RealmId = realm.Id,
            ClientId = clientId,
            Name = name,
            AccessType = "CONFIDENTIAL",
            StandardFlowEnabled = true,
            DirectAccessGrantsEnabled = true,
            ImplicitFlowEnabled = false,
            ServiceAccountsEnabled = false,
            ValidRedirectUris = redirectUris,
            ConsentRequired = false
        });

    /// <summary>
    /// Confidential client для Client Credentials Flow (сервіс-до-сервісу, напр. Aggregator): лише service account,
    /// без входу користувачів. Токен не містить user context — ідентичність сервісу і ролі його service account.
    /// </summary>
    public static Client AddServiceAccountClient(this Realm realm, string clientId, string name)
        => new(clientId, new ClientArgs
        {
            RealmId = realm.Id,
            ClientId = clientId,
            Name = name,
            AccessType = "CONFIDENTIAL",
            ServiceAccountsEnabled = true,
            StandardFlowEnabled = false,
            DirectAccessGrantsEnabled = false,
            ImplicitFlowEnabled = false
        });

    /// <summary>
    /// Client-level composite-роль service account: групує permissions resource servers (client-ролі) і
    /// призначається service account клієнта — у його токені Client Credentials з'являться ці permissions.
    /// </summary>
    public static Client WithServiceAccountPermissions(this Client client, Realm realm, string clientId,
        string roleName, IEnumerable<Role> permissionRoles)
    {
        var composite = new Role($"{clientId}-role-{roleName}", new RoleArgs
        {
            RealmId = realm.Id,
            ClientId = client.Id,
            Name = roleName,
            Description = $"Permissions service account {clientId} (composite)",
            CompositeRoles = permissionRoles.Select(r => r.Id).ToArray()
        });

        _ = new UserRoles($"{clientId}-service-account-roles", new UserRolesArgs
        {
            RealmId = realm.Id,
            UserId = client.ServiceAccountUserId,
            RoleIds = { composite.Id },
            Exhaustive = false
        });
        return client;
    }

    /// <summary>Публічний client (SPA / Swagger UI): без секрету, Authorization Code + PKCE (S256).</summary>
    public static Client AddPublicPkceClient(this Realm realm, string clientId, string name, string[] origins)
        => new(clientId, new ClientArgs
        {
            RealmId = realm.Id,
            ClientId = clientId,
            Name = name,
            AccessType = "PUBLIC",
            StandardFlowEnabled = true,
            DirectAccessGrantsEnabled = false,
            ImplicitFlowEnabled = false,
            PkceCodeChallengeMethod = "S256",
            ValidRedirectUris = origins.Select(origin => $"{origin}/swagger/oauth2-redirect.html").ToArray(),
            WebOrigins = origins
        });

    /// <summary>
    /// Mapper: realm-ролі користувача -> claim <c>role</c> (масив) в access token, ID token і userinfo —
    /// той самий claim, що в Auth Service та IdentityServer.
    /// </summary>
    public static Client WithRealmRolesClaim(this Client client, Realm realm, string clientId)
    {
        _ = new UserRealmRoleProtocolMapper($"{clientId}-mapper-roles", new UserRealmRoleProtocolMapperArgs
        {
            RealmId = realm.Id,
            ClientId = client.Id,
            Name = "realm roles -> role",
            ClaimName = "role",
            ClaimValueType = "String",
            Multivalued = true,
            AddToAccessToken = true,
            AddToIdToken = true,
            AddToUserinfo = true
        });
        return client;
    }

    /// <summary>Mappers audience: resource servers у claim <c>aud</c> access token (перевіряють сервіси).</summary>
    public static Client WithAudiences(this Client client, Realm realm, string clientId, IEnumerable<string> audiences)
    {
        foreach (var audience in audiences)
            _ = new AudienceProtocolMapper($"{clientId}-mapper-audience-{audience}", new AudienceProtocolMapperArgs
            {
                RealmId = realm.Id,
                ClientId = client.Id,
                Name = $"audience {audience}",
                IncludedClientAudience = audience,
                AddToAccessToken = true,
                AddToIdToken = false
            });
        return client;
    }

    /// <summary>
    /// Mapper permissions: client-ролі кожного resource server -> claim <c>permissions.&lt;resource server&gt;</c>
    /// в access token, тобто <c>"permissions": {"booking-api": ["bookings:read", ...]}</c>. Враховуються ефективні
    /// ролі (composite realm-ролі розгортаються), тож токен одразу містить permissions за матрицею —
    /// сервіси перевіряють їх локально, без звернення до Keycloak.
    /// </summary>
    public static Client WithPermissionsClaim(this Client client, Realm realm, string clientId,
        IEnumerable<string> resourceServers)
    {
        foreach (var resourceServer in resourceServers)
            _ = new UserClientRoleProtocolMapper($"{clientId}-mapper-permissions-{resourceServer}",
                new UserClientRoleProtocolMapperArgs
                {
                    RealmId = realm.Id,
                    ClientId = client.Id,
                    Name = $"permissions {resourceServer}",
                    ClientIdForRoleMappings = resourceServer,
                    ClaimName = $"permissions.{resourceServer}",
                    ClaimValueType = "String",
                    Multivalued = true,
                    AddToAccessToken = true,
                    AddToIdToken = false,
                    AddToUserinfo = false
                });
        return client;
    }

    /// <summary>
    /// Тестовий користувач з підтвердженим email і однією realm-роллю. Пароль тимчасовий (temporary):
    /// під час першого входу Keycloak вимагає змінити його. Для автоматичної перевірки матриці (password grant)
    /// — config temporaryPasswords=false.
    /// </summary>
    public static User AddTestUser(this Realm realm, string userName, string firstName, Output<string> password,
        Role role, bool temporaryPassword)
    {
        var user = new User($"user-{userName}", new UserArgs
        {
            RealmId = realm.Id,
            Username = userName,
            Email = $"{userName}@carpathians.local",
            EmailVerified = true,
            FirstName = firstName,
            LastName = "Test",
            Enabled = true,
            InitialPassword = new Pulumi.Keycloak.Inputs.UserInitialPasswordArgs { Value = password, Temporary = temporaryPassword }
        });

        _ = new UserRoles($"user-{userName}-roles", new UserRolesArgs
        {
            RealmId = realm.Id,
            UserId = user.Id,
            RoleIds = { role.Id },
            Exhaustive = false          // залишити default-roles-devrealmdrivers
        });

        return user;
    }
}
