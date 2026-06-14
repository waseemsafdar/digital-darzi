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
    Task<OrderItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct = default);
}

