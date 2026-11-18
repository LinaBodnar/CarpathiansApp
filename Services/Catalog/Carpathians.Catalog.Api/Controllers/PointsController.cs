using Carpathians.Bll.Dtos;
using Carpathians.Bll.Interfaces;
using Carpathians.Platform.ServiceDefaults.Security;
using Carpathians.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Carpathians.Catalog.Api.Controllers;

[ApiController]
[Route("api/points")]
[Authorize]   // Лаб. 09: лише з дійсним JWT; 401 без токена
[Produces("application/json")]
public class PointsController(IPointService points) : ControllerBase
{
    private readonly IPointService _points = points;

    [HttpGet]
    [RequirePermission(Permissions.Routes.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<MapPointDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MapPointDto>>> GetAll(CancellationToken ct)
        => Ok(await _points.GetAllAsync(ct));

    [HttpGet("{id:long}")]
    [RequirePermission(Permissions.Routes.Read)]
    [ProducesResponseType(typeof(MapPointDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MapPointDto>> GetById(long id, CancellationToken ct)
        => Ok(await _points.GetByIdAsync(id, ct));

    [HttpGet("type/{pointType}")]
    [RequirePermission(Permissions.Routes.Read)]
    [ProducesResponseType(typeof(IReadOnlyList<MapPointDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MapPointDto>>> GetByType(PointType pointType, CancellationToken ct)
        => Ok(await _points.GetByTypeAsync(pointType, ct));

    [HttpPost]
    [RequirePermission(Permissions.Routes.Create)]
    [ProducesResponseType(typeof(MapPointDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MapPointDto>> Create([FromBody] CreatePointDto input, CancellationToken ct)
    {
        var created = await _points.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    [RequirePermission(Permissions.Routes.Update)]
    [ProducesResponseType(typeof(MapPointDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MapPointDto>> Update(long id, [FromBody] UpdatePointDto input, CancellationToken ct)
        => Ok(await _points.UpdateAsync(id, input, ct));

    [HttpPost("{id:long}/images")]
    [RequirePermission(Permissions.Routes.Update)]
    [ProducesResponseType(typeof(MapPointDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MapPointDto>> AddImage(long id, [FromBody] AddGalleryImageDto input, CancellationToken ct)
        => Ok(await _points.AddImageAsync(id, input, ct));

    [HttpDelete("{id:long}/images/{imageId:long}")]
    [RequirePermission(Permissions.Routes.Update)]
    [ProducesResponseType(typeof(MapPointDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MapPointDto>> RemoveImage(long id, long imageId, CancellationToken ct)
        => Ok(await _points.RemoveImageAsync(id, imageId, ct));

    [HttpDelete("{id:long}")]
    [RequirePermission(Permissions.Routes.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await _points.DeleteAsync(id, ct);
        return NoContent();
    }
}
