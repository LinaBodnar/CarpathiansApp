using System.Security.Claims;
using Carpathians.Platform.ServiceDefaults.Security;

namespace Carpathians.Booking.Tests.Security;

/// <summary>
/// Локальна перевірка permissions (Лаб. 09, таблиці 3.3.1–3.3.3): той самий код, що й PermissionAuthorizationHandler
/// у кожному сервісі. Токени всіх трьох Identity Provider дають однакову матрицю доступу.
/// </summary>
[TestFixture]
public class PermissionMatrixTests
{
    // ---------- Keycloak: claim "permissions" (client-ролі, composite realm-ролі) ----------

    [Test]
    public void KeycloakManager_CanUpdateAndApprove_ButNotDeleteBookings()
    {
        var manager = Principal(new Claim("permissions",
            """{"booking-api":["bookings:read","bookings:update","bookings:approve"],"catalog-api":["routes:read","routes:update","routes:publish"]}""",
            "JSON"));

        Assert.Multiple(() =>
        {
            Assert.That(manager.HasPermission(Permissions.Bookings.Read), Is.True);        // GET    /api/bookings      -> 200
            Assert.That(manager.HasPermission(Permissions.Bookings.Update), Is.True);      // PUT    /api/bookings/{id} -> 200
            Assert.That(manager.HasPermission(Permissions.Bookings.Approve), Is.True);     // POST   /{id}/approve      -> 200
            Assert.That(manager.HasPermission(Permissions.Bookings.Delete), Is.False);     // DELETE /api/bookings/{id} -> 403
            Assert.That(manager.HasPermission(Permissions.Bookings.Create), Is.False);     // POST   /api/bookings      -> 403
            Assert.That(manager.HasPermission(Permissions.Payments.Read), Is.False);       // GET    /api/payments      -> 403
        });
    }

    // ---------- Keycloak RPT: claim "authorization.permissions[].scopes" ----------

    [Test]
    public void KeycloakRpt_PermissionsAreReadFromAuthorizationClaim()
    {
        var rpt = Principal(new Claim("authorization",
            """{"permissions":[{"rsname":"bookings","scopes":["bookings:read","bookings:create"]}]}""", "JSON"));

        Assert.That(rpt.GetPermissions(), Is.EquivalentTo(new[] { Permissions.Bookings.Read, Permissions.Bookings.Create }));
    }

    // ---------- Auth Service (лише ролі) -> матриця ----------

    [TestCase(PlatformRoleNames.Admin, Permissions.Payments.Refund, true)]
    [TestCase(PlatformRoleNames.Manager, Permissions.Payments.Read, false)]
    [TestCase(PlatformRoleNames.User, Permissions.Payments.Process, false)]
    [TestCase(PlatformRoleNames.User, Permissions.Bookings.Create, true)]
    [TestCase(PlatformRoleNames.User, Permissions.Routes.Update, false)]
    [TestCase(PlatformRoleNames.Guest, Permissions.Routes.Read, true)]
    [TestCase(PlatformRoleNames.Guest, Permissions.Bookings.Read, false)]
    [TestCase(PlatformRoleNames.Manager, Permissions.Routes.Publish, true)]
    [TestCase(PlatformRoleNames.Manager, Permissions.Routes.Delete, false)]
    public void Role_GetsPermissionsFromMatrix(string role, string permission, bool allowed)
        => Assert.That(Principal(new Claim(PlatformClaimTypes.Role, role)).HasPermission(permission), Is.EqualTo(allowed));

    // ---------- IdentityServer: scopes ∩ ролі ----------

    [Test]
    public void IdentityServerUserToken_IsIntersectionOfScopesAndRoles()
    {
        // manager, але клієнт отримав лише bookings.read — змінювати не може
        var token = Principal(
            new Claim(PlatformClaimTypes.Role, PlatformRoleNames.Manager),
            new Claim("scope", "openid profile bookings.read"));

        Assert.Multiple(() =>
        {
            Assert.That(token.HasPermission(Permissions.Bookings.Read), Is.True);
            Assert.That(token.HasPermission(Permissions.Bookings.Update), Is.False);
        });
    }

    [Test]
    public void ClientCredentialsToken_HasOnlyScopePermissions()
    {
        var aggregator = Principal(new Claim("scope", "bookings.read routes.read"));

        Assert.That(aggregator.GetPermissions(), Is.EquivalentTo(new[] { Permissions.Bookings.Read, Permissions.Routes.Read }));
    }

    [Test]
    public void AnonymousUser_HasNoPermissions()
        => Assert.That(new ClaimsPrincipal(new ClaimsIdentity()).HasPermission(Permissions.Routes.Read), Is.False);

    private static ClaimsPrincipal Principal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, authenticationType: "Bearer"));
}
