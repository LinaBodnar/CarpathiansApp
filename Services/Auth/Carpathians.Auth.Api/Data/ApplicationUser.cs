using Microsoft.AspNetCore.Identity;

namespace Carpathians.Auth.Api.Data;

/// <summary>Користувач платформи: стандартні поля ASP.NET Identity + профіль і refresh-токени.</summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>Ім'я для відображення (профіль, PUT /users/{id}).</summary>
    public string? DisplayName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

/// <summary>
/// Refresh token у БД (таблиця RefreshTokens). Зберігається лише SHA-256 хеш значення: витік бази
/// не дає готових токенів. Ланцюжок ротації — через ReplacedByTokenHash.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    /// <summary>SHA-256 (hex) значення токена, яке отримав клієнт.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public string? CreatedByIp { get; set; }
    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }
    public string? RevokedByIp { get; set; }
    public string? ReasonRevoked { get; set; }

    /// <summary>Хеш токена, що замінив цей при ротації.</summary>
    public string? ReplacedByTokenHash { get; set; }

    public bool IsExpired(DateTime now) => now >= ExpiresAt;
    public bool IsRevoked => RevokedAt is not null;
    public bool IsActive(DateTime now) => !IsRevoked && !IsExpired(now);
}
