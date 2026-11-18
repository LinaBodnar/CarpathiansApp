using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Carpathians.Platform.ServiceDefaults.Security;

/// <summary>Вимога: у токені користувача є конкретний permission (напр. "bookings:update").</summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>
/// Permission-based авторизація на рівні endpoint: <c>[RequirePermission(Permissions.Bookings.Update)]</c>.
/// Політика з такою назвою містить <see cref="PermissionRequirement"/>; рішення приймає мікросервіс ЛОКАЛЬНО,
/// без звернення до Identity Provider. Немає permission -> 403 Forbidden (а не 500).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission) : base(permission)
        => Permission = permission;

    public string Permission { get; }
}

/// <summary>
/// Перевіряє permission у токені. Джерела permissions (усі — з самого JWT):
/// <list type="number">
/// <item>Keycloak: claim <c>authorization</c> — <c>{"permissions":[{"rsname":"booking-api","scopes":["bookings:read",...]}]}</c>;</item>
/// <item>IdentityServer: claim <c>scope</c> ("bookings.read", "bookings.write"...) через <see cref="ScopePermissionMap"/>;</item>
/// <item>Auth Service: ролі User/Admin через <see cref="RolePermissionMatrix"/>.</item>
/// </list>
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasPermission(requirement.Permission))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

/// <summary>Читання permissions із claims токена.</summary>
public static class PermissionClaims
{
    /// <summary>Claim Keycloak Authorization Services (RPT) з наданими permissions.</summary>
    public const string Authorization = "authorization";

    /// <summary>OAuth 2.0 scopes (IdentityServer): кілька claims або один рядок через пробіл.</summary>
    public const string Scope = "scope";

    public static bool HasPermission(this ClaimsPrincipal user, string permission)
        => user.Identity?.IsAuthenticated == true && user.GetPermissions().Contains(permission);

    /// <summary>
    /// Усі permissions з токена:
    /// <list type="bullet">
    /// <item>Keycloak — claim <c>authorization</c> (permissions уже обчислені Identity Provider за матрицею ролей);</item>
    /// <item>токен лише зі scopes (Client Credentials, сервіс без користувача) — permissions scopes;</item>
    /// <item>токен лише з ролями (Auth Service) — permissions ролей за матрицею;</item>
    /// <item>делегований токен користувача (IdentityServer, Authorization Code): і scopes, і ролі —
    /// ПЕРЕТИН: клієнт не отримає більше, ніж дозволено користувачу, і більше, ніж дозволяють scopes.</item>
    /// </list>
    /// </summary>
    public static IReadOnlySet<string> GetPermissions(this ClaimsPrincipal user)
    {
        var permissions = new HashSet<string>(StringComparer.Ordinal);

        // 1. Keycloak: authorization.permissions[].scopes[]
        foreach (var claim in user.FindAll(Authorization))
            AddKeycloakPermissions(claim.Value, permissions);

        // 2. IdentityServer: scope -> permissions (null — у токені немає жодного відомого scope)
        HashSet<string>? fromScopes = null;
        foreach (var scope in user.GetScopes())
            if (ScopePermissionMap.Permissions.TryGetValue(scope, out var mapped))
                (fromScopes ??= new HashSet<string>(StringComparer.Ordinal)).UnionWith(mapped);

        // 3. Ролі (Auth Service: User/Admin; IdentityServer: admin/manager/user/guest) -> матриця
        HashSet<string>? fromRoles = null;
        foreach (var claim in user.FindAll(PlatformClaimTypes.Role))
            if (RolePermissionMatrix.Permissions.TryGetValue(claim.Value, out var mapped))
                (fromRoles ??= new HashSet<string>(StringComparer.Ordinal)).UnionWith(mapped);

        if (fromScopes is not null && fromRoles is not null)
            fromScopes.IntersectWith(fromRoles);
        permissions.UnionWith(fromScopes ?? fromRoles ?? []);
        return permissions;
    }

    /// <summary>OAuth-scopes токена (масив claims "scope" або один рядок через пробіл).</summary>
    public static IEnumerable<string> GetScopes(this ClaimsPrincipal user)
        => user.FindAll(Scope).SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public static bool HasScope(this ClaimsPrincipal user, string scope)
        => user.Identity?.IsAuthenticated == true && user.GetScopes().Contains(scope, StringComparer.Ordinal);

    private static void AddKeycloakPermissions(string json, HashSet<string> permissions)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("permissions", out var list) || list.ValueKind != JsonValueKind.Array)
                return;

            foreach (var permission in list.EnumerateArray())
                if (permission.TryGetProperty("scopes", out var scopes) && scopes.ValueKind == JsonValueKind.Array)
                    foreach (var scope in scopes.EnumerateArray())
                        if (scope.GetString() is { Length: > 0 } value)
                            permissions.Add(value);
        }
        catch (JsonException)
        {
            // некоректний claim — жодних permissions з нього
        }
    }
}

/// <summary>Назви scope-політик: збігаються з назвами OAuth-scopes IdentityServer.</summary>
public static class ScopePolicies
{
    public const string BookingsRead = ScopePermissionMap.BookingsRead;
    public const string RoutesRead = ScopePermissionMap.RoutesRead;
    public const string ReviewsRead = ScopePermissionMap.ReviewsRead;

    public static readonly string[] All =
    [
        ScopePermissionMap.BookingsRead, ScopePermissionMap.BookingsWrite,
        ScopePermissionMap.RoutesRead, ScopePermissionMap.RoutesWrite,
        ScopePermissionMap.PaymentsRead, ScopePermissionMap.PaymentsWrite,
        ScopePermissionMap.ReviewsRead
    ];
}

/// <summary>Реєстрація permission-based авторизації в сервісі.</summary>
public static class PermissionAuthorizationExtensions
{
    /// <summary>Політика на кожен permission (таблиці 3.3.1–3.3.3) + обробник, що читає permissions з токена.</summary>
    public static TBuilder AddPermissionAuthorization<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddAuthorization(options =>
        {
            foreach (var permission in Permissions.All)
                options.AddPolicy(permission, policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement(permission)));

            // Scope-based політики (IdentityServer): [Authorize(Policy = ScopePolicies.BookingsRead)] — напр. gRPC для Aggregator
            foreach (var scope in ScopePolicies.All)
                options.AddPolicy(scope, policy => policy
                    .RequireAuthenticatedUser()
                    .RequireAssertion(context => context.User.HasScope(scope)));
        });
        builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        return builder;
    }
}
