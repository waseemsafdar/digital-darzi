using API.Authorization;
using Application.Common;
using Application.Interfaces.Services;
using Application.ViewModels.Notifications;
using Application.ViewModels.Operations;
using Domain.Enums;
using Infrastructure.Notifications;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>Customer messages: templates, the delivery log, manual sends and a test send.</summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
[HasPermission(Permissions.NotificationsManage)]
public class NotificationsController : BaseController
{
    private readonly INotificationService _service;
    public NotificationsController(INotificationService service) => _service = service;

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates(CancellationToken ct) => ReturnProcessedResponse(await _service.GetTemplatesAsync(ct));

    /// <summary>Customise a message. Placeholders: {CustomerName} {OrderNumber} {ShopName} {Currency} {GrandTotal} {AmountPaid} {BalanceDue} {DeliveryDate} {TrialDate}.</summary>
    [HttpPut("templates")]
    public async Task<IActionResult> UpsertTemplate([FromBody] UpsertNotificationTemplateViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.UpsertTemplateAsync(vm, ct));

    [HttpGet]
    public async Task<IActionResult> GetMessages([FromQuery] Guid? orderId, [FromQuery] NotificationStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => ReturnProcessedResponse(await _service.GetMessagesAsync(orderId, status, page, pageSize, ct));

    [HttpPost("orders/{orderId:guid}")]
    public async Task<IActionResult> SendForOrder(Guid orderId, [FromBody] SendOrderNotificationViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.SendForOrderAsync(orderId, vm, ct));

    [HttpPost("test")]
    public async Task<IActionResult> SendTest([FromBody] TestNotificationViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.SendTestAsync(vm, ct));
}

/// <summary>Files attached to orders, order items, expenses and customers.</summary>
[ApiController]
[Route("api/attachments")]
[Authorize]
public class AttachmentsController : BaseController
{
    private readonly IAttachmentService _service;
    public AttachmentsController(IAttachmentService service) => _service = service;

    private static (string Read, string Write) PermissionsFor(AttachmentType type) => type switch
    {
        AttachmentType.Expense  => (Permissions.FinanceView, Permissions.FinanceManage),
        AttachmentType.Customer => (Permissions.CustomersView, Permissions.CustomersEdit),
        _                       => (Permissions.OrdersView, Permissions.OrdersEdit)
    };

    /// <summary>Multipart upload: file, entityType (Order=1, Expense=2, Customer=3, OrderItem=4), entityId, description.</summary>
    [HttpPost]
    [RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] AttachmentType entityType, [FromForm] Guid entityId, IFormFile? file,
        [FromForm] string? description, CancellationToken ct)
    {
        if (file == null) return BadRequest(ApiResponse<object>.Fail("Attach a file in the 'file' form field."));
        if (!await HasPermissionAsync(PermissionsFor(entityType).Write)) return Forbidden("You cannot add files here.");
        return ReturnProcessedResponse(await _service.UploadAsync(entityType, entityId, file, description, ct));
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AttachmentType entityType, [FromQuery] Guid entityId, CancellationToken ct)
    {
        if (!await HasPermissionAsync(PermissionsFor(entityType).Read)) return Forbidden("You cannot view these files.");
        return ReturnProcessedResponse(await _service.ListAsync(entityType, entityId, ct));
    }

    /// <summary>Streams the file (tenant and branch checked). Not a public URL.</summary>
    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var file = await _service.OpenAsync(id, ct);
        return file == null
            ? NotFound(ApiResponse<object>.NotFound("Attachment not found."))
            : File(file.Content, file.ContentType, file.FileName);
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.OrdersEdit)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) => ReturnProcessedResponse(await _service.DeleteAsync(id, ct));
}

/// <summary>Fabric and accessories stock with an append-only movement ledger.</summary>
[ApiController]
[Route("api/inventory")]
[Authorize]
public class InventoryController : BaseController
{
    private readonly IInventoryService _service;
    public InventoryController(IInventoryService service) => _service = service;

