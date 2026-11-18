using Carpathians.Platform.IdentityServer.Data;
using Carpathians.Platform.ServiceDefaults.Security;
using Duende.IdentityModel;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Carpathians.Platform.IdentityServer.Seeding;

/// <summary>
/// Під час старту: міграції трьох контекстів (PersistedGrant, Configuration, Application), конфігурація
/// (identity resources, API scopes, API resources, clients) і тестові користувачі для кожної ролі.
/// Ідемпотентно: наявні записи не дублюються; секрети й redirect URI clients оновлюються з конфігурації.
/// </summary>
public static class IdentityServerSeeder
{
    /// <summary>Тестові користувачі: по одному на роль платформи (матриця permissions — таблиця 7).</summary>
    public static readonly (string UserName, string Role)[] TestUsers =
    [
        ("alice", PlatformRoleNames.Admin),
        ("bob", PlatformRoleNames.Manager),
        ("carol", PlatformRoleNames.User),
        ("dave", PlatformRoleNames.Guest)
    ];

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;

        await provider.GetRequiredService<PersistedGrantDbContext>().Database.MigrateAsync();
        await provider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();

        var config = provider.GetRequiredService<ConfigurationDbContext>();
        await config.Database.MigrateAsync();
        await SeedConfigurationAsync(config, configuration, logger);

        await SeedUsersAsync(provider, configuration, logger);
    }

    private static async Task SeedConfigurationAsync(ConfigurationDbContext db, IConfiguration configuration, ILogger logger)
    {
        foreach (var resource in Config.IdentityResources)
            if (!await db.IdentityResources.AnyAsync(r => r.Name == resource.Name))
                db.IdentityResources.Add(resource.ToEntity());

        foreach (var scope in Config.ApiScopes)
            if (!await db.ApiScopes.AnyAsync(s => s.Name == scope.Name))
                db.ApiScopes.Add(scope.ToEntity());

        foreach (var resource in Config.ApiResources)
            if (!await db.ApiResources.AnyAsync(r => r.Name == resource.Name))
                db.ApiResources.Add(resource.ToEntity());

        foreach (var client in Config.Clients(configuration))
        {
            // Client перезаписується з коду: секрет (параметр AppHost) і redirect URI завжди актуальні.
            var existing = await db.Clients.FirstOrDefaultAsync(c => c.ClientId == client.ClientId);
            if (existing is not null)
                db.Clients.Remove(existing);
            db.Clients.Add(client.ToEntity());
        }

        await db.SaveChangesAsync();
        logger.LogInformation("IdentityServer configuration seeded: {Clients} clients, {Scopes} API scopes",
            await db.Clients.CountAsync(), await db.ApiScopes.CountAsync());
    }

    private static async Task SeedUsersAsync(IServiceProvider provider, IConfiguration configuration, ILogger logger)
    {
        var roles = provider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in PlatformRoleNames.All)
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));

        var password = configuration["TestUsers:Password"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("TestUsers:Password is not set; test users are not seeded");
            return;
        }

        var users = provider.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var (userName, role) in TestUsers)
        {
            var user = await users.FindByNameAsync(userName);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = userName,
                    Email = $"{userName}@carpathians.local",
                    EmailConfirmed = true,
                    DisplayName = char.ToUpperInvariant(userName[0]) + userName[1..]
                };
                var created = await users.CreateAsync(user, password);
                if (!created.Succeeded)
                {
                    logger.LogError("Test user {UserName} was not created: {Errors}", userName,
                        string.Join("; ", created.Errors.Select(e => e.Description)));
                    continue;
                }
                await users.AddClaimsAsync(user,
                [
                    new System.Security.Claims.Claim(JwtClaimTypes.Name, user.DisplayName!),
                    new System.Security.Claims.Claim(JwtClaimTypes.Email, user.Email!)
                ]);
                logger.LogInformation("Test user {UserName} created with role {Role}", userName, role);
            }

            if (!await users.IsInRoleAsync(user, role))
                await users.AddToRoleAsync(user, role);
        }
    }
}
