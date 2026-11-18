using Carpathians.Bll.Dtos;
using Carpathians.Bll.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Carpathians.Catalog.Api.Controllers;

[ApiController]
[Route("api/regions")]
[Produces("application/json")]
public class RegionsController(IRegionService regions) : ControllerBase
{
    private readonly IRegionService _regions = regions;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RegionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RegionDto>>> GetAll(CancellationToken ct)
        => Ok(await _regions.GetAllAsync(ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(RegionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RegionDto>> GetById(int id, CancellationToken ct)
        => Ok(await _regions.GetByIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(typeof(RegionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegionDto>> Create([FromBody] CreateRegionDto input, CancellationToken ct)
    {
        var created = await _regions.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(RegionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegionDto>> Update(int id, [FromBody] UpdateRegionDto input, CancellationToken ct)
        => Ok(await _regions.UpdateAsync(id, input, ct));

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _regions.DeleteAsync(id, ct);
        return NoContent();
    }
}