    [HttpGet("suppliers")]
    [HasPermission(Permissions.InventoryView)]
    public async Task<IActionResult> GetSuppliers(CancellationToken ct) => ReturnProcessedResponse(await _service.GetSuppliersAsync(ct));

    [HttpPost("suppliers")]
    [HasPermission(Permissions.InventoryManage)]
    public async Task<IActionResult> CreateSupplier([FromBody] SaveSupplierViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.SaveSupplierAsync(null, vm, ct));

    [HttpPut("suppliers/{id:guid}")]
    [HasPermission(Permissions.InventoryManage)]
    public async Task<IActionResult> UpdateSupplier(Guid id, [FromBody] SaveSupplierViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.SaveSupplierAsync(id, vm, ct));

    [HttpDelete("suppliers/{id:guid}")]
    [HasPermission(Permissions.InventoryManage)]
    public async Task<IActionResult> DeleteSupplier(Guid id, CancellationToken ct) => ReturnProcessedResponse(await _service.DeleteSupplierAsync(id, ct));

    [HttpGet("items")]
    [HasPermission(Permissions.InventoryView)]
    public async Task<IActionResult> SearchItems([FromQuery] InventorySearchViewModel filter, CancellationToken ct)
        => ReturnProcessedResponse(await _service.SearchItemsAsync(filter, ct));

    [HttpGet("items/{id:guid}")]
    [HasPermission(Permissions.InventoryView)]
    public async Task<IActionResult> GetItem(Guid id, CancellationToken ct) => ReturnProcessedResponse(await _service.GetItemAsync(id, ct));

    [HttpGet("low-stock")]
    [HasPermission(Permissions.InventoryView)]
    public async Task<IActionResult> LowStock(CancellationToken ct)
        => ReturnProcessedResponse(await _service.SearchItemsAsync(new InventorySearchViewModel { LowStockOnly = true, PageSize = Paging.MaxPageSize }, ct));

    [HttpPost("items")]
    [HasPermission(Permissions.InventoryManage)]
    public async Task<IActionResult> CreateItem([FromBody] SaveInventoryItemViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.SaveItemAsync(null, vm, ct));

    [HttpPut("items/{id:guid}")]
    [HasPermission(Permissions.InventoryManage)]
    public async Task<IActionResult> UpdateItem(Guid id, [FromBody] SaveInventoryItemViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.SaveItemAsync(id, vm, ct));

    [HttpDelete("items/{id:guid}")]
    [HasPermission(Permissions.InventoryManage)]
    public async Task<IActionResult> DeleteItem(Guid id, CancellationToken ct) => ReturnProcessedResponse(await _service.DeleteItemAsync(id, ct));

    [HttpGet("movements")]
    [HasPermission(Permissions.InventoryView)]
    public async Task<IActionResult> GetMovements([FromQuery] Guid? itemId, [FromQuery] Guid? orderId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => ReturnProcessedResponse(await _service.GetMovementsAsync(itemId, orderId, page, pageSize, ct));

    [HttpPost("items/{id:guid}/purchase")]
    [HasPermission(Permissions.InventoryManage)]
    public async Task<IActionResult> Purchase(Guid id, [FromBody] RecordPurchaseViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.RecordPurchaseAsync(id, vm, ct));

    [HttpPost("items/{id:guid}/issue")]
    [HasPermission(Permissions.InventoryManage)]
    public async Task<IActionResult> Issue(Guid id, [FromBody] IssueToOrderViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.IssueToOrderAsync(id, vm, ct));

    [HttpPost("items/{id:guid}/return")]
    [HasPermission(Permissions.InventoryManage)]
    public async Task<IActionResult> Return(Guid id, [FromBody] ReturnFromOrderViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.ReturnFromOrderAsync(id, vm, ct));

    [HttpPost("items/{id:guid}/adjust")]
    [HasPermission(Permissions.InventoryManage)]
    public async Task<IActionResult> Adjust(Guid id, [FromBody] StockAdjustmentViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.AdjustAsync(id, vm, ct));

    [HttpPost("items/{id:guid}/transfer")]
    [HasPermission(Permissions.InventoryManage)]
    public async Task<IActionResult> Transfer(Guid id, [FromBody] StockTransferViewModel vm, CancellationToken ct)
        => ReturnProcessedResponse(await _service.TransferAsync(id, vm, ct));
}

/// <summary>Cash register: open with a float, record pay-ins/outs, close with a count.</summary>
[ApiController]
[Route("api/cash")]
[Authorize]
[HasPermission(Permissions.CashManage)]
public class CashController : BaseController
{
    private readonly ICashService _service;
    public CashController(ICashService service) => _service = service;

