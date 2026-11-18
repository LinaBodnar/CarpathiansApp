using Carpathians.Bll.Dtos;
using Carpathians.Bll.Interfaces;
using Carpathians.Platform.ServiceDefaults.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Carpathians.Catalog.Api.Controllers;

[ApiController]
[Route("api/routes")]
[Authorize]   // Лаб. 09: лише з дійсним JWT; 401 без токена
[Produces("application/json")]
public class RoutesController(IRouteService routes, ILogger<RoutesController> logger) : ControllerBase
{
    private readonly IRouteService _routes = routes;
    private readonly ILogger<RoutesController> _logger = logger;

    [HttpGet]
    [RequirePermission(Permissions.Routes.Read)]
    [ProducesResponseType(typeof(PagedResult<RouteDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<RouteDto>>> GetAll(
        [FromQuery] RouteQueryParameters queryParams,
        CancellationToken ct)
    {
        _logger.LogInformation("Listing routes with page {Page}, pageSize {PageSize}, search {Search}",
            queryParams.Page, queryParams.PageSize, queryParams.Search);

        var result = await _routes.GetPagedAsync(queryParams, ct);

        Response.Headers.Append("X-Total-Count", result.TotalCount.ToString());
        Response.Headers.Append("X-Page", result.Page.ToString());
        Response.Headers.Append("X-Page-Size", result.PageSize.ToString());
        Response.Headers.Append("X-Total-Pages", result.TotalPages.ToString());

        return Ok(result);
    }

    [HttpGet("popular-challenging")]
    [RequirePermission(Permissions.Routes.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<RouteDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RouteDto>>> GetPopularChallenging(CancellationToken ct)
    {
        _logger.LogInformation("Retrieving popular challenging routes via specification");
        return Ok(await _routes.GetPopularChallengingAsync(ct));
    }

    [HttpGet("{id:long}")]
    [RequirePermission(Permissions.Routes.Read)]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteDto>> GetById(long id, CancellationToken ct)
    {
        _logger.LogInformation("Retrieving route with ID {Id} (eager loading)", id);
        return Ok(await _routes.GetByIdAsync(id, ct));
    }

    [HttpGet("{id:long}/explicit")]
    [RequirePermission(Permissions.Routes.Read)]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteDto>> GetWithDetailsExplicit(long id, CancellationToken ct)
    {
        _logger.LogInformation("Retrieving route with ID {Id} (explicit loading)", id);
        return Ok(await _routes.GetWithDetailsExplicitAsync(id, ct));
    }

    [HttpPost]
    [RequirePermission(Permissions.Routes.Create)]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RouteDto>> Create([FromBody] CreateRouteDto input, CancellationToken ct)
    {
        _logger.LogInformation("Creating new route with slug {Slug} in region {RegionId}", input.Slug, input.RegionId);
        var created = await _routes.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    [RequirePermission(Permissions.Routes.Update)]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RouteDto>> Update(long id, [FromBody] UpdateRouteDto input, CancellationToken ct)
    {
        _logger.LogInformation("Updating route with ID {Id}", id);
        return Ok(await _routes.UpdateAsync(id, input, ct));
    }

    [HttpPut("{id:long}/detail")]
    [RequirePermission(Permissions.Routes.Update)]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RouteDto>> SetDetail(long id, [FromBody] SetRouteDetailDto input, CancellationToken ct)
    {
        _logger.LogInformation("Setting detail for route with ID {Id}", id);
        return Ok(await _routes.SetDetailAsync(id, input, ct));
    }

    [HttpPost("{id:long}/gear")]
    [RequirePermission(Permissions.Routes.Update)]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RouteDto>> AddGearItem(long id, [FromBody] AddGearItemDto input, CancellationToken ct)
    {
        _logger.LogInformation("Adding gear item {GearName} to route {Id}", input.Name, id);
        return Ok(await _routes.AddGearItemAsync(id, input, ct));
    }

    [HttpDelete("{id:long}/gear/{gearItemId:long}")]
    [RequirePermission(Permissions.Routes.Update)]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteDto>> RemoveGearItem(long id, long gearItemId, CancellationToken ct)
    {
        _logger.LogInformation("Removing gear item {GearItemId} from route {Id}", gearItemId, id);
        return Ok(await _routes.RemoveGearItemAsync(id, gearItemId, ct));
    }

    [HttpPost("{id:long}/points")]
    [RequirePermission(Permissions.Routes.Update)]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RouteDto>> AddPoint(long id, [FromBody] AddRoutePointDto input, CancellationToken ct)
    {
        _logger.LogInformation("Adding point {PointId} with sequence {SequenceNo} to route {Id}", input.PointId, input.SequenceNo, id);
        return Ok(await _routes.AddPointAsync(id, input, ct));
    }

    [HttpDelete("{id:long}/points/{pointId:long}")]
    [RequirePermission(Permissions.Routes.Update)]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteDto>> RemovePoint(long id, long pointId, CancellationToken ct)
    {
        _logger.LogInformation("Removing point {PointId} from route {Id}", pointId, id);
        return Ok(await _routes.RemovePointAsync(id, pointId, ct));
    }

    [HttpDelete("{id:long}")]
    [RequirePermission(Permissions.Routes.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, [FromQuery] string? deletedBy, CancellationToken ct)
    {
        // Лаб. 09: автор видалення — ім'я з перевіреного токена (параметр запиту — лише для запуску без токена).
        var by = User.Identity?.Name ?? deletedBy;
        _logger.LogInformation("Deleting route with ID {Id} by {DeletedBy}", id, by);
        await _routes.DeleteAsync(id, by, ct);
        return NoContent();
    }

    /// <summary>Публікація маршруту (Лаб. 09): manager і admin. Повторна публікація — 409.</summary>
    [HttpPost("{id:long}/publish")]
    [RequirePermission(Permissions.Routes.Publish)]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RouteDto>> Publish(long id, CancellationToken ct)
    {
        var published = await _routes.PublishAsync(id, User.Identity?.Name, ct);
        _logger.LogInformation("Route {Id} published by {UserName}", id, User.Identity?.Name);
        return Ok(published);
    }
}
