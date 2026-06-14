using Application.Common;
using Application.ViewModels.Order;

namespace Application.Interfaces.Services;

public interface IOrderService
{
    Task<ApiResponse<PagedResult<OrderListViewModel>>> SearchAsync(OrderSearchViewModel filter, CancellationToken ct = default);
    Task<ApiResponse<OrderDetailViewModel>> GetDetailAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<List<OrderListViewModel>>> GetByCustomerAsync(Guid customerId, CancellationToken ct = default);
    Task<ApiResponse<List<OrderListViewModel>>> GetDueTodayAsync(CancellationToken ct = default);
    Task<ApiResponse<List<OrderListViewModel>>> GetOverdueAsync(CancellationToken ct = default);
    Task<ApiResponse<OrderDetailViewModel>> CreateAsync(CreateOrderViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<OrderDetailViewModel>> UpdateAsync(Guid id, UpdateOrderViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<object>> UpdateStatusAsync(Guid id, Domain.Enums.OrderStatus status, CancellationToken ct = default);
    Task<ApiResponse<object>> RecordPaymentAsync(Guid orderId, RecordOrderPaymentViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<object>> AddAlterationAsync(CreateOrderAlterationViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<object>> RecordStageLogAsync(RecordStageLogViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<object>> CompleteStageAsync(CompleteStageViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<object>> AssignStageAsync(AssignStageViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<object>> CancelOrderAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<object>> MarkDeliveredAsync(Guid id, CancellationToken ct = default);
}
