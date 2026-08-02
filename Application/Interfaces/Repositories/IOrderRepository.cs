using Application.Common;
using Application.ViewModels.Order;
using Domain.Entities;
using Domain.Enums;

namespace Application.Interfaces.Repositories;

public interface IOrderRepository : IBaseRepository<Order>
{
    Task<OrderDetailViewModel?> GetDetailAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<OrderListViewModel>> SearchAsync(OrderSearchViewModel filter, CancellationToken ct = default);
    Task<List<OrderListViewModel>> GetByCustomerAsync(Guid customerId, CancellationToken ct = default);
    Task<List<OrderListViewModel>> GetDueTodayAsync(CancellationToken ct = default);
    Task<List<OrderListViewModel>> GetOverdueAsync(CancellationToken ct = default);
    Task UpdateStatusAsync(Guid id, OrderStatus status, CancellationToken ct = default);
    /// <summary>
    /// Inserts a payment row directly and persists it (together with any pending changes on the
    /// tracked order in the same context) in a single SaveChanges. Avoids the graph re-attach that
    /// occurs when a client-keyed payment is added to order.Payments and the order is Update()'d.
    /// </summary>
    Task AddPaymentAsync(OrderPayment payment, CancellationToken ct = default);
    /// <summary>Inserts an alteration row directly (same rationale as <see cref="AddPaymentAsync"/>).</summary>
    Task AddAlterationAsync(OrderAlteration alteration, CancellationToken ct = default);
    Task<OrderItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct = default);
    /// <summary>Lightweight lookup — returns the OrderId for the given item without loading the full tracked graph.</summary>
    Task<Guid?> GetItemOrderIdAsync(Guid itemId, CancellationToken ct = default);
    /// <summary>Returns true if an active (non-deleted) user exists with the given id in the current tenant/branch scope.</summary>
    Task<bool> UserExistsAsync(Guid userId, CancellationToken ct = default);
}

