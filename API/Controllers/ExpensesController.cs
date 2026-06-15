using Application.Interfaces.Services;
using Application.ViewModels.Finance;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Route("api/expenses")]
[Authorize(Roles = "Owner,Manager")]
public class ExpensesController
    : BaseCrudController<ShopExpense, CreateShopExpenseViewModel, UpdateShopExpenseViewModel, ShopExpenseDetailViewModel, ShopExpenseListViewModel, ShopExpenseSearchModel>
{
    private readonly IShopExpenseService<CreateShopExpenseViewModel, UpdateShopExpenseViewModel, ShopExpenseDetailViewModel> _expenseService;

    public ExpensesController(IShopExpenseService<CreateShopExpenseViewModel, UpdateShopExpenseViewModel, ShopExpenseDetailViewModel> service) : base(service)
    {
        _expenseService = service;
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Manager")]
    public override async Task<IActionResult> Create([FromBody] CreateShopExpenseViewModel vm, CancellationToken ct)
    {
        var result = await _expenseService.CreateAsync(vm, ct);
        return result.Success
            ? CreatedAtAction(nameof(GetById), new { id = result.Data }, result)
            : ReturnProcessedResponse(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Owner")]
    public override async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ReturnProcessedResponse(await _expenseService.DeleteAsync(id, ct));
}
