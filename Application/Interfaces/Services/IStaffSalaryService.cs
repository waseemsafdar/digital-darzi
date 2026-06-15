using Application.Common;
using Application.ViewModels.Finance;

namespace Application.Interfaces.Services;

public interface IStaffSalaryService
    : IBaseCrudService<RecordStaffSalaryViewModel, UpdateStaffSalaryViewModel, StaffSalaryDetailViewModel>
{
    Task<ApiResponse<PagedResult<StaffSalaryDetailViewModel>>> GetFilteredAsync(
        int? month, int? year, Guid? staffId,
        int page, int pageSize, CancellationToken ct = default);
}
