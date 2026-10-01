using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using API.Authorization;
using Application.Common;
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
    private readonly IOrderService _orders;
    private readonly IOrderWorkflowService _workflow;
    private readonly IOrderPaymentService _payments;

    public OrdersController(IOrderService orders, IOrderWorkflowService workflow, IOrderPaymentService payments)
    {
        _orders = orders;
        _workflow = workflow;
        _payments = payments;
    }

    // ── Reads ────────────────────────────────────────────────────────────────

    /// <summary>Paged search: text (order no. / customer name / phone), status, payment status, priority, karigar, garment, dates, overdue.</summary>
    [HttpGet]
    [HasPermission(Permissions.OrdersList)]
    public async Task<IActionResult> Search([FromQuery] OrderSearchViewModel filter, CancellationToken ct)
        => ReturnProcessedResponse(await _orders.SearchAsync(filter, ct));

    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.OrdersView)]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct)
        => ReturnProcessedResponse(await _orders.GetDetailAsync(id, ct));

    [HttpGet("{id:guid}/history")]
    [HasPermission(Permissions.OrdersView)]
    public async Task<IActionResult> GetHistory(Guid id, CancellationToken ct)
        => ReturnProcessedResponse(await _orders.GetHistoryAsync(id, ct));

    [HttpGet("customer/{customerId:guid}")]
    [HasPermission(Permissions.OrdersList)]
    public async Task<IActionResult> GetByCustomer(Guid customerId, CancellationToken ct)
        => ReturnProcessedResponse(await _orders.GetByCustomerAsync(customerId, ct));

    [HttpGet("due-today")]
    [HasPermission(Permissions.OrdersList)]
    public async Task<IActionResult> GetDueToday(CancellationToken ct)
        => ReturnProcessedResponse(await _orders.GetDueTodayAsync(ct));

    [HttpGet("overdue")]
    [HasPermission(Permissions.OrdersList)]
    public async Task<IActionResult> GetOverdue(CancellationToken ct)
        => ReturnProcessedResponse(await _orders.GetOverdueAsync(ct));

    /// <summary>Delivery board: due today, due tomorrow, overdue, ready for pickup, delivered today.</summary>
    [HttpGet("board")]
    [HasPermission(Permissions.OrdersList)]
    public async Task<IActionResult> GetBoard([FromQuery] int perBucket = 20, CancellationToken ct = default)
        => ReturnProcessedResponse(await _orders.GetBoardAsync(perBucket, ct));

    /// <summary>Receipt as JSON, or a printable page with <c>?format=html</c> (80 mm thermal friendly).</summary>
    [HttpGet("{id:guid}/receipt")]
    [HasPermission(Permissions.OrdersView)]
    public async Task<IActionResult> GetReceipt(Guid id, [FromQuery] string? format, CancellationToken ct)
    {
        var result = await _orders.GetReceiptAsync(id, ct);
        if (!result.Success || !string.Equals(format, "html", StringComparison.OrdinalIgnoreCase))
            return ReturnProcessedResponse(result);
        return Content(ReceiptHtml.Render(result.Data!), "text/html", Encoding.UTF8);
    }

    // ── Create / edit ────────────────────────────────────────────────────────

    [HttpPost]
    [HasPermission(Permissions.OrdersCreate)]
    public async Task<IActionResult> Create([FromBody] CreateOrderViewModel vm, CancellationToken ct)
    {
        var result = await _orders.CreateAsync(vm, ct);
        return result.Success ? CreatedAtAction(nameof(GetDetail), new { id = result.Data!.Id }, result) : ReturnProcessedResponse(result);
    }

    [HttpPut]
    [HasPermission(Permissions.OrdersEdit)]
    public async Task<IActionResult> Update([FromBody] UpdateOrderViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _orders.UpdateAsync(vm, ct));

    /// <summary>Creates a new order with the same garments, prices and style (measurements from the latest profiles by default).</summary>
    [HttpPost("{id:guid}/reorder")]
    [HasPermission(Permissions.OrdersCreate)]
    public async Task<IActionResult> Reorder(Guid id, [FromBody] ReorderViewModel vm, CancellationToken ct)
    {
        var result = await _orders.ReorderAsync(id, vm, ct);
        return result.Success ? CreatedAtAction(nameof(GetDetail), new { id = result.Data!.Id }, result) : ReturnProcessedResponse(result);
    }

    // ── Status ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Change status. Body is either the bare status number (legacy) or
    /// <c>{ "status": 3, "notes": "...", "force": false }</c>. Backward moves need notes;
    /// <c>force</c> overrides the workflow and is recorded as a manual override.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [HasPermission(Permissions.OrdersStatus)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] JsonElement body, CancellationToken ct)
    {
        ChangeOrderStatusViewModel? vm;
        try
        {
            vm = body.ValueKind switch
            {
                JsonValueKind.Number => new ChangeOrderStatusViewModel { Status = (OrderStatus)body.GetInt32() },
                JsonValueKind.String when Enum.TryParse<OrderStatus>(body.GetString(), true, out var s) => new ChangeOrderStatusViewModel { Status = s },
                JsonValueKind.Object => body.Deserialize<ChangeOrderStatusViewModel>(new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                _ => null
            };
        }
        catch (Exception e) when (e is JsonException or FormatException or InvalidOperationException)
        {
            vm = null;
        }
        if (vm == null || !Enum.IsDefined(vm.Status))
            return BadRequest(ApiResponse<object>.Fail("Send a status value, or { \"status\": ..., \"notes\": ..., \"force\": false }."));

        return ReturnProcessedResponse(await _workflow.ChangeStatusAsync(id, vm, ct));
    }

    [HttpPost("{id:guid}/cancel")]
    [HasPermission(Permissions.OrdersCancel)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] CancelOrderViewModel? vm, CancellationToken ct)
        => ReturnProcessedResponse(await _workflow.CancelAsync(id, vm ?? new CancelOrderViewModel(), ct));

    /// <summary>Hand over all (or selected) items. A balance due blocks delivery unless an Owner/Manager passes allowBalance with a note.</summary>
    [HttpPost("{id:guid}/deliver")]
    [HasPermission(Permissions.OrdersDeliver)]
    public async Task<IActionResult> MarkDelivered(Guid id, [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] DeliverOrderViewModel? vm, CancellationToken ct)
        => ReturnProcessedResponse(await _workflow.DeliverAsync(id, vm ?? new DeliverOrderViewModel(), ct));

    [HttpPost("{id:guid}/trial-result")]
    [HasPermission(Permissions.AlterationsManage)]
    public async Task<IActionResult> RecordTrialResult(Guid id, [FromBody] TrialResultViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _workflow.RecordTrialResultAsync(id, vm, ct));

    // ── Money ────────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/payments")]
    [HasPermission(Permissions.PaymentsRecord)]
    public async Task<IActionResult> RecordPayment(Guid id, [FromBody] RecordOrderPaymentViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _payments.RecordPaymentAsync(id, vm, ct));

    [HttpPost("{id:guid}/refunds")]
    [HasPermission(Permissions.PaymentsRefund)]
    public async Task<IActionResult> RecordRefund(Guid id, [FromBody] RecordRefundViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _payments.RecordRefundAsync(id, vm, ct));

    // ── Alterations ──────────────────────────────────────────────────────────

    [HttpPost("alterations")]
    [HasPermission(Permissions.AlterationsManage)]
    public async Task<IActionResult> AddAlteration([FromBody] CreateOrderAlterationViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _workflow.AddAlterationAsync(vm, ct));

    [HttpGet("{id:guid}/alterations")]
    [HasPermission(Permissions.OrdersView)]
    public async Task<IActionResult> GetAlterations(Guid id, CancellationToken ct)
        => ReturnProcessedResponse(await _workflow.GetAlterationsAsync(id, ct));

    /// <summary>Move an alteration Received → InProgress → Done → Delivered (or Cancelled), reassign, add notes.</summary>
    [HttpPatch("alterations/{alterationId:guid}")]
    [HasPermission(Permissions.AlterationsManage)]
    public async Task<IActionResult> UpdateAlteration(Guid alterationId, [FromBody] UpdateAlterationViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _workflow.UpdateAlterationAsync(alterationId, vm, ct));

    // ── Production stages ────────────────────────────────────────────────────

    [HttpPost("stages/assign")]
    [HasPermission(Permissions.ProductionAssign)]
    public async Task<IActionResult> AssignStage([FromBody] AssignStageViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _workflow.AssignStageAsync(vm, ct));

    /// <summary>Start work on a stage. A karigar can only start their own work.</summary>
    [HttpPost("stages/log")]
    [HasPermission(Permissions.ProductionWork)]
    public async Task<IActionResult> RecordStageLog([FromBody] RecordStageLogViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _workflow.RecordStageLogAsync(vm, ct));

    /// <summary>Finish a stage (cutting can include fabric used, cut quantity, defects, recut). Credits the karigar's ledger.</summary>
    [HttpPost("stages/complete")]
    [HasPermission(Permissions.ProductionWork)]
    public async Task<IActionResult> CompleteStage([FromBody] CompleteStageViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _workflow.CompleteStageAsync(vm, ct));

    /// <summary>Quality check: send completed work back for rework, optionally deducting from the karigar.</summary>
    [HttpPost("stages/reject")]
    [HasPermission(Permissions.ProductionQc)]
    public async Task<IActionResult> RejectStage([FromBody] RejectStageViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _workflow.RejectStageAsync(vm, ct));
}

