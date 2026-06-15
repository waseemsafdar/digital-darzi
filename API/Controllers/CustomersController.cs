using Application.Interfaces.Services;
using Application.ViewModels.Customer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Route("api/customers")]
[Authorize]
public class CustomersController
    : BaseCrudController<CreateCustomerViewModel, UpdateCustomerViewModel, CustomerDetailViewModel>
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService service) : base(service)
    {
        _customerService = service;
    }

    /// <summary>Paginated search — overrides base GetAll.</summary>
    [HttpGet]
    public override async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => ReturnProcessedResponse(await _customerService.SearchAsync(
            new CustomerSearchViewModel { Page = page, PageSize = pageSize }, ct));

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] CustomerSearchViewModel filter, CancellationToken ct)
        => ReturnProcessedResponse(await _customerService.SearchAsync(filter, ct));

    [HttpGet("{id}/ledger")]
    public async Task<IActionResult> GetLedger(Guid id, CancellationToken ct)
        => ReturnProcessedResponse(await _customerService.GetLedgerAsync(id, ct));

    [HttpPost]
    public override async Task<IActionResult> Create([FromBody] CreateCustomerViewModel vm, CancellationToken ct)
    {
        var result = await _customerService.CreateAsync(vm, ct);
        return result.Success
            ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result)
            : ReturnProcessedResponse(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Owner,Manager")]
    public override async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ReturnProcessedResponse(await _customerService.DeleteAsync(id, ct));
}