    [HttpPost("open")]
    public async Task<IActionResult> Open([FromBody] OpenCashSessionViewModel vm, CancellationToken ct) => ReturnProcessedResponse(await _service.OpenAsync(vm, ct));

    [HttpGet("current")]
    public async Task<IActionResult> Current(CancellationToken ct) => ReturnProcessedResponse(await _service.GetCurrentAsync(ct));

    [HttpPost("movements")]
    public async Task<IActionResult> Movement([FromBody] CashMovementRequestViewModel vm, CancellationToken ct) => ReturnProcessedResponse(await _service.AddMovementAsync(vm, ct));

    [HttpPost("close")]
    public async Task<IActionResult> Close([FromBody] CloseCashSessionViewModel vm, CancellationToken ct) => ReturnProcessedResponse(await _service.CloseAsync(vm, ct));

    [HttpGet("sessions")]
    public async Task<IActionResult> Sessions([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => ReturnProcessedResponse(await _service.GetSessionsAsync(page, pageSize, ct));
}

/// <summary>Who changed what: orders, payments, measurements, prices, users, stock, cash.</summary>
[ApiController]
[Route("api/audit")]
[Authorize]
[HasPermission(Permissions.AuditView)]
public class AuditController : BaseController
{
    private readonly IAuditService _service;
    public AuditController(IAuditService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] AuditSearchViewModel filter, CancellationToken ct) => ReturnProcessedResponse(await _service.SearchAsync(filter, ct));
}

/// <summary>The Owner's view of the subscription and invoices. Reachable even when the subscription has lapsed.</summary>
[ApiController]
[Route("api/billing")]
[Authorize]
[HasPermission(Permissions.BillingView)]
public class BillingController : BaseController
{
    private readonly IBillingService _service;
    public BillingController(IBillingService service) => _service = service;

    [HttpGet("subscription")]
    public async Task<IActionResult> Subscription(CancellationToken ct) => ReturnProcessedResponse(await _service.GetMySubscriptionAsync(ct));

    [HttpGet("invoices")]
    public async Task<IActionResult> Invoices(CancellationToken ct) => ReturnProcessedResponse(await _service.GetMyInvoicesAsync(ct));

    [HttpGet("plans")]
    public async Task<IActionResult> Plans(CancellationToken ct) => ReturnProcessedResponse(await _service.GetPlansAsync(false, ct));
}

/// <summary>Per-tenant role permissions. The Owner can grant or revoke delegable permissions for Manager, Receptionist and Karigar.</summary>
[ApiController]
[Route("api/permissions")]
[Authorize]
[HasPermission(Permissions.SettingsPermissions)]
public class PermissionsController : BaseController
{
    private readonly IPermissionService _service;
    public PermissionsController(IPermissionService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => ReturnProcessedResponse(await _service.GetMatrixAsync(ct));

    /// <summary>Body: <c>{ "permissions": { "orders.create": true, "payments.refund": false } }</c>. Unlisted permissions are unchanged.</summary>
    [HttpPut("{role}")]
    public async Task<IActionResult> Update(string role, [FromBody] UpdateRolePermissionsRequest request, CancellationToken ct)
        => ReturnProcessedResponse(await _service.UpdateRoleAsync(role, request, ct));
}
