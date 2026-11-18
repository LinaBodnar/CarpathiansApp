using System.Text;
using Carpathians.Auth.Api.Contracts;
using Carpathians.Auth.Api.Data;
using Carpathians.Auth.Api.Services;
using Carpathians.Platform.ServiceDefaults.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Carpathians.Auth.Api.Controllers;

/// <summary>
/// Автентифікація: реєстрація, вхід, оновлення й відкликання токенів, підтвердження email, скидання й зміна пароля.
/// Access token (15 хв) повертається в тілі відповіді, refresh token (7 днів) — лише в HttpOnly cookie.
/// </summary>
[ApiController]
[Route("auth")]
[Produces("application/json")]
public class AuthController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    IJwtTokenService jwt,
    RefreshTokenService refreshTokens,
    IEmailSender email,
    IOptions<AuthOptions> authOptions,
    ILogger<AuthController> logger) : ControllerBase
{
    public const string RefreshTokenCookie = "refreshToken";
    private readonly AuthOptions _options = authOptions.Value;

    /// <summary>Реєстрація: роль User, лист підтвердження email. Вхід можливий лише після підтвердження.</summary>
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var user = new ApplicationUser
        {
            UserName = request.UserName,
            Email = request.Email,
            DisplayName = request.DisplayName
        };

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            return IdentityProblem(created);

        await users.AddToRoleAsync(user, PlatformRoles.User);
        await SendEmailConfirmationAsync(user, ct);

        logger.LogInformation("User {UserId} ({UserName}) registered, email confirmation sent", user.Id, user.UserName);
        return Created($"/users/{user.Id}", new RegisterResponse(user.Id, user.UserName!, user.Email!, EmailConfirmationRequired: true));
    }

    /// <summary>Вхід за ім'ям або email: access token у тілі, refresh token у cookie. 5 невдалих спроб — блокування.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status423Locked)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var user = request.Login.Contains('@')
            ? await users.FindByEmailAsync(request.Login)
            : await users.FindByNameAsync(request.Login);
        if (user is null)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");

        // RequireConfirmedEmail + lockoutOnFailure: непідтверджений email -> IsNotAllowed, 5 невдач -> IsLockedOut.
        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            logger.LogWarning("Login of locked out user {UserId}", user.Id);
            return Problem(statusCode: StatusCodes.Status423Locked, title: "Account is locked",
                detail: "Too many failed attempts. Try again later.");
        }
        if (result.IsNotAllowed)
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Email is not confirmed",
                detail: "Confirm your email (POST /auth/verify-email) or request a new link (POST /auth/resend-verification).");
        if (!result.Succeeded)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");

        logger.LogInformation("User {UserId} logged in", user.Id);
        return Ok(await IssueTokensAsync(user, ct));
    }

    /// <summary>Новий access token за refresh token (cookie або тіло). Ротація: старий refresh token відкликається.</summary>
    [AllowAnonymous]
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshTokenRequest? request, CancellationToken ct)
    {
        var token = request?.RefreshToken ?? Request.Cookies[RefreshTokenCookie];
        if (string.IsNullOrWhiteSpace(token))
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Refresh token is missing");

        var rotation = await refreshTokens.RotateAsync(token, ClientIp, ct);
        if (rotation is null)
        {
            Response.Cookies.Delete(RefreshTokenCookie, CookieOptions(DateTime.UnixEpoch));
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid refresh token",
                detail: "The token is unknown, expired or revoked. Log in again.");
        }

        var user = rotation.User;
        if (await users.IsLockedOutAsync(user))
            return Problem(statusCode: StatusCodes.Status423Locked, title: "Account is locked");

        SetRefreshCookie(rotation.NewRefreshToken, rotation.ExpiresAt);
        return Ok(await CreateTokenResponseAsync(user));
    }

    /// <summary>Відкликає refresh token (з тіла або cookie). Звичайний користувач — лише власний, Admin — будь-який.</summary>
    [Authorize]
    [HttpPost("revoke-token")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeToken(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshTokenRequest? request, CancellationToken ct)
    {
        var fromCookie = request?.RefreshToken is null;
        var token = request?.RefreshToken ?? Request.Cookies[RefreshTokenCookie];
        if (string.IsNullOrWhiteSpace(token))
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Refresh token is required");

        var ownerId = User.IsInRole(PlatformRoles.Admin) ? null : CurrentUserId;
        if (!await refreshTokens.RevokeAsync(token, ClientIp, "Revoked by user", ct, ownerId))
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Active refresh token not found");

        if (fromCookie)
            Response.Cookies.Delete(RefreshTokenCookie, CookieOptions(DateTime.UnixEpoch));
        return NoContent();
    }

    /// <summary>Вихід: відкликає refresh token поточної сесії й видаляє cookie. Access token живе до закінчення 15 хв.</summary>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies[RefreshTokenCookie] is { Length: > 0 } token)
            await refreshTokens.RevokeAsync(token, ClientIp, "Logout", ct, CurrentUserId);

        Response.Cookies.Delete(RefreshTokenCookie, CookieOptions(DateTime.UnixEpoch));
        logger.LogInformation("User {UserId} logged out", CurrentUserId);
        return NoContent();
    }

    /// <summary>Підтвердження email одноразовим токеном з листа.</summary>
    [AllowAnonymous]
    [HttpPost("verify-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request)
    {
        var user = await users.FindByIdAsync(request.UserId);
        if (user is null || user.EmailConfirmed)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid or already used token");

        var result = await users.ConfirmEmailAsync(user, DecodeToken(request.Token));
        if (!result.Succeeded)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid or expired token");

        // Новий security stamp анулює всі видані раніше токени (зокрема копії цього посилання).
        await users.UpdateSecurityStampAsync(user);
        logger.LogInformation("Email of user {UserId} confirmed", user.Id);
        return NoContent();
    }

    /// <summary>Повторний лист підтвердження. Відповідь однакова для будь-якої адреси (без розкриття облікових записів).</summary>
    [AllowAnonymous]
    [HttpPost("resend-verification")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ResendVerification(EmailRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is { EmailConfirmed: false })
            await SendEmailConfirmationAsync(user, ct);
        return Accepted();
    }

    /// <summary>Запит на скидання пароля: лист з одноразовим токеном. Відповідь однакова для будь-якої адреси.</summary>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> ForgotPassword(EmailRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is { EmailConfirmed: true })
        {
            var token = EncodeToken(await users.GeneratePasswordResetTokenAsync(user));
            var link = $"{_options.PublicBaseUrl}/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={token}";
            await email.SendAsync(user.Email!, "Carpathians: password reset",
                $"Use this token to reset your password (valid {_options.EmailTokenLifetimeHours} h): {token}", link, ct);
            logger.LogInformation("Password reset requested for user {UserId}", user.Id);
        }
        return Accepted();
    }

    /// <summary>Скидання пароля за токеном. Після успіху всі refresh tokens користувача відкликаються.</summary>
    [AllowAnonymous]
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid or expired token");

        // Токен скидання прив'язаний до security stamp: після успішного скидання stamp змінюється -> токен одноразовий.
        var result = await users.ResetPasswordAsync(user, DecodeToken(request.Token), request.NewPassword);
        if (!result.Succeeded)
            return IdentityProblem(result);

        await users.ResetAccessFailedCountAsync(user);
        var revoked = await refreshTokens.RevokeAllAsync(user.Id, ClientIp, "Password reset", ct);
        logger.LogInformation("Password of user {UserId} reset, {Revoked} refresh tokens revoked", user.Id, revoked);
        return NoContent();
    }

    /// <summary>Зміна пароля (поточний + новий). Інші сесії завершуються: refresh tokens відкликаються.</summary>
    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(CurrentUserId);
        if (user is null)
            return Unauthorized();

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return IdentityProblem(result);

        var revoked = await refreshTokens.RevokeAllAsync(user.Id, ClientIp, "Password changed", ct);
        Response.Cookies.Delete(RefreshTokenCookie, CookieOptions(DateTime.UnixEpoch));
        logger.LogInformation("User {UserId} changed password, {Revoked} refresh tokens revoked", user.Id, revoked);
        return NoContent();
    }

    /// <summary>Листи mock email service (лише Development): токени підтвердження й скидання для перевірки flows.</summary>
    [AllowAnonymous]
    [HttpGet("dev/emails")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult DevEmails([FromQuery] string to, [FromServices] IWebHostEnvironment environment)
        => environment.IsDevelopment() && email is DevelopmentEmailSender sender
            ? Ok(sender.For(to))
            : NotFound();

    // ---------- Допоміжне ----------

    private async Task<TokenResponse> IssueTokensAsync(ApplicationUser user, CancellationToken ct)
    {
        var (refresh, expiresAt) = await refreshTokens.IssueAsync(user, ClientIp, ct);
        SetRefreshCookie(refresh, expiresAt);
        return await CreateTokenResponseAsync(user);
    }

    private async Task<TokenResponse> CreateTokenResponseAsync(ApplicationUser user)
    {
        // Ролі читаються з БД при кожній видачі: зміна ролей діє з наступного refresh.
        var roles = (await users.GetRolesAsync(user)).ToList();
        var access = jwt.GenerateAccessToken(user, roles);
        return new TokenResponse(access.Token, "Bearer", access.ExpiresAt,
            (int)(access.ExpiresAt - DateTime.UtcNow).TotalSeconds, UsersController.ToDto(user, roles));
    }

    private async Task SendEmailConfirmationAsync(ApplicationUser user, CancellationToken ct)
    {
        var token = EncodeToken(await users.GenerateEmailConfirmationTokenAsync(user));
        var link = $"{_options.PublicBaseUrl}/verify-email?userId={user.Id}&token={token}";
        await email.SendAsync(user.Email!, "Carpathians: confirm your email",
            $"Confirm your email with POST /auth/verify-email {{ \"userId\": \"{user.Id}\", \"token\": \"{token}\" }} " +
            $"(valid {_options.EmailTokenLifetimeHours} h).", link, ct);
    }

    /// <summary>HttpOnly cookie: недоступна JavaScript (захист від XSS); SameSite=Strict; лише шлях /auth.</summary>
    private void SetRefreshCookie(string token, DateTime expiresAt)
        => Response.Cookies.Append(RefreshTokenCookie, token, CookieOptions(expiresAt));

    private CookieOptions CookieOptions(DateTime expiresAt) => new()
    {
        HttpOnly = true,
        Secure = Request.IsHttps || _options.RequireSecureCookie,
        SameSite = SameSiteMode.Strict,
        Path = "/auth",
        Expires = expiresAt
    };

    private IActionResult IdentityProblem(IdentityResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError(error.Code, error.Description);
        return ValidationProblem(ModelState);
    }

    // Токени Identity містять символи '+', '/', '=' — у посиланнях і JSON передаються як base64url.
    private static string EncodeToken(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    private static string DecodeToken(string token)
    {
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch (FormatException)
        {
            return token;
        }
    }

    private string CurrentUserId => User.FindFirst(PlatformClaimTypes.Subject)?.Value ?? string.Empty;

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();
}

/// <summary>Налаштування Auth Service (секція "Auth").</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Термін дії токенів підтвердження email і скидання пароля.</summary>
    public int EmailTokenLifetimeHours { get; set; } = 3;

    /// <summary>Адреса клієнтського застосунку для посилань у листах.</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:5000";

    /// <summary>Secure-cookie навіть для HTTP (production за TLS-термінацією).</summary>
    public bool RequireSecureCookie { get; set; }

    public AdminOptions Admin { get; set; } = new();

    public sealed class AdminOptions
    {
        public string UserName { get; set; } = "admin";
        public string Email { get; set; } = "admin@carpathians.local";
        public string? Password { get; set; }
    }
}
