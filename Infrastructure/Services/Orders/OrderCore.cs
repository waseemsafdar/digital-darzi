using Application.Interfaces.Services;
using Application.Orders;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Orders;

/// <summary>
/// Rules shared by the order services: status transitions with history and side effects,
/// money recalculated from the payment and alteration lines, customer statistics, and
/// derived item/order production status.
/// </summary>
public sealed class OrderCore
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBusinessCalendar _calendar;
    private readonly INotificationService _notifications;

    public OrderCore(ApplicationDbContext db, ICurrentUserService currentUser, IBusinessCalendar calendar, INotificationService notifications)
    {
        _db = db;
        _currentUser = currentUser;
        _calendar = calendar;
        _notifications = notifications;
    }

    public DateTime Now => _calendar.UtcNow;

    /// <summary>Order with customer, payments and alterations — everything the money rules need.</summary>
    public Task<Order?> LoadForMoneyAsync(Guid orderId, CancellationToken ct)
        => _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Payments)
            .Include(o => o.Alterations)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

    /// <summary>Full production graph: items with their stage plan and logs, plus the money graph.</summary>
    public Task<Order?> LoadForProductionAsync(Guid orderId, CancellationToken ct)
        => _db.Orders
            .Include(o => o.Customer)
            .Include(o => o.Payments)
            .Include(o => o.Alterations)
            .Include(o => o.Items).ThenInclude(i => i.StageAssignments)
            .Include(o => o.Items).ThenInclude(i => i.StageLogs)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

    /// <summary>
    /// Moves the order to <paramref name="to"/> and records it. Callers validate first; this method
    /// only refuses terminal-state changes. Queues the customer message tied to the new status.
    /// </summary>
    public async Task TransitionAsync(Order order, OrderStatus to, string? notes, bool manualOverride, CancellationToken ct,
        Guid? orderItemId = null)
    {
        var from = order.Status;
        if (from == to) return;
        if (OrderStateMachine.IsTerminal(from))
            throw new InvalidOperationException($"Order {order.OrderNumber} is {from} and cannot change status.");

        var now = Now;
        order.Status = to;
        switch (to)
        {
            case OrderStatus.ReadyToDeliver:
                order.ReadyDate ??= now;
                break;
            case OrderStatus.Delivered:
                order.ActualDeliveryDate ??= now;
                break;
            case OrderStatus.Cancelled:
                order.CancelledOn ??= now;
                break;
        }

        _db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            TenantId         = order.TenantId,
            BranchId         = order.BranchId,
            OrderId          = order.Id,
            OrderItemId      = orderItemId,
            ChangedBy        = _currentUser.UserId,
            FromStatus       = from,
            ToStatus         = to,
            IsReversal       = OrderStateMachine.IsReversal(from, to),
            IsManualOverride = manualOverride,
            ChangedOn        = now,
            Notes            = notes
        });

        var evt = to switch
        {
            OrderStatus.ReadyToDeliver => NotificationEvent.OrderReady,
            OrderStatus.TrialReady     => NotificationEvent.TrialReady,
            _                          => (NotificationEvent?)null
        };
        if (evt.HasValue) await _notifications.EnqueueOrderEventAsync(order, evt.Value, ct);
    }

    /// <summary>Records a karigar reassignment in the status history (status unchanged).</summary>
    public void RecordReassignment(Order order, Guid itemId, Guid? fromKarigar, Guid toKarigar, ProductionStage stage)
    {
        _db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            TenantId     = order.TenantId,
            BranchId     = order.BranchId,
            OrderId      = order.Id,
            OrderItemId  = itemId,
            ChangedBy    = _currentUser.UserId,
            FromStatus   = order.Status,
            ToStatus     = order.Status,
            AssignedFrom = fromKarigar,
            AssignedTo   = toKarigar,
            ChangedOn    = Now,
            Notes        = fromKarigar.HasValue ? $"{stage} reassigned" : $"{stage} assigned"
        });
    }

    /// <summary>
    /// Recomputes every money field from its source lines. Requires Payments and Alterations to be
    /// loaded (new lines added through the DbSet are fixed up into the collections by EF).
    /// </summary>
    public static void RecalculateMoney(Order order)
    {
        var alterationCharges = order.Alterations
            .Where(a => !a.IsDeleted && a.Status != AlterationStatus.Cancelled)
            .Sum(a => a.AdditionalCharge);

        order.GrandTotal    = OrderMath.GrandTotal(order.SubTotal, order.Discount, alterationCharges);
        order.AmountPaid    = order.Payments.Sum(p => p.Amount);
        order.BalanceDue    = order.Status == OrderStatus.Cancelled ? 0 : OrderMath.Balance(order.GrandTotal, order.AmountPaid);
        order.PaymentStatus = OrderMath.PaymentStatusFor(order.GrandTotal, order.AmountPaid, order.Payments.Any(p => p.IsRefund));
    }

    /// <summary>Atomic increment of the customer's denormalised totals (no read-modify-write race).</summary>
    public Task AdjustCustomerStatsAsync(Guid customerId, int orders, decimal spend, int points, CancellationToken ct)
        => _db.Customers
            .Where(c => c.Id == customerId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.TotalOrders, c => c.TotalOrders + orders < 0 ? 0 : c.TotalOrders + orders)
                .SetProperty(c => c.TotalSpend, c => c.TotalSpend + spend < 0 ? 0 : c.TotalSpend + spend)
                .SetProperty(c => c.LoyaltyPoints, c => c.LoyaltyPoints + points < 0 ? 0 : c.LoyaltyPoints + points)
                // Thresholds duplicated from OrderMath.LoyaltyTier: EF's SQL translator cannot call
                // an arbitrary C# method inside a SetProperty expression, so the switch is inlined
                // here. Keep the two in sync if the tiers ever change.
                .SetProperty(c => c.LoyaltyTier, c =>
                    c.LoyaltyPoints + points < 100 ? "Bronze"
                    : c.LoyaltyPoints + points < 500 ? "Silver"
                    : c.LoyaltyPoints + points < 1000 ? "Gold"
                    : "Platinum"), ct);

    /// <summary>Latest log per stage for an item.</summary>
    private static Dictionary<ProductionStage, OrderItemStageLog> LatestLogs(OrderItem item)
        => item.StageLogs
            .GroupBy(l => l.Stage)
            .ToDictionary(g => g.Key, g => g.OrderBy(l => l.CreatedOn).ThenBy(l => l.CompletedAt ?? DateTime.MaxValue).Last());

    /// <summary>
    /// An item is finished when every planned stage's latest log is completed. An item without a
    /// stage plan is finished when it has completed work and nothing open or awaiting rework.
    /// </summary>
    public static bool IsItemFinished(OrderItem item)
    {
        var latest = LatestLogs(item);
        var planned = item.StageAssignments.Select(a => a.Stage).Distinct().ToList();
        if (planned.Count == 0)
            return latest.Count > 0 && latest.Values.All(l => l.Status == StageLogStatus.Completed);
        return planned.All(s => latest.TryGetValue(s, out var l) && l.Status == StageLogStatus.Completed);
    }

    /// <summary>
    /// After stage work changes: sets each item's status and, when every active item is finished,
    /// moves the order to TrialReady (trial required and not yet done) or ReadyToDeliver.
    /// </summary>
    public async Task SyncProductionStatusAsync(Order order, CancellationToken ct)
    {
        foreach (var item in order.Items.Where(i => !i.IsDeleted && i.Status is not (OrderItemStatus.Cancelled or OrderItemStatus.Delivered)))
        {
            if (IsItemFinished(item)) item.Status = OrderItemStatus.Done;
            else if (item.StageLogs.Count > 0) item.Status = OrderItemStatus.InProgress;
        }

        var active = order.Items.Where(i => !i.IsDeleted && i.Status != OrderItemStatus.Cancelled).ToList();
        if (active.Count == 0 || !active.All(i => i.Status is OrderItemStatus.Done or OrderItemStatus.Delivered)) return;
        if (!OrderStateMachine.IsInProduction(order.Status)) return;

        var target = order.TrialRequired ? OrderStatus.TrialReady : OrderStatus.ReadyToDeliver;
        await TransitionAsync(order, target, "Automatic: all garments finished.", manualOverride: false, ct);
    }
}
