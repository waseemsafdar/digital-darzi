using API.Authorization;
using Application.Common;
using Application.Interfaces.Services;
using Application.ViewModels.Finance;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Route("api/expenses")]
[Authorize]
public class ExpensesController
    : BaseCrudController<ShopExpense, CreateShopExpenseViewModel, UpdateShopExpenseViewModel, ShopExpenseDetailViewModel, ShopExpenseListViewModel, ShopExpenseSearchModel>
{
    public ExpensesController(IShopExpenseService<CreateShopExpenseViewModel, UpdateShopExpenseViewModel, ShopExpenseDetailViewModel> service) : base(service) { }

    [HttpGet("{id}")]
    [HasPermission(Permissions.FinanceView)]
    public override Task<IActionResult> GetById(Guid id, CancellationToken ct) => base.GetById(id, ct);

    [HttpGet]
    [HasPermission(Permissions.FinanceView)]
    public override Task<IActionResult> GetAll([FromQuery] ShopExpenseSearchModel search, CancellationToken ct) => base.GetAll(search, ct);

    [HttpPost]
    [HasPermission(Permissions.FinanceManage)]
    public override async Task<IActionResult> Create([FromBody] CreateShopExpenseViewModel vm, CancellationToken ct)
    {
        var result = await _service.CreateAsync(vm, ct);
        return result.Success ? CreatedAtAction(nameof(GetById), new { id = result.Data }, result) : ReturnProcessedResponse(result);
    }

    [HttpPut]
    [HasPermission(Permissions.FinanceManage)]
    public override Task<IActionResult> Update([FromBody] UpdateShopExpenseViewModel vm, CancellationToken ct) => base.Update(vm, ct);

    [HttpDelete("{id}")]
    [HasPermission(Permissions.FinanceDelete)]
    public override Task<IActionResult> Delete(Guid id, CancellationToken ct) => base.Delete(id, ct);
}
