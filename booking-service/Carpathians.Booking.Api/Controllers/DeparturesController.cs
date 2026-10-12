using Carpathians.Bll.Dtos;
using Carpathians.Bll.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Carpathians.Api.Controllers;

[ApiController]
[Route("api/departures")]
[Produces("application/json")]
public class DeparturesController(IDepartureService departures) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DepartureDto>>> GetAll(CancellationToken ct)
        => Ok(await departures.GetAllAsync(ct));

    [HttpGet("{id:long}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DepartureDto>> GetById(long id, CancellationToken ct)
        => Ok(await departures.GetByIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DepartureDto>> Create(CreateDepartureDto input, CancellationToken ct)
    {
        var created = await departures.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DepartureDto>> Update(
        long id, UpdateDepartureDto input, CancellationToken ct)
        => Ok(await departures.UpdateAsync(id, input, ct));

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await departures.DeleteAsync(id, ct);
        return NoContent();
    }
}