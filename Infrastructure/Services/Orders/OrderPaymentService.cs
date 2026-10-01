using Application.Common;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.ViewModels.Order;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services.Orders;

/// <summary>
/// Payments and refunds. Each operation inserts one append-only line and recomputes the order's
/// money fields from all lines in the same SaveChanges. The order row carries an xmin
/// concurrency token, so two simultaneous payments cannot both pass the balance check: the loser
/// gets a concurrency conflict, reloads, and is re-validated (up to three attempts).
/// </summary>
public class OrderPaymentService : IOrderPaymentService
{
    private const int MaxAttempts = 3;

    private readonly ApplicationDbContext _db;
    private readonly IOrderRepository _repo;
    private readonly IBusinessCalendar _calendar;
    private readonly INotificationService _notifications;
    private readonly OrderCore _core;

    public OrderPaymentService(ApplicationDbContext db, IOrderRepository repo, IBusinessCalendar calendar,
        INotificationService notifications, OrderCore core)
    {
        _db = db;
        _repo = repo;
        _calendar = calendar;
        _notifications = notifications;
        _core = core;
    }

    public Task<ApiResponse<OrderDetailViewModel>> RecordPaymentAsync(Guid orderId, RecordOrderPaymentViewModel vm, CancellationToken ct = default)
        => WithConcurrencyRetryAsync(async () =>
        {
            if (vm.Amount <= 0) return ApiResponse<OrderDetailViewModel>.Fail("Payment amount must be greater than zero.");
            var order = await _core.LoadForMoneyAsync(orderId, ct);
            if (order == null) return ApiResponse<OrderDetailViewModel>.NotFound("Order not found.");
            if (order.Status == OrderStatus.Cancelled) return ApiResponse<OrderDetailViewModel>.Fail("Cannot take payment on a cancelled order.");

            OrderCore.RecalculateMoney(order);
            if (vm.Amount > order.BalanceDue)
                return ApiResponse<OrderDetailViewModel>.Fail($"Amount exceeds balance due ({order.BalanceDue:N2}).");

            _db.OrderPayments.Add(new OrderPayment
            {
                OrderId       = order.Id,
                Amount        = vm.Amount,
                PaymentMethod = vm.PaymentMethod,
                Note          = vm.Note,
                PaidAt        = _calendar.UtcNow
            });
            OrderCore.RecalculateMoney(order);
            order.UpdatedOn = _calendar.UtcNow;   // always touch the row so the xmin check applies

            await _notifications.EnqueueOrderEventAsync(order, NotificationEvent.PaymentReceived, ct,
                new Dictionary<string, string?> { ["PaymentAmount"] = NotificationTemplates.Money(vm.Amount) });

            await _db.SaveChangesAsync(ct);
            return ApiResponse<OrderDetailViewModel>.Ok((await _repo.GetDetailAsync(order.Id, ct))!, "Payment recorded.");
        }, ct);

    public Task<ApiResponse<OrderDetailViewModel>> RecordRefundAsync(Guid orderId, RecordRefundViewModel vm, CancellationToken ct = default)
        => WithConcurrencyRetryAsync(async () =>
        {
            if (vm.Amount <= 0) return ApiResponse<OrderDetailViewModel>.Fail("Refund amount must be greater than zero.");
            var order = await _core.LoadForMoneyAsync(orderId, ct);
            if (order == null) return ApiResponse<OrderDetailViewModel>.NotFound("Order not found.");

            OrderCore.RecalculateMoney(order);
            if (vm.Amount > order.AmountPaid)
                return ApiResponse<OrderDetailViewModel>.Fail($"Refund ({vm.Amount:N2}) cannot exceed the amount paid ({order.AmountPaid:N2}).");

            _db.OrderPayments.Add(new OrderPayment
            {
                OrderId       = order.Id,
                Amount        = -vm.Amount,
                PaymentMethod = vm.PaymentMethod,
                Note          = string.IsNullOrWhiteSpace(vm.Note) ? "Refund" : vm.Note,
                PaidAt        = _calendar.UtcNow,
                IsRefund      = true
            });
            order.RefundAmount += vm.Amount;
            if (order.Status == OrderStatus.Cancelled) order.RefundStatus = RefundStatus.Processed;
            OrderCore.RecalculateMoney(order);
            order.UpdatedOn = _calendar.UtcNow;

            await _db.SaveChangesAsync(ct);
            return ApiResponse<OrderDetailViewModel>.Ok((await _repo.GetDetailAsync(order.Id, ct))!, "Refund recorded.");
        }, ct);

    private async Task<ApiResponse<OrderDetailViewModel>> WithConcurrencyRetryAsync(
        Func<Task<ApiResponse<OrderDetailViewModel>>> operation, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
            {
                _db.ChangeTracker.Clear();   // drop the stale graph; the next attempt reloads and re-validates
            }
        }
    }
}
