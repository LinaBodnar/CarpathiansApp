using Carpathians.Platform.ServiceDefaults.Security;
using Pulumi;
using Pulumi.Keycloak;
using Pulumi.Keycloak.OpenId;
using Pulumi.Keycloak.OpenId.Inputs;

namespace Carpathians.Infra.Identity;

/// <summary>
/// Resource server: client з Authorization Services, його ресурс і client-level ролі — по одній на кожен permission
/// (<c>bookings:read</c>, <c>bookings:approve</c>, ...). Realm-ролі admin/manager/user/guest — composite-ролі,
/// що групують ці client-ролі за матрицею (<see cref="RolePermissionMatrix"/>).
/// </summary>
public sealed record ResourceServer(
    string ClientId,
    Client Client,
    ClientAuthorizationResource Resource,
    IReadOnlyDictionary<string, ClientAuthorizationScope> Scopes,
    IReadOnlyDictionary<string, Role> PermissionRoles);

/// <summary>
/// Authorization Services Keycloak у два кроки (між ними створюються composite realm-ролі):
/// <list type="number">
/// <item><see cref="AddResourceServer"/> — confidential client з увімкненою авторизацією, scopes (= permissions сервісу),
/// ресурс і client-ролі permissions;</item>
/// <item><see cref="WithRolePermissions"/> — role-політика на кожну роль і scope-permission на кожен scope:
/// «дозволено ролям, у яких цей permission є в матриці» (AFFIRMATIVE).</item>
/// </list>
/// Користувач отримує permissions двома шляхами: у RPT (grant_type=urn:ietf:params:oauth:grant-type:uma-ticket,
/// audience=&lt;client&gt;) — claim <c>authorization.permissions[].scopes</c>; у звичайному access token — claim
/// <c>permissions</c> (client-ролі через mapper, <see cref="ClientFactory.WithPermissionsClaim"/>).
/// Обидва читає PermissionAuthorizationHandler сервісів.
/// </summary>
public static class ResourceServerExtensions
{
    public static ResourceServer AddResourceServer(
        this Realm realm,
        string clientId,
        string resourceName,
        string displayName,
        IReadOnlyCollection<string> scopes)
    {
        var client = new Client(clientId, new ClientArgs
        {
            RealmId = realm.Id,
            ClientId = clientId,
            Name = $"{displayName} API ({clientId})",
            Description = $"Resource server {clientId}: permissions {string.Join(", ", scopes)}",
            AccessType = "CONFIDENTIAL",
            ServiceAccountsEnabled = true,          // обов'язково для Authorization Services
            StandardFlowEnabled = false,            // API не входить від імені користувача
            DirectAccessGrantsEnabled = false,
            Authorization = new ClientAuthorizationArgs
            {
                PolicyEnforcementMode = "ENFORCING",
                DecisionStrategy = "UNANIMOUS",     // Permission Evaluation: усі permissions ресурсу мають дозволити
                AllowRemoteResourceManagement = false,
                KeepDefaults = false                // без Default Resource / Default Policy
            }
        });

        // Scopes = permissions сервісу
        var authorizationScopes = scopes.ToDictionary(
            scope => scope,
            scope => new ClientAuthorizationScope($"{clientId}-scope-{Slug(scope)}", new ClientAuthorizationScopeArgs
            {
                RealmId = realm.Id,
                ResourceServerId = client.ResourceServerId,
                Name = scope,
                DisplayName = scope
            }));

        // Ресурс (scopes задаються іменами — тому залежність від створених scopes)
        var resource = new ClientAuthorizationResource($"{clientId}-resource-{resourceName}", new ClientAuthorizationResourceArgs
        {
            RealmId = realm.Id,
            ResourceServerId = client.ResourceServerId,
            Name = resourceName,
            DisplayName = displayName,
            Type = $"urn:{clientId}:resources:{resourceName}",
            Uris = { $"/api/{resourceName}/*" },
            Scopes = scopes.ToArray()
        }, new CustomResourceOptions { DependsOn = authorizationScopes.Values.Select(s => (Resource)s).ToArray() });

        // Client-level ролі: одна на permission — їх групують composite realm-ролі і client-ролі service accounts
        var permissionRoles = scopes.ToDictionary(
            scope => scope,
            scope => new Role($"{clientId}-role-{Slug(scope)}", new RoleArgs
            {
                RealmId = realm.Id,
                ClientId = client.Id,
                Name = scope,
                Description = $"Permission {scope} сервісу {clientId}"
            }));

        return new ResourceServer(clientId, client, resource, authorizationScopes, permissionRoles);
    }

    /// <summary>Permission Policies: role-політика на кожну роль з доступом і scope-permission на кожен scope.</summary>
    public static ResourceServer WithRolePermissions(this ResourceServer server, Realm realm,
        IReadOnlyDictionary<string, Role> roles)
    {
        var clientId = server.ClientId;
        var scopes = server.Scopes.Keys.ToArray();

        // Role-політика для кожної ролі, що має хоча б один permission цього сервісу
        // (payments-api: лише admin — manager/user/guest не отримують жодного permission платежів)
        var policies = roles
            .Where(role => RolePermissionMatrix.For(role.Key).Any(scopes.Contains))
            .ToDictionary(
                role => role.Key,
                role => new ClientRolePolicy($"{clientId}-policy-{role.Key}", new ClientRolePolicyArgs
                {
                    RealmId = realm.Id,
                    ResourceServerId = server.Client.ResourceServerId,
                    Name = $"role-{role.Key}",
                    Description = $"Користувач має realm-роль {role.Key}",
                    Type = "role",
                    Logic = "POSITIVE",
                    DecisionStrategy = "UNANIMOUS",
                    Roles = { new ClientRolePolicyRoleArgs { Id = role.Value.Id, Required = true } }
                }));

        // Scope-permission: матриця «роль -> permission»
        foreach (var scope in scopes)
        {
            var allowedRoles = policies.Keys.Where(role => RolePermissionMatrix.For(role).Contains(scope)).ToArray();
            var args = new ClientAuthorizationPermissionArgs
            {
                RealmId = realm.Id,
                ResourceServerId = server.Client.ResourceServerId,
                Name = $"{scope} permission",
                Description = $"{scope}: {string.Join(", ", allowedRoles)}",
                Type = "scope",
                DecisionStrategy = "AFFIRMATIVE",   // достатньо однієї з ролей
                Resources = { server.Resource.Id },
                Scopes = { server.Scopes[scope].Id }
            };
            foreach (var role in allowedRoles)
                args.Policies.Add(policies[role].Id);

            _ = new ClientAuthorizationPermission($"{clientId}-permission-{Slug(scope)}", args);
        }

        return server;
    }

    private static string Slug(string scope) => scope.Replace(':', '-');
}
