using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Carpathians.Auth.Api.Data;

/// <summary>
/// База IdentityDb: стандартні таблиці ASP.NET Identity (AspNetUsers, AspNetRoles, AspNetUserRoles,
/// AspNetUserClaims, AspNetUserTokens...) + RefreshTokens.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole, string>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(user =>
        {
            user.Property(u => u.DisplayName).HasMaxLength(100);
        });

        builder.Entity<RefreshToken>(token =>
        {
            token.ToTable("RefreshTokens");
            token.HasKey(t => t.Id);
            token.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
            token.HasIndex(t => t.TokenHash).IsUnique();
            token.Property(t => t.ReplacedByTokenHash).HasMaxLength(64);
            token.Property(t => t.CreatedByIp).HasMaxLength(64);
            token.Property(t => t.RevokedByIp).HasMaxLength(64);
            token.Property(t => t.ReasonRevoked).HasMaxLength(200);
            token.HasIndex(t => new { t.UserId, t.ExpiresAt });
            token.HasOne(t => t.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

/// <summary>
/// Фабрика для dotnet ef (migrations add): дає контекст без запуску застосунку й без AppHost.
/// Рядок підключення потрібен лише для генерації міграцій; застосовуються вони під час старту сервісу.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost,14333;Database=IdentityDb;Integrated Security=false;TrustServerCertificate=true")
            .Options;
        return new ApplicationDbContext(options);
    }
}
