using API.Authorization;
using Application.Common;
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
        => _customerService = service;

    [HttpGet("{id}")]
    [HasPermission(Permissions.CustomersView)]
    public override Task<IActionResult> GetById(Guid id, CancellationToken ct) => base.GetById(id, ct);

    /// <summary>Paged list. Filters: Query (name/phone/WhatsApp), City, Gender, CustomerType, HasBalance, ActiveStatus; SortBy name|totalSpend|createdOn.</summary>
    [HttpGet]
    [HasPermission(Permissions.CustomersView)]
    public override Task<IActionResult> GetAll([FromQuery] CustomerSearchViewModel search, CancellationToken ct) => base.GetAll(search, ct);

    /// <summary>Running account: order charges, alterations, payments, refunds and cancellations with a running balance.</summary>
    [HttpGet("{id}/ledger")]
    [HasPermission(Permissions.FinanceView)]
    public async Task<IActionResult> GetLedger(Guid id, CancellationToken ct)
        => ReturnProcessedResponse(await _customerService.GetLedgerAsync(id, ct));

    [HttpPost]
    [HasPermission(Permissions.CustomersEdit)]
    public override async Task<IActionResult> Create([FromBody] CreateCustomerViewModel vm, CancellationToken ct)
    {
        var result = await _customerService.CreateAsync(vm, ct);
        return result.Success ? CreatedAtAction(nameof(GetById), new { id = result.Data }, result) : ReturnProcessedResponse(result);
    }

    [HttpPut]
    [HasPermission(Permissions.CustomersEdit)]
    public override Task<IActionResult> Update([FromBody] UpdateCustomerViewModel vm, CancellationToken ct) => base.Update(vm, ct);

    [HttpDelete("{id}")]
    [HasPermission(Permissions.CustomersDelete)]
    public override Task<IActionResult> Delete(Guid id, CancellationToken ct) => base.Delete(id, ct);
}
