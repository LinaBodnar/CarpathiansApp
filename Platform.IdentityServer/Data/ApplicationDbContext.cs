using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Carpathians.Platform.IdentityServer.Data;

/// <summary>Користувач IdentityServer (ASP.NET Identity у базі IdentityServerDb, окремо від бізнес-сервісів).</summary>
public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
}

/// <summary>
/// Таблиці ASP.NET Identity (AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims...) у IdentityServerDb.
/// Поруч у тій самій базі — ConfigurationDbContext (Clients, ApiResources, ApiScopes, IdentityResources)
/// і PersistedGrantDbContext (PersistedGrants, DeviceCodes, Keys) з Duende.IdentityServer.EntityFramework.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole, string>(options);
