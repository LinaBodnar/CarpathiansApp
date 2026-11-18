using Carpathians.Auth.Api.Controllers;
using Carpathians.Platform.ServiceDefaults.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Carpathians.Auth.Api.Data;

/// <summary>
/// Під час старту: застосовує міграції IdentityDb, створює ролі User і Admin та (якщо задано пароль)
/// адміністратора з підтвердженим email. Ідемпотентно — повторний запуск нічого не дублює.
/// </summary>
public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, ILogger logger, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        var db = provider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync(ct);

        var roles = provider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in PlatformRoles.All)
        {
            if (await roles.RoleExistsAsync(role))
                continue;
            await roles.CreateAsync(new IdentityRole(role));
            logger.LogInformation("Role {Role} created", role);
        }

        var admin = provider.GetRequiredService<IOptions<AuthOptions>>().Value.Admin;
        if (string.IsNullOrWhiteSpace(admin.Password))
        {
            logger.LogWarning("Auth:Admin:Password is not set; the administrator account is not seeded");
            return;
        }

        var users = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByNameAsync(admin.UserName);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = admin.UserName,
                Email = admin.Email,
                EmailConfirmed = true,
                DisplayName = "Administrator"
            };
            var created = await users.CreateAsync(user, admin.Password);
            if (!created.Succeeded)
            {
                logger.LogError("Administrator was not created: {Errors}", string.Join("; ", created.Errors.Select(e => e.Description)));
                return;
            }
            logger.LogInformation("Administrator {UserName} created", admin.UserName);
        }

        foreach (var role in PlatformRoles.All)
            if (!await users.IsInRoleAsync(user, role))
                await users.AddToRoleAsync(user, role);
    }
}