/// <summary>Minimal printable receipt. Every value is HTML-encoded.</summary>
internal static class ReceiptHtml
{
    public static string Render(OrderReceiptViewModel r)
    {
        var e = HtmlEncoder.Default;
        string Money(decimal v) => e.Encode($"{r.CurrencySymbol} {v:N0}");
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html><head><meta charset=\"utf-8\"><title>Receipt ").Append(e.Encode(r.OrderNumber)).Append("</title>");
        sb.Append("<style>body{font-family:monospace;width:72mm;margin:0 auto;font-size:12px}h1{font-size:16px;text-align:center;margin:4px 0}")
          .Append(".c{text-align:center}table{width:100%;border-collapse:collapse}td{padding:2px 0}.r{text-align:right}")
          .Append("hr{border:0;border-top:1px dashed #000}.b{font-weight:bold}@media print{button{display:none}}</style></head><body>");
        sb.Append("<h1>").Append(e.Encode(r.ShopName)).Append("</h1>");
        if (!string.IsNullOrWhiteSpace(r.ShopPhone)) sb.Append("<div class=\"c\">").Append(e.Encode(r.ShopPhone)).Append("</div>");
        sb.Append("<hr><div>Order: <b>").Append(e.Encode(r.OrderNumber)).Append("</b></div>");
        sb.Append("<div>Date: ").Append(e.Encode(r.OrderDate.ToString("dd MMM yyyy"))).Append("</div>");
        sb.Append("<div>Delivery: ").Append(e.Encode(r.DeliveryDate.ToString("dd MMM yyyy"))).Append("</div>");
        sb.Append("<div>Customer: ").Append(e.Encode(r.CustomerName)).Append(" (").Append(e.Encode(r.CustomerPhone)).Append(")</div><hr><table>");
        foreach (var l in r.Lines)
            sb.Append("<tr><td>").Append(e.Encode(l.Description)).Append(" x").Append(l.Qty).Append("</td><td class=\"r\">").Append(Money(l.LineTotal)).Append("</td></tr>");
        sb.Append("</table><hr><table>");
        void Row(string label, decimal v, bool bold = false)
            => sb.Append("<tr").Append(bold ? " class=\"b\"" : "").Append("><td>").Append(label).Append("</td><td class=\"r\">").Append(Money(v)).Append("</td></tr>");
        Row("Subtotal", r.SubTotal);
        if (r.Discount > 0) Row("Discount", -r.Discount);
        if (r.Alterations > 0) Row("Alterations", r.Alterations);
        Row("Total", r.GrandTotal, true);
        Row("Paid", r.AmountPaid);
        Row("Balance", r.BalanceDue, true);
        sb.Append("</table><hr><div class=\"c\">Thank you!</div><button onclick=\"window.print()\">Print</button></body></html>");
        return sb.ToString();
    }
}
