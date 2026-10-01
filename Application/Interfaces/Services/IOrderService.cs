using Application.Common;
using Application.ViewModels.Order;

namespace Application.Interfaces.Services;

/// <summary>Order creation, editing and read models.</summary>
public interface IOrderService
{
    Task<ApiResponse<PagedResult<OrderListViewModel>>> SearchAsync(OrderSearchViewModel filter, CancellationToken ct = default);
    Task<ApiResponse<OrderDetailViewModel>> GetDetailAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<List<OrderStatusHistoryViewModel>>> GetHistoryAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<List<OrderListViewModel>>> GetByCustomerAsync(Guid customerId, CancellationToken ct = default);
    Task<ApiResponse<List<OrderListViewModel>>> GetDueTodayAsync(CancellationToken ct = default);
    Task<ApiResponse<List<OrderListViewModel>>> GetOverdueAsync(CancellationToken ct = default);
    Task<ApiResponse<DeliveryBoardViewModel>> GetBoardAsync(int perBucket, CancellationToken ct = default);
    Task<ApiResponse<OrderDetailViewModel>> CreateAsync(CreateOrderViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<OrderDetailViewModel>> UpdateAsync(UpdateOrderViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<OrderDetailViewModel>> ReorderAsync(Guid id, ReorderViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<OrderReceiptViewModel>> GetReceiptAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Status changes, production stages, trials, alterations, delivery and cancellation.</summary>
public interface IOrderWorkflowService
{
    Task<ApiResponse<OrderDetailViewModel>> ChangeStatusAsync(Guid id, ChangeOrderStatusViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<OrderDetailViewModel>> CancelAsync(Guid id, CancelOrderViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<OrderDetailViewModel>> DeliverAsync(Guid id, DeliverOrderViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<OrderDetailViewModel>> RecordTrialResultAsync(Guid id, TrialResultViewModel vm, CancellationToken ct = default);

    Task<ApiResponse<object>> AssignStageAsync(AssignStageViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<object>> RecordStageLogAsync(RecordStageLogViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<object>> CompleteStageAsync(CompleteStageViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<object>> RejectStageAsync(RejectStageViewModel vm, CancellationToken ct = default);

    Task<ApiResponse<OrderAlterationViewModel>> AddAlterationAsync(CreateOrderAlterationViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<List<OrderAlterationViewModel>>> GetAlterationsAsync(Guid orderId, CancellationToken ct = default);
    Task<ApiResponse<OrderAlterationViewModel>> UpdateAlterationAsync(Guid alterationId, UpdateAlterationViewModel vm, CancellationToken ct = default);
}

/// <summary>Money received and returned against an order. History is append-only.</summary>
public interface IOrderPaymentService
{
    Task<ApiResponse<OrderDetailViewModel>> RecordPaymentAsync(Guid orderId, RecordOrderPaymentViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<OrderDetailViewModel>> RecordRefundAsync(Guid orderId, RecordRefundViewModel vm, CancellationToken ct = default);
}
