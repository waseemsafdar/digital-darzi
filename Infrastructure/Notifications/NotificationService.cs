using Application.Common;
using Application.Interfaces.Services;
using Application.ViewModels.Notifications;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Notifications;

public interface INotificationService
{
    /// <summary>
    /// Adds an outbox row for an order event to the current DbContext without saving, so it is
    /// committed atomically with the business change that caused it. Returns false when the
    /// message was skipped (shop disabled, customer opted out, template disabled, no number).
    /// </summary>
    Task<bool> EnqueueOrderEventAsync(Order order, NotificationEvent eventType, CancellationToken ct = default,
        IDictionary<string, string?>? extra = null, DateTime? scheduledAt = null, string? customBody = null);

    /// <summary>Adds an outbox row addressed directly to a recipient (OTP codes, tests). Does not save.</summary>
    void EnqueueDirect(Guid tenantId, Guid branchId, NotificationChannel channel, string recipient, string body, NotificationEvent eventType);

    Task<ApiResponse<List<NotificationTemplateViewModel>>> GetTemplatesAsync(CancellationToken ct = default);
    Task<ApiResponse<NotificationTemplateViewModel>> UpsertTemplateAsync(UpsertNotificationTemplateViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<PagedResult<NotificationMessageViewModel>>> GetMessagesAsync(Guid? orderId, NotificationStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task<ApiResponse<NotificationMessageViewModel>> SendForOrderAsync(Guid orderId, SendOrderNotificationViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<NotificationMessageViewModel>> SendTestAsync(TestNotificationViewModel vm, CancellationToken ct = default);
}

public sealed class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly IShopSettingsCache _shops;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _clock;

    public NotificationService(ApplicationDbContext db, IShopSettingsCache shops, ICurrentUserService currentUser, TimeProvider clock)
    {
        _db = db;
        _shops = shops;
        _currentUser = currentUser;
        _clock = clock;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    public async Task<bool> EnqueueOrderEventAsync(Order order, NotificationEvent eventType, CancellationToken ct = default,
        IDictionary<string, string?>? extra = null, DateTime? scheduledAt = null, string? customBody = null)
    {
        var shop = await _shops.GetAsync(order.BranchId, ct);
        if (shop == null || !shop.NotificationsEnabled) return false;

        var customer = order.Customer ?? await _db.Customers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == order.CustomerId && c.TenantId == order.TenantId, ct);
        if (customer == null || customer.NotificationsOptOut) return false;

        var (body, channel, enabled) = await ResolveTemplateAsync(order.TenantId, order.BranchId, eventType, ct);
        if (eventType == NotificationEvent.Custom)
        {
            if (string.IsNullOrWhiteSpace(customBody)) return false;
            body = customBody;
            enabled = true;
        }
        if (!enabled || string.IsNullOrWhiteSpace(body)) return false;

        var recipient = channel == NotificationChannel.WhatsApp
            ? FirstNonEmpty(customer.WhatsAppNumber, customer.Phone)
            : FirstNonEmpty(customer.Phone, customer.WhatsAppNumber);
        if (recipient == null) return false;

        // Avoid duplicates when the same event fires twice before the first message goes out.
        var duplicatePending = _db.ChangeTracker.Entries<NotificationMessage>()
            .Any(e => e.Entity.OrderId == order.Id && e.Entity.EventType == eventType && e.State == EntityState.Added)
            || await _db.NotificationMessages.IgnoreQueryFilters()
                .AnyAsync(m => m.OrderId == order.Id && m.EventType == eventType && m.Status == NotificationStatus.Pending, ct);
        if (duplicatePending && eventType != NotificationEvent.Custom) return false;

        var values = BuildValues(order, customer, shop);
        if (extra != null)
            foreach (var kv in extra) values[kv.Key] = kv.Value;

        _db.NotificationMessages.Add(new NotificationMessage
        {
            TenantId    = order.TenantId,
            BranchId    = order.BranchId,
            EventType   = eventType,
            Channel     = channel,
            Recipient   = recipient,
            Body        = NotificationTemplates.Render(body, values),
            Status      = NotificationStatus.Pending,
            ScheduledAt = scheduledAt ?? Now,
            OrderId     = order.Id,
            CustomerId  = customer.Id
        });
        return true;
    }

    public void EnqueueDirect(Guid tenantId, Guid branchId, NotificationChannel channel, string recipient, string body, NotificationEvent eventType)
    {
        _db.NotificationMessages.Add(new NotificationMessage
        {
            TenantId    = tenantId,
            BranchId    = branchId,
            EventType   = eventType,
            Channel     = channel,
            Recipient   = recipient,
            Body        = body,
            Status      = NotificationStatus.Pending,
            ScheduledAt = Now
        });
    }

    public async Task<ApiResponse<List<NotificationTemplateViewModel>>> GetTemplatesAsync(CancellationToken ct = default)
    {
        var custom = await _db.NotificationTemplates.AsNoTracking().ToListAsync(ct);
        var list = new List<NotificationTemplateViewModel>();
        foreach (var (evt, def) in NotificationTemplates.Defaults)
        {
            var mine = custom.Where(t => t.EventType == evt).ToList();
            if (mine.Count == 0)
            {
                list.Add(new NotificationTemplateViewModel
                {
                    EventType = evt, Channel = NotificationChannel.WhatsApp, Body = def.Body, IsEnabled = def.Enabled, IsDefault = true
                });
                continue;
            }
            list.AddRange(mine.Select(t => new NotificationTemplateViewModel
            {
                EventType = t.EventType, Channel = t.Channel, Body = t.Body, IsEnabled = t.IsEnabled, IsDefault = false
            }));
        }
        return ApiResponse<List<NotificationTemplateViewModel>>.Ok(list.OrderBy(t => t.EventType).ToList());
    }

    public async Task<ApiResponse<NotificationTemplateViewModel>> UpsertTemplateAsync(UpsertNotificationTemplateViewModel vm, CancellationToken ct = default)
    {
        if (vm.EventType == NotificationEvent.Custom)
            return ApiResponse<NotificationTemplateViewModel>.Fail("Custom messages have no stored template.");

        // One active template per event: replace whichever channel was configured before.
        var existing = await _db.NotificationTemplates.Where(t => t.EventType == vm.EventType).ToListAsync(ct);
        var row = existing.FirstOrDefault(t => t.Channel == vm.Channel);
        foreach (var other in existing.Where(t => t != row)) other.IsDeleted = true;

        if (row == null)
        {
            row = new NotificationTemplate { EventType = vm.EventType, Channel = vm.Channel };
            _db.NotificationTemplates.Add(row);
        }
        row.Body = vm.Body.Trim();
        row.IsEnabled = vm.IsEnabled;
        await _db.SaveChangesAsync(ct);

        return ApiResponse<NotificationTemplateViewModel>.Ok(new NotificationTemplateViewModel
        {
            EventType = row.EventType, Channel = row.Channel, Body = row.Body, IsEnabled = row.IsEnabled, IsDefault = false
        }, "Template saved.");
    }

    public async Task<ApiResponse<PagedResult<NotificationMessageViewModel>>> GetMessagesAsync(
        Guid? orderId, NotificationStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var q = _db.NotificationMessages.AsNoTracking();
        if (orderId.HasValue) q = q.Where(m => m.OrderId == orderId);
        if (status.HasValue) q = q.Where(m => m.Status == status);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(m => m.CreatedOn)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => ToViewModel(m))
            .ToListAsync(ct);
        return ApiResponse<PagedResult<NotificationMessageViewModel>>.Ok(PagedResult<NotificationMessageViewModel>.From(items, total, page, pageSize));
    }

    public async Task<ApiResponse<NotificationMessageViewModel>> SendForOrderAsync(Guid orderId, SendOrderNotificationViewModel vm, CancellationToken ct = default)
    {
        var order = await _db.Orders.Include(o => o.Customer).FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order == null) return ApiResponse<NotificationMessageViewModel>.NotFound("Order not found.");
        if (vm.EventType == NotificationEvent.PasswordReset)
            return ApiResponse<NotificationMessageViewModel>.Fail("This message type cannot be sent for an order.");

        var queued = await EnqueueOrderEventAsync(order, vm.EventType, ct, customBody: vm.CustomBody);
        if (!queued)
            return ApiResponse<NotificationMessageViewModel>.Fail(
                "Nothing was sent: notifications are off for this shop, the customer opted out or has no number, the template is disabled, or the same message is already queued.");

        var message = _db.ChangeTracker.Entries<NotificationMessage>().Last(e => e.State == EntityState.Added).Entity;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<NotificationMessageViewModel>.Ok(ToViewModel(message), "Message queued.");
    }

