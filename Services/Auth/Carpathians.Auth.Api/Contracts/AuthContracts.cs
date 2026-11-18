using System.ComponentModel.DataAnnotations;

namespace Carpathians.Auth.Api.Contracts;

public sealed record RegisterRequest(
    [Required, StringLength(50, MinimumLength = 3), RegularExpression("^[a-zA-Z0-9._-]+$")] string UserName,
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128, MinimumLength = 8)] string Password,
    [StringLength(100)] string? DisplayName = null);

public sealed record RegisterResponse(string UserId, string UserName, string Email, bool EmailConfirmationRequired);

/// <summary>Login — ім'я користувача або email.</summary>
public sealed record LoginRequest(
    [Required] string Login,
    [Required] string Password);

/// <summary>Access token — у тілі відповіді; refresh token — лише в HttpOnly cookie.</summary>
public sealed record TokenResponse(string AccessToken, string TokenType, DateTime ExpiresAt, int ExpiresIn, UserDto User);

/// <summary>Refresh token у тілі — альтернатива cookie (для клієнтів без cookie, напр. сервісних скриптів).</summary>
public sealed record RefreshTokenRequest(string? RefreshToken);

public sealed record VerifyEmailRequest(
    [Required] string UserId,
    [Required] string Token);

public sealed record EmailRequest([Required, EmailAddress] string Email);

public sealed record ResetPasswordRequest(
    [Required, EmailAddress] string Email,
    [Required] string Token,
    [Required, StringLength(128, MinimumLength = 8)] string NewPassword);

public sealed record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, StringLength(128, MinimumLength = 8)] string NewPassword);

public sealed record UserDto(
    string Id,
    string UserName,
    string Email,
    bool EmailConfirmed,
    string? DisplayName,
    string? PhoneNumber,
    IReadOnlyList<string> Roles,
    DateTime CreatedAt,
    DateTimeOffset? LockoutEnd);

public sealed record UpdateUserRequest(
    [StringLength(100)] string? DisplayName,
    [Phone, StringLength(30)] string? PhoneNumber);

public sealed record AssignRoleRequest([Required] string Role);

/// <summary>Запис історії refresh tokens — без значення й хешу.</summary>
public sealed record RefreshTokenDto(
    Guid Id,
    DateTime CreatedAt,
    string? CreatedByIp,
    DateTime ExpiresAt,
    DateTime? RevokedAt,
    string? RevokedByIp,
    string? ReasonRevoked,
    bool Replaced,
    string Status);
