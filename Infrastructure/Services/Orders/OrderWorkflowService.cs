using Application.Common;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Orders;
using Application.ViewModels.Order;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Orders;

public class OrderWorkflowService : IOrderWorkflowService
{
    private readonly ApplicationDbContext _db;
    private readonly IOrderRepository _repo;
    private readonly ICurrentUserService _currentUser;
    private readonly IBusinessCalendar _calendar;
    private readonly IKarigarLedgerService _ledger;
    private readonly INotificationService _notifications;
    private readonly IPermissionService _permissions;
    private readonly OrderCore _core;

    public OrderWorkflowService(
        ApplicationDbContext db,
        IOrderRepository repo,
        ICurrentUserService currentUser,
        IBusinessCalendar calendar,
        IKarigarLedgerService ledger,
        INotificationService notifications,
        IPermissionService permissions,
        OrderCore core)
    {
        _db = db;
        _repo = repo;
        _currentUser = currentUser;
        _calendar = calendar;
        _ledger = ledger;
        _notifications = notifications;
        _permissions = permissions;
        _core = core;
    }

    private async Task<ApiResponse<OrderDetailViewModel>> DetailAsync(Guid id, string message, CancellationToken ct)
        => ApiResponse<OrderDetailViewModel>.Ok((await _repo.GetDetailAsync(id, ct))!, message);

    // ── Status ───────────────────────────────────────────────────────────────

    public async Task<ApiResponse<OrderDetailViewModel>> ChangeStatusAsync(Guid id, ChangeOrderStatusViewModel vm, CancellationToken ct = default)
    {
        // Delivery and cancellation have their own rules (balance, items, refunds, customer totals).
        if (vm.Status is OrderStatus.Delivered or OrderStatus.PartiallyDelivered)
            return await DeliverAsync(id, new DeliverOrderViewModel { Notes = vm.Notes }, ct);
        if (vm.Status == OrderStatus.Cancelled)
            return await CancelAsync(id, new CancelOrderViewModel { Reason = vm.Notes }, ct);

        var order = await _core.LoadForMoneyAsync(id, ct);
        if (order == null) return ApiResponse<OrderDetailViewModel>.NotFound("Order not found.");

        var error = OrderStateMachine.Validate(order.Status, vm.Status, vm.Notes, vm.Force);
        if (error != null) return ApiResponse<OrderDetailViewModel>.Fail(error);

        await _core.TransitionAsync(order, vm.Status, vm.Notes, manualOverride: vm.Force, ct);
        await _db.SaveChangesAsync(ct);
        return await DetailAsync(id, $"Order status updated to {vm.Status}.", ct);
    }

    // ── Cancel ───────────────────────────────────────────────────────────────

    public async Task<ApiResponse<OrderDetailViewModel>> CancelAsync(Guid id, CancelOrderViewModel vm, CancellationToken ct = default)
    {
        var order = await _core.LoadForMoneyAsync(id, ct);
        if (order == null) return ApiResponse<OrderDetailViewModel>.NotFound("Order not found.");
        if (order.Status == OrderStatus.Delivered) return ApiResponse<OrderDetailViewModel>.Fail("Cannot cancel a delivered order.");
        if (order.Status == OrderStatus.Cancelled) return ApiResponse<OrderDetailViewModel>.Fail("The order is already cancelled.");
        if (vm.RefundAmount < 0) return ApiResponse<OrderDetailViewModel>.Fail("Refund amount cannot be negative.");
        if (vm.RefundAmount > order.AmountPaid)
            return ApiResponse<OrderDetailViewModel>.Fail($"Refund ({vm.RefundAmount:N2}) cannot exceed the amount paid ({order.AmountPaid:N2}).");

        var grandBefore = order.GrandTotal;
        order.CancellationStage  = OrderStateMachine.CancellationStageFor(order.Status);
        order.CancellationReason = vm.Reason;

        if (vm.RefundAmount > 0)
            AddRefundLine(order, vm.RefundAmount, vm.RefundMethod, string.IsNullOrWhiteSpace(vm.Reason) ? "Refund on cancellation" : $"Refund on cancellation: {vm.Reason}");

        order.RefundAmount += vm.RefundAmount;
        var keptByShop = order.AmountPaid - vm.RefundAmount;
        order.RefundStatus = vm.RefundAmount > 0 ? RefundStatus.Processed
            : keptByShop > 0 ? (vm.WaiveRefund ? RefundStatus.Waived : RefundStatus.Pending)
            : null;

        foreach (var item in await _db.OrderItems.Where(i => i.OrderId == id).ToListAsync(ct))
            if (item.Status != OrderItemStatus.Delivered) item.Status = OrderItemStatus.Cancelled;

        await _core.TransitionAsync(order, OrderStatus.Cancelled, vm.Reason, manualOverride: false, ct);
        OrderCore.RecalculateMoney(order);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.SaveChangesAsync(ct);
        await _core.AdjustCustomerStatsAsync(order.CustomerId, -1, -grandBefore, -OrderMath.PointsPerOrder, ct);
        await tx.CommitAsync(ct);

        return await DetailAsync(id, "Order cancelled.", ct);
    }

