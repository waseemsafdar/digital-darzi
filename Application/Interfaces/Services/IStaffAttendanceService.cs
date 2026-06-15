using Application.Common;
using Application.ViewModels.Common;
using Application.ViewModels.Finance;

namespace Application.Interfaces.Services;

public interface IStaffAttendanceService<TCreate, TUpdate, TDetail>
    : IBaseCrudService<TCreate, TUpdate, TDetail>
    where TCreate : class, IBaseCrudViewModel, new()
    where TUpdate : class, IBaseCrudViewModel, IIdentification, new()
    where TDetail : class, IBaseCrudViewModel, new()
{
    Task<ApiResponse<AttendanceSummaryViewModel>> GetSummaryAsync(
        Guid staffId, int month, int year, CancellationToken ct = default);
}
