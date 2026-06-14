using Application.Interfaces.Services;
using Application.ViewModels.Customer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _service;
    public CustomersController(ICustomerService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] CustomerSearchViewModel filter, CancellationToken ct)
        => Ok(await _service.SearchAsync(filter, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct)
    {
        var result = await _service.GetDetailAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpGet("{id:guid}/ledger")]
    public async Task<IActionResult> GetLedger(Guid id, CancellationToken ct)
    {
        var result = await _service.GetLedgerAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerViewModel vm, CancellationToken ct)
    {
        var result = await _service.CreateAsync(vm, ct);
        return result.Success ? CreatedAtAction(nameof(GetDetail), new { id = result.Data!.Id }, result) : BadRequest(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerViewModel vm, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, vm, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }
}
