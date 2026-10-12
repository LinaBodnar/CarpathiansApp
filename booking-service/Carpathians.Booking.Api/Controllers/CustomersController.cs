using Carpathians.Bll.Dtos;
using Carpathians.Bll.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Carpathians.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Produces("application/json")]
public class CustomersController(ICustomerService customers) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerDto>>> GetAll(CancellationToken ct)
        => Ok(await customers.GetAllAsync(ct));

    [HttpGet("{id:long}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDto>> GetById(long id, CancellationToken ct)
        => Ok(await customers.GetByIdAsync(id, ct));

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerDto>> Create(CustomerInputDto input, CancellationToken ct)
    {
        var created = await customers.CreateAsync(input, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerDto>> Update(
        long id, CustomerInputDto input, CancellationToken ct)
        => Ok(await customers.UpdateAsync(id, input, ct));

    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await customers.DeleteAsync(id, ct);
        return NoContent();
    }
}