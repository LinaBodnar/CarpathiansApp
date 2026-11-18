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

    /// <summary>Усі permissions користувача з токена (Keycloak authorization, scopes IdentityServer, ролі Auth Service).</summary>
    public static IReadOnlySet<string> GetPermissions(this ClaimsPrincipal user)
    {
        var permissions = new HashSet<string>(StringComparer.Ordinal);

        // 1. Keycloak: authorization.permissions[].scopes[]
        foreach (var claim in user.FindAll(Authorization))
            AddKeycloakPermissions(claim.Value, permissions);

        // 2. IdentityServer: scope -> permissions
        foreach (var claim in user.FindAll(Scope))
            foreach (var scope in claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                if (ScopePermissionMap.Permissions.TryGetValue(scope, out var fromScope))
                    permissions.UnionWith(fromScope);

        // 3. Auth Service: роль (User/Admin) -> матриця ролей
        foreach (var claim in user.FindAll(PlatformClaimTypes.Role))
            permissions.UnionWith(RolePermissionMatrix.For(claim.Value));

        return permissions;
    }

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
        });
        builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        return builder;
    }
}
