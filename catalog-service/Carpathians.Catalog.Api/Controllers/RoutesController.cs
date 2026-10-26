using Carpathians.Bll.Dtos;
using Carpathians.Bll.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Carpathians.Catalog.Api.Controllers;

[ApiController]
[Route("api/routes")]
[Produces("application/json")]
public class RoutesController(IRouteService routes) : ControllerBase
{
    private readonly IRouteService _routes = routes;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<RouteDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<RouteDto>>> GetAll(
        [FromQuery] RouteQueryParameters queryParams,
        CancellationToken ct)
    {
        var result = await _routes.GetPagedAsync(queryParams, ct);

        Response.Headers.Append("X-Total-Count", result.TotalCount.ToString());
        Response.Headers.Append("X-Page", result.Page.ToString());
        Response.Headers.Append("X-Page-Size", result.PageSize.ToString());
        Response.Headers.Append("X-Total-Pages", result.TotalPages.ToString());

        return Ok(result);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteDto>> GetById(long id, CancellationToken ct)
        => Ok(await _routes.GetByIdAsync(id, ct));

    [HttpGet("{id:long}/explicit")]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteDto>> GetWithDetailsExplicit(long id, CancellationToken ct)
        => Ok(await _routes.GetWithDetailsExplicitAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RouteDto>> Create([FromBody] CreateRouteDto input, CancellationToken ct)
    {
        var created = await _routes.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RouteDto>> Update(long id, [FromBody] UpdateRouteDto input, CancellationToken ct)
        => Ok(await _routes.UpdateAsync(id, input, ct));

    [HttpPut("{id:long}/detail")]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RouteDto>> SetDetail(long id, [FromBody] SetRouteDetailDto input, CancellationToken ct)
        => Ok(await _routes.SetDetailAsync(id, input, ct));

    [HttpPost("{id:long}/gear")]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RouteDto>> AddGearItem(long id, [FromBody] AddGearItemDto input, CancellationToken ct)
        => Ok(await _routes.AddGearItemAsync(id, input, ct));

    [HttpDelete("{id:long}/gear/{gearItemId:long}")]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteDto>> RemoveGearItem(long id, long gearItemId, CancellationToken ct)
        => Ok(await _routes.RemoveGearItemAsync(id, gearItemId, ct));

    [HttpPost("{id:long}/points")]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RouteDto>> AddPoint(long id, [FromBody] AddRoutePointDto input, CancellationToken ct)
        => Ok(await _routes.AddPointAsync(id, input, ct));

    [HttpDelete("{id:long}/points/{pointId:long}")]
    [ProducesResponseType(typeof(RouteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteDto>> RemovePoint(long id, long pointId, CancellationToken ct)
        => Ok(await _routes.RemovePointAsync(id, pointId, ct));

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, [FromQuery] string? deletedBy, CancellationToken ct)
    {
        await _routes.DeleteAsync(id, deletedBy, ct);
        return NoContent();
    }
}
