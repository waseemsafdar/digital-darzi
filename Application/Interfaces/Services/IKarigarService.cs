using Application.Common;
using Application.ViewModels.Order;

namespace Application.Interfaces.Services;

public interface IKarigarService
{
    /// <summary>Returns all pending + in-progress stage assignments for the logged-in karigar.</summary>
    Task<ApiResponse<KarigarWorkQueueViewModel>> GetMyWorkQueueAsync(CancellationToken ct = default);

    /// <summary>Returns the work queue for a specific karigar (Owner/Manager view).</summary>
    Task<ApiResponse<KarigarWorkQueueViewModel>> GetKarigarWorkQueueAsync(Guid karigarId, CancellationToken ct = default);

    /// <summary>Summary: how many stages assigned, done, pending per karigar.</summary>
    Task<ApiResponse<List<KarigarPerformanceSummaryViewModel>>> GetAllKarigarsSummaryAsync(CancellationToken ct = default);
}
