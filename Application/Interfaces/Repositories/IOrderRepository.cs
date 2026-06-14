using Application.Common;
using Application.ViewModels.Order;
using Domain.Entities;

namespace Application.Interfaces.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<OrderDetailViewModel?> GetDetailAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<OrderListViewModel>> SearchAsync(OrderSearchViewModel filter, CancellationToken ct = default);
    Task<List<OrderListViewModel>> GetByCustomerAsync(Guid customerId, CancellationToken ct = default);
    Task<List<OrderListViewModel>> GetDueTodayAsync(CancellationToken ct = default);
    Task<List<OrderListViewModel>> GetOverdueAsync(CancellationToken ct = default);
    Task<Order> CreateAsync(Order order, CancellationToken ct = default);
    Task UpdateAsync(Order order, CancellationToken ct = default);
    Task UpdateStatusAsync(Guid id, Domain.Enums.OrderStatus status, CancellationToken ct = default);
    Task<OrderItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
