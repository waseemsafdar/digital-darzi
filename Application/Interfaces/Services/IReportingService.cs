using Application.Common;
using Application.ViewModels.Reporting;

namespace Application.Interfaces.Services;

/// <summary>
/// Reports. <c>tenantWide</c> = true consolidates every branch of the tenant (Owner only);
/// otherwise figures are for the signed-in branch.
/// </summary>
public interface IReportingService
{
    Task<ApiResponse<DashboardSummaryViewModel>> GetDashboardAsync(DateTime from, DateTime to, bool tenantWide = false, CancellationToken ct = default);
    Task<ApiResponse<RevenueReportViewModel>> GetRevenueReportAsync(DateTime from, DateTime to, bool tenantWide = false, CancellationToken ct = default);
    Task<ApiResponse<List<TopCustomerViewModel>>> GetTopCustomersAsync(DateTime from, DateTime to, int limit, bool tenantWide = false, CancellationToken ct = default);
    Task<ApiResponse<List<StaffPerformanceViewModel>>> GetStaffPerformanceAsync(DateTime from, DateTime to, bool tenantWide = false, CancellationToken ct = default);
    Task<ApiResponse<OrderStatusReportViewModel>> GetOrderStatusReportAsync(bool tenantWide = false, CancellationToken ct = default);
    Task<ApiResponse<List<PendingBalanceViewModel>>> GetPendingBalancesAsync(int limit, bool tenantWide = false, CancellationToken ct = default);
    Task<ApiResponse<List<DailyRevenueViewModel>>> GetDailyRevenueAsync(DateTime from, DateTime to, bool tenantWide = false, CancellationToken ct = default);
    Task<ApiResponse<List<BranchComparisonViewModel>>> GetBranchComparisonAsync(DateTime from, DateTime to, CancellationToken ct = default);
}
