using System.Security.Cryptography;
using System.Text;
using Carpathians.Auth.Api.Data;
using Carpathians.Platform.ServiceDefaults.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Carpathians.Auth.Api.Services;

/// <summary>Результат ротації: власник токена й нове значення refresh token.</summary>
public sealed record RotationResult(ApplicationUser User, string NewRefreshToken, DateTime ExpiresAt);

/// <summary>
/// Refresh-токени в БД: видача (7 днів), ротація, відкликання.
/// <list type="bullet">
/// <item>ротація: при кожному /auth/refresh-token старий токен відкликається ("Replaced by new token"),
/// а новий записується з посиланням ReplacedByTokenHash;</item>
/// <item>повторне використання вже відкликаного токена — ознака викрадення: відкликаються всі активні
/// токени користувача, і йому доведеться увійти знову;</item>
/// <item>у БД зберігається лише SHA-256 хеш значення.</item>
/// </list>
/// </summary>
public sealed class RefreshTokenService(
    ApplicationDbContext db,
    IJwtTokenService tokens,
    IOptions<JwtSettings> options,
    TimeProvider time,
    ILogger<RefreshTokenService> logger)
{
    private readonly JwtSettings _settings = options.Value;

    public async Task<(string Token, DateTime ExpiresAt)> IssueAsync(ApplicationUser user, string? ip, CancellationToken ct)
    {
        var (value, entity) = Create(user.Id, ip);
        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync(ct);
        return (value, entity.ExpiresAt);
    }

    /// <summary>Ротація: null — токен невідомий, прострочений або відкликаний.</summary>
    public async Task<RotationResult?> RotateAsync(string refreshToken, string? ip, CancellationToken ct)
    {
        var now = Now;
        var stored = await FindAsync(refreshToken, ct);
        if (stored is null)
            return null;

        if (stored.IsRevoked)
        {
            // Хтось пред'явив уже використаний токен: компрометація ланцюжка — закриваємо всі сесії користувача.
            logger.LogWarning("Reuse of revoked refresh token {TokenId} for user {UserId}; revoking all active tokens",
                stored.Id, stored.UserId);
            await RevokeAllAsync(stored.UserId, ip, "Attempted reuse of revoked token", ct);
            return null;
        }

        if (stored.IsExpired(now))
            return null;

        var (value, replacement) = Create(stored.UserId, ip);
        Revoke(stored, ip, "Replaced by new token", replacement.TokenHash);
        db.RefreshTokens.Add(replacement);
        await db.SaveChangesAsync(ct);

        return new RotationResult(stored.User!, value, replacement.ExpiresAt);
    }

    /// <summary>Відкликає один токен; false — токен не знайдено або він уже неактивний.</summary>
    public async Task<bool> RevokeAsync(string refreshToken, string? ip, string reason, CancellationToken ct, string? ownerId = null)
    {
        var stored = await FindAsync(refreshToken, ct);
        if (stored is null || !stored.IsActive(Now) || (ownerId is not null && stored.UserId != ownerId))
            return false;

        Revoke(stored, ip, reason, replacedBy: null);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Відкликає всі активні токени користувача (скидання/зміна пароля, видалення ролі, компрометація).</summary>
    public async Task<int> RevokeAllAsync(string userId, string? ip, string reason, CancellationToken ct)
    {
        var now = Now;
        var active = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(ct);
        foreach (var token in active)
            Revoke(token, ip, reason, replacedBy: null);
        await db.SaveChangesAsync(ct);
        return active.Count;
    }

    /// <summary>Історія токенів користувача (без значень і хешів).</summary>
    public async Task<IReadOnlyList<RefreshToken>> HistoryAsync(string userId, CancellationToken ct)
        => await db.RefreshTokens.AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

    private (string Value, RefreshToken Entity) Create(string userId, string? ip)
    {
        var value = tokens.GenerateRefreshToken();
        var now = Now;
        return (value, new RefreshToken
        {
            UserId = userId,
            TokenHash = Hash(value),
            CreatedAt = now,
            CreatedByIp = ip,
            ExpiresAt = now.AddDays(_settings.RefreshTokenDays)
        });
    }

    private void Revoke(RefreshToken token, string? ip, string reason, string? replacedBy)
    {
        token.RevokedAt = Now;
        token.RevokedByIp = ip;
        token.ReasonRevoked = reason;
        token.ReplacedByTokenHash = replacedBy;
    }

    private Task<RefreshToken?> FindAsync(string refreshToken, CancellationToken ct)
    {
        var hash = Hash(refreshToken);
        return db.RefreshTokens.Include(t => t.User).SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
    }

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
