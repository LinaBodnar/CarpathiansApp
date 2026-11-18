using System.Security.Claims;
using Carpathians.Platform.ServiceDefaults.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Carpathians.Tests.Security;

/// <summary>
/// Permission-based авторизація (Лаб. 09): рішення за permissions з токена — матриця таблиці 3.3.1
/// для ролей admin / manager / user / guest на кожному endpoint бронювань.
/// </summary>
[TestFixture]
public class PermissionAuthorizationTests
{
    private IAuthorizationService _authorization = null!;
    private ServiceProvider _services = null!;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAuthorization(options =>
        {
            foreach (var permission in Permissions.All)
                options.AddPolicy(permission, p => p.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(permission)));
        });
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        _services = services.BuildServiceProvider();
        _authorization = _services.GetRequiredService<IAuthorizationService>();
    }

    [TearDown]
    public void TearDown() => _services.Dispose();

    // endpoint (таблиця 3.3.1)            permission                     admin  manager user   guest
    [TestCase("GET /api/bookings", Permissions.Bookings.Read, true, true, true, false)]
    [TestCase("POST /api/bookings", Permissions.Bookings.Create, true, false, true, false)]
    [TestCase("PUT /api/bookings/{id}", Permissions.Bookings.Update, true, true, false, false)]
    [TestCase("DELETE /api/bookings/{id}", Permissions.Bookings.Delete, true, false, false, false)]
    [TestCase("POST /api/bookings/{id}/approve", Permissions.Bookings.Approve, true, true, false, false)]
    [TestCase("GET /api/routes", Permissions.Routes.Read, true, true, true, true)]
    [TestCase("POST /api/routes", Permissions.Routes.Create, true, false, false, false)]
    [TestCase("POST /api/routes/{id}/publish", Permissions.Routes.Publish, true, true, false, false)]
    [TestCase("POST /api/payments/refund", Permissions.Payments.Refund, true, false, false, false)]
    public async Task Role_matrix_from_keycloak_permissions_claim(
        string endpoint, string permission, bool admin, bool manager, bool user, bool guest)
    {
        var expected = new Dictionary<string, bool>
        {
            [PlatformRoleNames.Admin] = admin,
            [PlatformRoleNames.Manager] = manager,
            [PlatformRoleNames.User] = user,
            [PlatformRoleNames.Guest] = guest
        };

        foreach (var (role, allowed) in expected)
        {
            var result = await _authorization.AuthorizeAsync(KeycloakUser(role), null, permission);
            Assert.That(result.Succeeded, Is.EqualTo(allowed), $"{role} -> {endpoint}");
        }
    }

    [Test]
    public async Task Auth_service_roles_are_mapped_to_permissions()
    {
        var user = Principal(new Claim(PlatformClaimTypes.Role, PlatformRoles.User));
        var admin = Principal(new Claim(PlatformClaimTypes.Role, PlatformRoles.Admin));

        Assert.That((await _authorization.AuthorizeAsync(user, null, Permissions.Bookings.Create)).Succeeded, Is.True);
        Assert.That((await _authorization.AuthorizeAsync(user, null, Permissions.Payments.Read)).Succeeded, Is.False);
        Assert.That((await _authorization.AuthorizeAsync(admin, null, Permissions.Payments.Refund)).Succeeded, Is.True);
    }

    [Test]
    public async Task Identity_server_scopes_are_mapped_to_permissions()
    {
        var service = Principal(new Claim(PermissionClaims.Scope, $"{ScopePermissionMap.BookingsRead} {ScopePermissionMap.RoutesRead}"));

        Assert.That((await _authorization.AuthorizeAsync(service, null, Permissions.Bookings.Read)).Succeeded, Is.True);
        Assert.That((await _authorization.AuthorizeAsync(service, null, Permissions.Bookings.Update)).Succeeded, Is.False);
    }

    [Test]
    public async Task Anonymous_user_is_never_authorized()
    {
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.That((await _authorization.AuthorizeAsync(anonymous, null, Permissions.Routes.Read)).Succeeded, Is.False);
    }

    /// <summary>Токен Keycloak: permissions ролі (як їх видає Authorization Services) у claim "authorization".</summary>
    private static ClaimsPrincipal KeycloakUser(string role)
    {
        var permissions = RolePermissionMatrix.For(role)
            .GroupBy(p => p.Split(':')[0])
            .Select(g => $$"""{"rsname":"{{g.Key}}","scopes":[{{string.Join(",", g.Select(s => $"\"{s}\""))}}]}""");
        var json = $$"""{"permissions":[{{string.Join(",", permissions)}}]}""";
        return Principal(new Claim(PermissionClaims.Authorization, json, "JSON"));
    }

    private static ClaimsPrincipal Principal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, "Bearer", PlatformClaimTypes.UserName, PlatformClaimTypes.Role));
}