    public async Task<ApiResponse<NotificationMessageViewModel>> SendTestAsync(TestNotificationViewModel vm, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(vm.Recipient))
            return ApiResponse<NotificationMessageViewModel>.Fail("Recipient is required.");
        EnqueueDirect(_currentUser.TenantId, _currentUser.ShopId, vm.Channel, vm.Recipient.Trim(), vm.Body, NotificationEvent.Custom);
        var message = _db.ChangeTracker.Entries<NotificationMessage>().Last(e => e.State == EntityState.Added).Entity;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<NotificationMessageViewModel>.Ok(ToViewModel(message), "Test message queued.");
    }

    private async Task<(string Body, NotificationChannel Channel, bool Enabled)> ResolveTemplateAsync(
        Guid tenantId, Guid branchId, NotificationEvent evt, CancellationToken ct)
    {
        var custom = await _db.NotificationTemplates.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.BranchId == branchId && t.EventType == evt && !t.IsDeleted)
            .FirstOrDefaultAsync(ct);
        if (custom != null) return (custom.Body, custom.Channel, custom.IsEnabled);
        return NotificationTemplates.Defaults.TryGetValue(evt, out var def)
            ? (def.Body, NotificationChannel.WhatsApp, def.Enabled)
            : (string.Empty, NotificationChannel.WhatsApp, evt == NotificationEvent.Custom);
    }

    internal static Dictionary<string, string?> BuildValues(Order order, Customer customer, ShopSettings shop) => new()
    {
        ["CustomerName"] = customer.Name,
        ["OrderNumber"]  = order.OrderNumber,
        ["ShopName"]     = shop.Name,
        ["ShopPhone"]    = shop.Phone,
        ["Currency"]     = shop.CurrencySymbol,
        ["GrandTotal"]   = NotificationTemplates.Money(order.GrandTotal),
        ["AmountPaid"]   = NotificationTemplates.Money(order.AmountPaid),
        ["BalanceDue"]   = NotificationTemplates.Money(order.BalanceDue),
        ["DeliveryDate"] = NotificationTemplates.Date(order.DeliveryDate),
        ["TrialDate"]    = order.TrialDate.HasValue ? NotificationTemplates.Date(order.TrialDate.Value) : null
    };

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static NotificationMessageViewModel ToViewModel(NotificationMessage m) => new()
    {
        Id = m.Id, EventType = m.EventType, Channel = m.Channel, Recipient = m.Recipient, Body = m.Body,
        Status = m.Status, Attempts = m.Attempts, LastError = m.LastError, ScheduledAt = m.ScheduledAt,
        SentAt = m.SentAt, OrderId = m.OrderId, CustomerId = m.CustomerId
    };
}
