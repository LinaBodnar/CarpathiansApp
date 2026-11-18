using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Carpathians.Auth.Api.Data;
using Carpathians.Platform.ServiceDefaults.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Carpathians.Auth.Api.Services;

/// <summary>Access token і значення refresh token.</summary>
public interface IJwtTokenService
{
    /// <summary>JWT на 15 хвилин: sub (userId), unique_name (username), email, role (усі ролі), jti.</summary>
    AccessToken GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles);

    /// <summary>Криптографічно випадкове значення refresh token (64 байти, base64url).</summary>
    string GenerateRefreshToken();

    /// <summary>Перевіряє підпис, issuer, audience і (за потреби) термін дії; null — токен недійсний.</summary>
    ClaimsPrincipal? ValidateToken(string token, bool validateLifetime = true);
}

public sealed record AccessToken(string Token, DateTime ExpiresAt);

public sealed class JwtTokenService(IOptions<JwtSettings> options, TimeProvider time) : IJwtTokenService
{
    private readonly JwtSettings _settings = options.Value;
    private readonly JwtSecurityTokenHandler _handler = new() { MapInboundClaims = false };

    public AccessToken GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_settings.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName!),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(now).ToString(), ClaimValueTypes.Integer64)
        };
        // Ролі в токені: мікросервіси перевіряють [Authorize(Roles = ...)] без звернення до БД.
        claims.AddRange(roles.Select(role => new Claim(PlatformClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: new SigningCredentials(_settings.CreateSigningKey(), SecurityAlgorithms.HmacSha256));

        return new AccessToken(_handler.WriteToken(token), expires);
    }

    public string GenerateRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));

    public ClaimsPrincipal? ValidateToken(string token, bool validateLifetime = true)
    {
        var parameters = PlatformAuthentication.CreateValidationParameters(_settings);
        parameters.ValidateLifetime = validateLifetime;
        try
        {
            return _handler.ValidateToken(token, parameters, out _);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;                                    // не JWT
        }
    }
}
