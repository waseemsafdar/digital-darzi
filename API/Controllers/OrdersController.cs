using Application.Interfaces.Services;
using Application.ViewModels.Order;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController : BaseController
{
    private readonly IOrderService _service;
    public OrdersController(IOrderService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] OrderSearchViewModel filter, CancellationToken ct)
        => ReturnProcessedResponse(await _service.SearchAsync(filter, ct));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct)
        => ReturnProcessedResponse(await _service.GetDetailAsync(id, ct));

    [HttpGet("customer/{customerId}")]
    public async Task<IActionResult> GetByCustomer(Guid customerId, CancellationToken ct)
        => ReturnProcessedResponse(await _service.GetByCustomerAsync(customerId, ct));

    [HttpGet("due-today")]
    public async Task<IActionResult> GetDueToday(CancellationToken ct)
        => ReturnProcessedResponse(await _service.GetDueTodayAsync(ct));

    [HttpGet("overdue")]
    public async Task<IActionResult> GetOverdue(CancellationToken ct)
        => ReturnProcessedResponse(await _service.GetOverdueAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderViewModel vm, CancellationToken ct)
    {
        var result = await _service.CreateAsync(vm, ct);
        return result.Success
            ? CreatedAtAction(nameof(GetDetail), new { id = result.Data!.Id }, result)
            : ReturnProcessedResponse(result);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateOrderViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.UpdateAsync(vm, ct));

    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] OrderStatus status, CancellationToken ct)
        => ReturnProcessedResponse(await _service.UpdateStatusAsync(id, status, ct));

    [HttpPost("{id}/payments")]
    public async Task<IActionResult> RecordPayment(Guid id, [FromBody] RecordOrderPaymentViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.RecordPaymentAsync(id, vm, ct));

    [HttpPost("alterations")]
    public async Task<IActionResult> AddAlteration([FromBody] CreateOrderAlterationViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.AddAlterationAsync(vm, ct));

    [HttpPost("stages/log")]
    [Authorize(Roles = "Owner,Manager,Karigar")]
    public async Task<IActionResult> RecordStageLog([FromBody] RecordStageLogViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.RecordStageLogAsync(vm, ct));

    [HttpPost("stages/complete")]
    [Authorize(Roles = "Owner,Manager,Karigar")]
    public async Task<IActionResult> CompleteStage([FromBody] CompleteStageViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.CompleteStageAsync(vm, ct));

    [HttpPost("stages/assign")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<IActionResult> AssignStage([FromBody] AssignStageViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.AssignStageAsync(vm, ct));

    [HttpPost("{id}/cancel")]
    [Authorize(Roles = "Owner,Manager")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        => ReturnProcessedResponse(await _service.CancelOrderAsync(id, ct));

    [HttpPost("{id}/deliver")]
    [Authorize(Roles = "Owner,Manager,Receptionist")]
    public async Task<IActionResult> MarkDelivered(Guid id, CancellationToken ct)
        => ReturnProcessedResponse(await _service.MarkDeliveredAsync(id, ct));
}
