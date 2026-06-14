using Application.Interfaces.Services;
using Application.ViewModels.Order;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _service;
    public OrdersController(IOrderService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] OrderSearchViewModel filter, CancellationToken ct)
        => Ok(await _service.SearchAsync(filter, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct)
    {
        var result = await _service.GetDetailAsync(id, ct);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpGet("customer/{customerId:guid}")]
    public async Task<IActionResult> GetByCustomer(Guid customerId, CancellationToken ct)
        => Ok(await _service.GetByCustomerAsync(customerId, ct));

    [HttpGet("due-today")]
    public async Task<IActionResult> GetDueToday(CancellationToken ct)
        => Ok(await _service.GetDueTodayAsync(ct));

    [HttpGet("overdue")]
    public async Task<IActionResult> GetOverdue(CancellationToken ct)
        => Ok(await _service.GetOverdueAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderViewModel vm, CancellationToken ct)
    {
        var result = await _service.CreateAsync(vm, ct);
        return result.Success ? CreatedAtAction(nameof(GetDetail), new { id = result.Data!.Id }, result) : BadRequest(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrderViewModel vm, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, vm, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] OrderStatus status, CancellationToken ct)
    {
        var result = await _service.UpdateStatusAsync(id, status, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:guid}/payments")]
    public async Task<IActionResult> RecordPayment(Guid id, [FromBody] RecordOrderPaymentViewModel vm, CancellationToken ct)
    {
        var result = await _service.RecordPaymentAsync(id, vm, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("alterations")]
    public async Task<IActionResult> AddAlteration([FromBody] CreateOrderAlterationViewModel vm, CancellationToken ct)
    {
        var result = await _service.AddAlterationAsync(vm, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("stages/log")]
    [Authorize(Roles = "Owner,Manager,Karigar")]
    public async Task<IActionResult> RecordStageLog([FromBody] RecordStageLogViewModel vm, CancellationToken ct)
    {
        var result = await _service.RecordStageLogAsync(vm, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("stages/complete")]
    [Authorize(Roles = "Owner,Manager,Karigar")]
    public async Task<IActionResult> CompleteStage([FromBody] CompleteStageViewModel vm, CancellationToken ct)
    {
        var result = await _service.CompleteStageAsync(vm, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("stages/assign")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<IActionResult> AssignStage([FromBody] AssignStageViewModel vm, CancellationToken ct)
    {
        var result = await _service.AssignStageAsync(vm, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var result = await _service.CancelOrderAsync(id, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:guid}/deliver")]
    [Authorize(Roles = "Owner,Manager,Receptionist")]
    public async Task<IActionResult> MarkDelivered(Guid id, CancellationToken ct)
    {
        var result = await _service.MarkDeliveredAsync(id, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
