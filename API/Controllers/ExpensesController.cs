using Application.Interfaces.Services;
using Application.ViewModels.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[Route("api/expenses")]
[Authorize(Roles = "Owner,Manager")]
public class ExpensesController
    : BaseCrudController<CreateShopExpenseViewModel, UpdateShopExpenseViewModel, ShopExpenseDetailViewModel>
{
    private readonly IShopExpenseService _expenseService;

    public ExpensesController(IShopExpenseService service) : base(service)
    {
        _expenseService = service;
    }

    /// <summary>Get paginated expenses with optional date/category filters.</summary>
    [HttpGet]
    public override async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => ReturnProcessedResponse(await _expenseService.GetFilteredAsync(null, null, null, page, pageSize, ct));

    [HttpGet("filter")]
    public async Task<IActionResult> GetFiltered(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
        => ReturnProcessedResponse(await _expenseService.GetFilteredAsync(from, to, category, page, pageSize, ct));

    [HttpPost]
    [Authorize(Roles = "Owner,Manager")]
    public override async Task<IActionResult> Create([FromBody] CreateShopExpenseViewModel vm, CancellationToken ct)
    {
        var result = await _expenseService.CreateAsync(vm, ct);
        return result.Success
            ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result)
            : ReturnProcessedResponse(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Owner")]
    public override async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ReturnProcessedResponse(await _expenseService.DeleteAsync(id, ct));
}
