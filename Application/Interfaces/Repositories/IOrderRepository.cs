using Application.Common;
using Application.ViewModels.Order;
using Domain.Entities;

namespace Application.Interfaces.Repositories;

/// <summary>Read models for orders. Writes go through the order services and the DbContext.</summary>
public interface IOrderRepository : IBaseRepository<Order>
{
    Task<OrderDetailViewModel?> GetDetailAsync(Guid id, CancellationToken ct = default);
    Task<List<OrderStatusHistoryViewModel>> GetHistoryAsync(Guid orderId, CancellationToken ct = default);
    Task<PagedResult<OrderListViewModel>> SearchAsync(OrderSearchViewModel filter, DateTime today, CancellationToken ct = default);
    Task<List<OrderListViewModel>> GetByCustomerAsync(Guid customerId, int limit, CancellationToken ct = default);
    Task<List<OrderListViewModel>> GetDueOnAsync(DateTime calendarDate, int limit, CancellationToken ct = default);
    Task<List<OrderListViewModel>> GetOverdueAsync(DateTime today, int limit, CancellationToken ct = default);
    Task<DeliveryBoardViewModel> GetBoardAsync(DateTime today, DateTime todayStartUtc, DateTime todayEndUtc, int perBucket, CancellationToken ct = default);
    Task<List<OrderAlterationViewModel>> GetAlterationsAsync(Guid orderId, CancellationToken ct = default);
}
