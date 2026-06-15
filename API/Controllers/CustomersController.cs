using Application.Interfaces.Services;
using Application.ViewModels.Customer;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Route("api/customers")]
[Authorize]
public class CustomersController
    : BaseCrudController<Customer, CreateCustomerViewModel, UpdateCustomerViewModel, CustomerDetailViewModel, CustomerListViewModel, CustomerSearchViewModel>
{
    private readonly ICustomerService<CreateCustomerViewModel, UpdateCustomerViewModel, CustomerDetailViewModel> _customerService;

    public CustomersController(ICustomerService<CreateCustomerViewModel, UpdateCustomerViewModel, CustomerDetailViewModel> service) : base(service)
    {
        _customerService = service;
    }

    // Search endpoint removed - handled by base GetAll

    [HttpGet("{id}/ledger")]
    public async Task<IActionResult> GetLedger(Guid id, CancellationToken ct)
        => ReturnProcessedResponse(await _customerService.GetLedgerAsync(id, ct));

    [HttpPost]
    public override async Task<IActionResult> Create([FromBody] CreateCustomerViewModel vm, CancellationToken ct)
    {
        var result = await _customerService.CreateAsync(vm, ct);
        return result.Success
            ? CreatedAtAction(nameof(GetById), new { id = result.Data }, result)
            : ReturnProcessedResponse(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Owner,Manager")]
    public override async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ReturnProcessedResponse(await _customerService.DeleteAsync(id, ct));
}