    // ── Deliver ──────────────────────────────────────────────────────────────

    public async Task<ApiResponse<OrderDetailViewModel>> DeliverAsync(Guid id, DeliverOrderViewModel vm, CancellationToken ct = default)
    {
        var order = await _core.LoadForProductionAsync(id, ct);
        if (order == null) return ApiResponse<OrderDetailViewModel>.NotFound("Order not found.");
        if (order.Status == OrderStatus.Cancelled) return ApiResponse<OrderDetailViewModel>.Fail("Order is cancelled.");
        if (order.Status == OrderStatus.Delivered) return ApiResponse<OrderDetailViewModel>.Fail("Order is already delivered.");

        var pending = order.Items.Where(i => !i.IsDeleted && !i.IsDelivered && i.Status != OrderItemStatus.Cancelled).ToList();
        var toDeliver = vm.ItemIds is { Count: > 0 } ? pending.Where(i => vm.ItemIds.Contains(i.Id)).ToList() : pending;
        if (vm.ItemIds is { Count: > 0 } && toDeliver.Count != vm.ItemIds.Distinct().Count())
            return ApiResponse<OrderDetailViewModel>.Fail("One or more items are not part of this order or were already delivered.");

        OrderCore.RecalculateMoney(order);
        var completesOrder = toDeliver.Count == pending.Count;
        if (completesOrder && order.BalanceDue > 0)
        {
            if (!vm.AllowBalance)
                return ApiResponse<OrderDetailViewModel>.Fail(
                    $"A balance of {order.BalanceDue:N2} is still due. Record the payment first, or deliver with allowBalance and a note (Owner/Manager).");
            if (!await _permissions.HasPermissionAsync(_currentUser.TenantId, _currentUser.Roles, Permissions.OrdersDeliverWithBalance, ct))
                return ApiResponse<OrderDetailViewModel>.Forbidden("Only an Owner or Manager can hand over an order with a balance due.");
            if (string.IsNullOrWhiteSpace(vm.Notes))
                return ApiResponse<OrderDetailViewModel>.Fail("A note is required when delivering with a balance due.");
        }

        var now = _calendar.UtcNow;
        foreach (var item in toDeliver)
        {
            item.IsDelivered = true;
            item.DeliveredOn = now;
            item.Status = OrderItemStatus.Delivered;
        }

        var target = completesOrder ? OrderStatus.Delivered : OrderStatus.PartiallyDelivered;
        if (order.Status != target)
        {
            var note = vm.Notes ?? (completesOrder ? "Delivered to customer." : $"{toDeliver.Count} item(s) delivered.");
            await _core.TransitionAsync(order, target, note, manualOverride: false, ct);
        }
        else
        {
            _db.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.Id, ChangedBy = _currentUser.UserId, FromStatus = order.Status, ToStatus = order.Status,
                ChangedOn = now, Notes = vm.Notes ?? $"{toDeliver.Count} more item(s) delivered."
            });
        }

        await _db.SaveChangesAsync(ct);
        return await DetailAsync(id, completesOrder ? "Order marked as delivered." : "Items delivered.", ct);
    }

    // ── Trial ────────────────────────────────────────────────────────────────

    public async Task<ApiResponse<OrderDetailViewModel>> RecordTrialResultAsync(Guid id, TrialResultViewModel vm, CancellationToken ct = default)
    {
        var order = await _core.LoadForProductionAsync(id, ct);
        if (order == null) return ApiResponse<OrderDetailViewModel>.NotFound("Order not found.");
        if (order.Status is not (OrderStatus.TrialReady or OrderStatus.TrialDone))
            return ApiResponse<OrderDetailViewModel>.Fail($"A trial result can only be recorded when the order is TrialReady (it is {order.Status}).");

        order.TrialNotes = vm.Notes;
        if (order.Status == OrderStatus.TrialReady)
            await _core.TransitionAsync(order, OrderStatus.TrialDone, vm.Notes ?? "Trial completed.", manualOverride: false, ct);

        if (vm.Outcome == TrialOutcome.Approved)
        {
            await _core.TransitionAsync(order, OrderStatus.ReadyToDeliver, "Customer approved the fitting.", manualOverride: false, ct);
        }
        else
        {
            if (vm.Alterations.Count == 0)
                return ApiResponse<OrderDetailViewModel>.Fail("List at least one alteration when the fitting needs changes.");
            foreach (var a in vm.Alterations)
            {
                var item = order.Items.FirstOrDefault(i => i.Id == a.OrderItemId && !i.IsDeleted);
                if (item == null) return ApiResponse<OrderDetailViewModel>.Fail("An alteration refers to an item that is not in this order.");
                if (a.AdditionalCharge < 0) return ApiResponse<OrderDetailViewModel>.Fail("Alteration charge cannot be negative.");
                if (a.AssignedKarigarId.HasValue && !await KarigarExistsAsync(a.AssignedKarigarId.Value, ct))
                    return ApiResponse<OrderDetailViewModel>.NotFound("Assigned karigar not found.");

                item.FittingNotes = string.IsNullOrWhiteSpace(item.FittingNotes) ? a.Description : $"{item.FittingNotes}; {a.Description}";
                _db.OrderAlterations.Add(new OrderAlteration
                {
                    OrderId = order.Id, OrderItemId = item.Id, Description = a.Description, RequiredChange = a.RequiredChange,
                    AdditionalCharge = a.AdditionalCharge, AssignedKarigarId = a.AssignedKarigarId,
                    Status = AlterationStatus.Received, Notes = "From trial fitting"
                });
            }
            OrderCore.RecalculateMoney(order);
        }

        await _db.SaveChangesAsync(ct);
        return await DetailAsync(id, "Trial result recorded.", ct);
    }

    // ── Stages ───────────────────────────────────────────────────────────────

    public async Task<ApiResponse<object>> AssignStageAsync(AssignStageViewModel vm, CancellationToken ct = default)
    {
        var order = await LoadOrderForItemAsync(vm.OrderItemId, ct);
        if (order == null) return ApiResponse<object>.NotFound("Order item not found.");
        if (OrderStateMachine.IsTerminal(order.Status)) return ApiResponse<object>.Fail($"The order is {order.Status}.");
        if (vm.AssignedKarigarId == Guid.Empty || !await KarigarExistsAsync(vm.AssignedKarigarId, ct))
            return ApiResponse<object>.NotFound("Assigned karigar not found.");

        var item = order.Items.First(i => i.Id == vm.OrderItemId);
        var price = vm.StagePrice ?? await _ledger.ResolveStagePriceAsync(vm.AssignedKarigarId, item.GarmentType, vm.Stage, item.Qty, ct);
        var existing = item.StageAssignments.FirstOrDefault(a => a.Stage == vm.Stage);

        if (existing != null)
        {
            var previous = existing.AssignedKarigarId;
            existing.AssignedKarigarId = vm.AssignedKarigarId;
            existing.StagePrice        = price;
            existing.EstimatedDays     = vm.EstimatedDays;
            if (previous != vm.AssignedKarigarId) _core.RecordReassignment(order, item.Id, previous, vm.AssignedKarigarId, vm.Stage);
            await _db.SaveChangesAsync(ct);
            return ApiResponse<object>.Ok(null, "Stage assignment updated.");
        }

        _db.OrderItemStageAssignments.Add(new OrderItemStageAssignment
        {
            OrderItemId       = item.Id,
            Stage             = vm.Stage,
            AssignedKarigarId = vm.AssignedKarigarId,
            StagePrice        = price,
            EstimatedDays     = vm.EstimatedDays
        });
        _core.RecordReassignment(order, item.Id, null, vm.AssignedKarigarId, vm.Stage);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(null, "Stage assigned.");
    }

    public async Task<ApiResponse<object>> RecordStageLogAsync(RecordStageLogViewModel vm, CancellationToken ct = default)
    {
        var order = await LoadOrderForItemAsync(vm.OrderItemId, ct);
        if (order == null) return ApiResponse<object>.NotFound("Order item not found.");
        if (OrderStateMachine.IsTerminal(order.Status)) return ApiResponse<object>.Fail($"The order is {order.Status}.");
        var item = order.Items.First(i => i.Id == vm.OrderItemId);
        if (item.Status is OrderItemStatus.Cancelled or OrderItemStatus.Delivered)
            return ApiResponse<object>.Fail($"This item is {item.Status}.");

        var (karigarId, error) = await ResolveWorkerAsync(item, vm.Stage, vm.KarigarId, ct);
        if (error != null) return error.As<object>();

        var logs = item.StageLogs.Where(l => l.Stage == vm.Stage).OrderBy(l => l.CreatedOn).ToList();
        if (logs.Any(l => l.Status == StageLogStatus.InProgress && !l.CompletedAt.HasValue))
            return ApiResponse<object>.Conflict($"{vm.Stage} is already in progress for this item.");
        var latest = logs.LastOrDefault();
        if (latest?.Status == StageLogStatus.Completed)
            return ApiResponse<object>.Fail($"{vm.Stage} is already completed for this item.");

        var log = new OrderItemStageLog
        {
            OrderItemId  = item.Id,
            Stage        = vm.Stage,
            KarigarId    = karigarId,
            Notes        = vm.Notes,
            Status       = StageLogStatus.InProgress,
            ReworkReason = latest?.Status == StageLogStatus.ReworkRequired ? $"Rework: {latest.ReworkReason}" : null
        };
        _db.OrderItemStageLogs.Add(log);
        item.Status = OrderItemStatus.InProgress;

        if (OrderStateMachine.ShouldAutoAdvance(order.Status, vm.Stage, out var target))
            await _core.TransitionAsync(order, target, $"Automatic: {vm.Stage} started.", manualOverride: false, ct, item.Id);

        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(null, "Stage log recorded.");
    }

    public async Task<ApiResponse<object>> CompleteStageAsync(CompleteStageViewModel vm, CancellationToken ct = default)
    {
        var order = await LoadOrderForItemAsync(vm.OrderItemId, ct);
        if (order == null) return ApiResponse<object>.NotFound("Order item not found.");
        if (OrderStateMachine.IsTerminal(order.Status)) return ApiResponse<object>.Fail($"The order is {order.Status}.");
        var item = order.Items.First(i => i.Id == vm.OrderItemId);

        var logs = item.StageLogs.Where(l => l.Stage == vm.Stage).OrderBy(l => l.CreatedOn).ToList();
        var log = logs.LastOrDefault(l => l.Status == StageLogStatus.InProgress && !l.CompletedAt.HasValue);

        if (log == null)
        {
            if (logs.LastOrDefault()?.Status == StageLogStatus.Completed)
                return ApiResponse<object>.Fail($"{vm.Stage} is already completed for this item.");

            // Small shops often skip "start": complete directly, attributed to the assignee or the caller.
            var (karigarId, error) = await ResolveWorkerAsync(item, vm.Stage, Guid.Empty, ct);
            if (error != null) return error.As<object>();
            var previous = logs.LastOrDefault();
            log = new OrderItemStageLog
            {
                OrderItemId  = item.Id,
                Stage        = vm.Stage,
                KarigarId    = karigarId,
                Status       = StageLogStatus.InProgress,
                ReworkReason = previous?.Status == StageLogStatus.ReworkRequired ? $"Rework: {previous.ReworkReason}" : null
            };
            _db.OrderItemStageLogs.Add(log);
        }
        else if (!_currentUser.IsSupervisor && log.KarigarId != _currentUser.UserId)
        {
            return ApiResponse<object>.Forbidden("This work was started by another karigar.");
        }

        log.CompletedAt   = _calendar.UtcNow;
        if (vm.Notes != null) log.Notes = vm.Notes;
        log.FabricUsed    = vm.FabricUsed;
        log.CutQuantity   = vm.CutQuantity;
        log.DefectNotes   = vm.DefectNotes;
        log.RecutRequired = vm.RecutRequired;

        if (vm.RecutRequired)
        {
            log.Status = StageLogStatus.ReworkRequired;
            log.ReworkReason = string.IsNullOrWhiteSpace(vm.DefectNotes) ? "Recut required" : $"Recut required: {vm.DefectNotes}";
        }
        else
        {
            log.Status = StageLogStatus.Completed;
            if (log.ReworkReason == null)   // rework is not paid twice
                await _ledger.AddEarningAsync(log, item, ct);
        }

        await _core.SyncProductionStatusAsync(order, ct);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(null, vm.RecutRequired ? "Recut recorded; the cutting stage is open again." : "Stage completed.");
    }

    public async Task<ApiResponse<object>> RejectStageAsync(RejectStageViewModel vm, CancellationToken ct = default)
    {
        var order = await LoadOrderForItemAsync(vm.OrderItemId, ct);
        if (order == null) return ApiResponse<object>.NotFound("Order item not found.");
        if (OrderStateMachine.IsTerminal(order.Status)) return ApiResponse<object>.Fail($"The order is {order.Status}.");
        var item = order.Items.First(i => i.Id == vm.OrderItemId);

        var log = item.StageLogs.Where(l => l.Stage == vm.Stage).OrderBy(l => l.CreatedOn).LastOrDefault();
        if (log == null || log.Status != StageLogStatus.Completed)
            return ApiResponse<object>.Fail($"There is no completed {vm.Stage} work to reject on this item.");

        log.Status = StageLogStatus.ReworkRequired;
        log.ReworkReason = vm.Reason;
        item.Status = OrderItemStatus.InProgress;
        if (vm.DeductAmount is > 0)
            _ledger.AddDeduction(log.KarigarId, vm.DeductAmount.Value, item.Id, vm.Stage, $"Rework: {vm.Reason}");

        if (order.Status is OrderStatus.ReadyToDeliver or OrderStatus.TrialReady or OrderStatus.TrialDone or OrderStatus.Finishing)
        {
            var back = OrderStateMachine.StatusForStage(vm.Stage);
            if (back != order.Status)
                await _core.TransitionAsync(order, back, $"Quality check: {vm.Reason}", manualOverride: false, ct, item.Id);
        }

        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(null, "Work sent back for rework.");
    }

    // ── Alterations ──────────────────────────────────────────────────────────

    public async Task<ApiResponse<OrderAlterationViewModel>> AddAlterationAsync(CreateOrderAlterationViewModel vm, CancellationToken ct = default)
    {
        var orderId = await _db.OrderItems.Where(i => i.Id == vm.OrderItemId).Select(i => (Guid?)i.OrderId).FirstOrDefaultAsync(ct);
        if (orderId == null) return ApiResponse<OrderAlterationViewModel>.NotFound("Order item not found.");
        var order = await _core.LoadForMoneyAsync(orderId.Value, ct);
        if (order == null) return ApiResponse<OrderAlterationViewModel>.NotFound("Order not found.");
        if (order.Status == OrderStatus.Cancelled) return ApiResponse<OrderAlterationViewModel>.Fail("Order is cancelled.");
        if (vm.AdditionalCharge < 0) return ApiResponse<OrderAlterationViewModel>.Fail("Alteration charge cannot be negative.");
        if (vm.AssignedKarigarId.HasValue && !await KarigarExistsAsync(vm.AssignedKarigarId.Value, ct))
            return ApiResponse<OrderAlterationViewModel>.NotFound("Assigned karigar not found.");

        var alteration = new OrderAlteration
        {
            OrderId           = order.Id,
            OrderItemId       = vm.OrderItemId,
            Description       = vm.Description,
            RequiredChange    = vm.RequiredChange,
            AdditionalCharge  = vm.AdditionalCharge,
            DeliveryDate      = vm.DeliveryDate.HasValue ? await _calendar.ToCalendarDateAsync(vm.DeliveryDate.Value, ct) : null,
            AssignedKarigarId = vm.AssignedKarigarId,
            Notes             = vm.Notes,
            Status            = AlterationStatus.Received
        };
        _db.OrderAlterations.Add(alteration);

        var totalBefore = order.GrandTotal;
        OrderCore.RecalculateMoney(order);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.SaveChangesAsync(ct);
        if (order.GrandTotal != totalBefore)
            await _core.AdjustCustomerStatsAsync(order.CustomerId, 0, order.GrandTotal - totalBefore, 0, ct);
        await tx.CommitAsync(ct);

        var list = await _repo.GetAlterationsAsync(order.Id, ct);
        return ApiResponse<OrderAlterationViewModel>.Ok(list.First(a => a.Id == alteration.Id), "Alteration added.");
    }

    public async Task<ApiResponse<List<OrderAlterationViewModel>>> GetAlterationsAsync(Guid orderId, CancellationToken ct = default)
    {
        if (!await _db.Orders.AnyAsync(o => o.Id == orderId, ct))
            return ApiResponse<List<OrderAlterationViewModel>>.NotFound("Order not found.");
        return ApiResponse<List<OrderAlterationViewModel>>.Ok(await _repo.GetAlterationsAsync(orderId, ct));
    }

    private static readonly Dictionary<AlterationStatus, AlterationStatus[]> AlterationFlow = new()
    {
        [AlterationStatus.Received]   = new[] { AlterationStatus.InProgress, AlterationStatus.Done, AlterationStatus.Cancelled },
        [AlterationStatus.InProgress] = new[] { AlterationStatus.Done, AlterationStatus.Received, AlterationStatus.Cancelled },
        [AlterationStatus.Done]       = new[] { AlterationStatus.Delivered, AlterationStatus.InProgress },
        [AlterationStatus.Delivered]  = Array.Empty<AlterationStatus>(),
        [AlterationStatus.Cancelled]  = Array.Empty<AlterationStatus>()
    };

    public async Task<ApiResponse<OrderAlterationViewModel>> UpdateAlterationAsync(Guid alterationId, UpdateAlterationViewModel vm, CancellationToken ct = default)
    {
        var alteration = await _db.OrderAlterations.FirstOrDefaultAsync(a => a.Id == alterationId, ct);
        if (alteration == null) return ApiResponse<OrderAlterationViewModel>.NotFound("Alteration not found.");
        var order = await _core.LoadForMoneyAsync(alteration.OrderId, ct);
        if (order == null) return ApiResponse<OrderAlterationViewModel>.NotFound("Order not found.");

        if (vm.AssignedKarigarId.HasValue)
        {
            if (!await KarigarExistsAsync(vm.AssignedKarigarId.Value, ct))
                return ApiResponse<OrderAlterationViewModel>.NotFound("Assigned karigar not found.");
            alteration.AssignedKarigarId = vm.AssignedKarigarId;
        }
        if (vm.RequiredChange != null) alteration.RequiredChange = vm.RequiredChange;
        if (vm.Notes != null) alteration.Notes = vm.Notes;
        if (vm.DeliveryDate.HasValue) alteration.DeliveryDate = await _calendar.ToCalendarDateAsync(vm.DeliveryDate.Value, ct);

        var totalBefore = order.GrandTotal;
        if (vm.Status.HasValue && vm.Status != alteration.Status)
        {
            if (!AlterationFlow[alteration.Status].Contains(vm.Status.Value))
                return ApiResponse<OrderAlterationViewModel>.Fail(
                    $"Cannot move an alteration from {alteration.Status} to {vm.Status}. Allowed: {string.Join(", ", AlterationFlow[alteration.Status])}.");

            alteration.Status = vm.Status.Value;
            var now = _calendar.UtcNow;
            if (vm.Status == AlterationStatus.Done)
            {
                alteration.CompletedOn = now;
                await _notifications.EnqueueOrderEventAsync(order, NotificationEvent.AlterationReady, ct);
            }
            if (vm.Status == AlterationStatus.Delivered) alteration.DeliveredOn = now;
            OrderCore.RecalculateMoney(order);   // a cancelled alteration no longer charges

            // Fitting changes finished → the order is ready again.
            var open = order.Alterations.Any(a => !a.IsDeleted && a.Status is AlterationStatus.Received or AlterationStatus.InProgress);
            if (!open && order.Status == OrderStatus.TrialDone)
                await _core.TransitionAsync(order, OrderStatus.ReadyToDeliver, "Automatic: trial alterations finished.", manualOverride: false, ct);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _db.SaveChangesAsync(ct);
        if (order.GrandTotal != totalBefore)
            await _core.AdjustCustomerStatsAsync(order.CustomerId, 0, order.GrandTotal - totalBefore, 0, ct);
        await tx.CommitAsync(ct);

        var list = await _repo.GetAlterationsAsync(order.Id, ct);
        return ApiResponse<OrderAlterationViewModel>.Ok(list.First(a => a.Id == alteration.Id), "Alteration updated.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<Order?> LoadOrderForItemAsync(Guid itemId, CancellationToken ct)
    {
        var orderId = await _db.OrderItems.Where(i => i.Id == itemId && !i.IsDeleted).Select(i => (Guid?)i.OrderId).FirstOrDefaultAsync(ct);
        return orderId == null ? null : await _core.LoadForProductionAsync(orderId.Value, ct);
    }

    private Task<bool> KarigarExistsAsync(Guid id, CancellationToken ct)
        => _db.AppUsers.AnyAsync(u => u.Id == id && u.ActiveStatus == ActiveStatus.Active, ct);

    /// <summary>
    /// Decides who a stage log belongs to. A supervisor may log for anyone (the requested karigar,
    /// else the stage's assignee). Anyone else may only log their own work, and not on a stage
    /// assigned to someone else.
    /// </summary>
    private async Task<(Guid KarigarId, ApiResponse<object>? Error)> ResolveWorkerAsync(OrderItem item, ProductionStage stage, Guid requested, CancellationToken ct)
    {
        var assignee = item.StageAssignments.FirstOrDefault(a => a.Stage == stage)?.AssignedKarigarId;

        if (!_currentUser.IsSupervisor)
        {
            var self = _currentUser.UserId;
            if (requested != Guid.Empty && requested != self)
                return (Guid.Empty, ApiResponse<object>.Forbidden("You can only record your own work."));
            if (assignee.HasValue && assignee != self)
                return (Guid.Empty, ApiResponse<object>.Forbidden($"{stage} on this item is assigned to another karigar."));
            return (self, null);
        }

        var karigarId = requested != Guid.Empty ? requested : assignee ?? Guid.Empty;
        if (karigarId == Guid.Empty)
            return (Guid.Empty, ApiResponse<object>.Fail($"Choose the karigar for {stage}: the stage has no assignee."));
        if (!await KarigarExistsAsync(karigarId, ct))
            return (Guid.Empty, ApiResponse<object>.NotFound("Karigar not found."));
        return (karigarId, null);
    }

    private void AddRefundLine(Order order, decimal amount, PaymentMethod method, string note)
        => _db.OrderPayments.Add(new OrderPayment
        {
            OrderId = order.Id, Amount = -amount, PaymentMethod = method, Note = note,
            PaidAt = _calendar.UtcNow, IsRefund = true
        });
}
