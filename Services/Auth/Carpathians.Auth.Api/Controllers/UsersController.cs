using Carpathians.Auth.Api.Contracts;
using Carpathians.Auth.Api.Data;
using Carpathians.Auth.Api.Services;
using Carpathians.Platform.ServiceDefaults.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Carpathians.Auth.Api.Controllers;

/// <summary>
/// Керування користувачами й ролями. Admin-endpoints — [Authorize(Roles = "Admin")];
/// owner-endpoints дозволені власнику (userId з токена = {id}) або Admin.
/// </summary>
[ApiController]
[Route("users")]
[Authorize]
[Produces("application/json")]
public class UsersController(
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole> roles,
    RefreshTokenService refreshTokens,
    TimeProvider time,
    ILogger<UsersController> logger) : ControllerBase
{
    /// <summary>Список користувачів (Admin).</summary>
    [HttpGet]
    [Authorize(Roles = PlatformRoles.Admin)]
    [ProducesResponseType(typeof(IReadOnlyList<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var list = await users.Users.AsNoTracking()
            .OrderBy(u => u.UserName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var result = new List<UserDto>(list.Count);
        foreach (var user in list)
            result.Add(ToDto(user, await users.GetRolesAsync(user)));

        Response.Headers.Append("X-Total-Count", (await users.Users.CountAsync(ct)).ToString());
        return Ok(result);
    }

    /// <summary>Дані користувача (власник або Admin).</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id)
    {
        if (!IsOwnerOrAdmin(id))
            return Forbid();

        var user = await users.FindByIdAsync(id);
        return user is null ? NotFound() : Ok(ToDto(user, await users.GetRolesAsync(user)));
    }

    /// <summary>Оновлення профілю (власник або Admin).</summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, UpdateUserRequest request)
    {
        if (!IsOwnerOrAdmin(id))
            return Forbid();

        var user = await users.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        user.DisplayName = request.DisplayName;
        user.PhoneNumber = request.PhoneNumber;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
            return IdentityProblem(result);

        return Ok(ToDto(user, await users.GetRolesAsync(user)));
    }

    /// <summary>Видалення користувача (Admin). Власний обліковий запис адміністратор так не видаляє.</summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = PlatformRoles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string id)
    {
        if (id == CurrentUserId)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "An administrator cannot delete their own account");

        var user = await users.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        var result = await users.DeleteAsync(user);          // RefreshTokens видаляються каскадно
        if (!result.Succeeded)
            return IdentityProblem(result);

        logger.LogInformation("User {UserId} deleted by {AdminId}", id, CurrentUserId);
        return NoContent();
    }

    /// <summary>Історія refresh tokens: створення, закінчення, відкликання й причина (власник або Admin).</summary>
    [HttpGet("{id}/refresh-tokens")]
    [ProducesResponseType(typeof(IReadOnlyList<RefreshTokenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRefreshTokens(string id, CancellationToken ct)
    {
        if (!IsOwnerOrAdmin(id))
            return Forbid();

        var now = time.GetUtcNow().UtcDateTime;
        var history = await refreshTokens.HistoryAsync(id, ct);
        return Ok(history.Select(t => new RefreshTokenDto(
            t.Id, t.CreatedAt, t.CreatedByIp, t.ExpiresAt, t.RevokedAt, t.RevokedByIp, t.ReasonRevoked,
            Replaced: t.ReplacedByTokenHash is not null,
            Status: t.IsRevoked ? "revoked" : t.IsExpired(now) ? "expired" : "active")));
    }

    /// <summary>Ролі користувача (будь-який автентифікований користувач).</summary>
    [HttpGet("{id}/roles")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRoles(string id)
    {
        var user = await users.FindByIdAsync(id);
        return user is null ? NotFound() : Ok(await users.GetRolesAsync(user));
    }

    /// <summary>Призначення ролі (Admin). Нова роль з'явиться в access token після наступного refresh.</summary>
    [HttpPost("{id}/roles")]
    [Authorize(Roles = PlatformRoles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignRole(string id, AssignRoleRequest request)
    {
        if (!await roles.RoleExistsAsync(request.Role))
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: $"Role '{request.Role}' does not exist",
                detail: $"Available roles: {string.Join(", ", PlatformRoles.All)}");

        var user = await users.FindByIdAsync(id);
        if (user is null)
            return NotFound();

        if (!await users.IsInRoleAsync(user, request.Role))
        {
            var result = await users.AddToRoleAsync(user, request.Role);
            if (!result.Succeeded)
                return IdentityProblem(result);
            logger.LogInformation("Role {Role} assigned to user {UserId} by {AdminId}", request.Role, id, CurrentUserId);
        }
        return NoContent();
    }

    /// <summary>Видалення ролі (Admin). Refresh tokens відкликаються — повноваження зменшуються одразу після 15 хв.</summary>
    [HttpDelete("{id}/roles/{role}")]
    [Authorize(Roles = PlatformRoles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveRole(string id, string role, CancellationToken ct)
    {
        if (id == CurrentUserId && role == PlatformRoles.Admin)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "An administrator cannot remove their own Admin role");

        var user = await users.FindByIdAsync(id);
        if (user is null || !await users.IsInRoleAsync(user, role))
            return NotFound();

        var result = await users.RemoveFromRoleAsync(user, role);
        if (!result.Succeeded)
            return IdentityProblem(result);

        await refreshTokens.RevokeAllAsync(id, HttpContext.Connection.RemoteIpAddress?.ToString(), $"Role {role} removed", ct);
        logger.LogInformation("Role {Role} removed from user {UserId} by {AdminId}", role, id, CurrentUserId);
        return NoContent();
    }

    public static UserDto ToDto(ApplicationUser user, IEnumerable<string> userRoles) => new(
        user.Id, user.UserName!, user.Email ?? string.Empty, user.EmailConfirmed, user.DisplayName, user.PhoneNumber,
        userRoles.ToList(), user.CreatedAt, user.LockoutEnd);

    private bool IsOwnerOrAdmin(string id) => id == CurrentUserId || User.IsInRole(PlatformRoles.Admin);

    private string CurrentUserId => User.FindFirst(PlatformClaimTypes.Subject)?.Value ?? string.Empty;

    private IActionResult IdentityProblem(IdentityResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError(error.Code, error.Description);
        return ValidationProblem(ModelState);
    }
}
