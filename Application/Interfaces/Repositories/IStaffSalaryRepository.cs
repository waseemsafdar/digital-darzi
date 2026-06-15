using Application.Common;
using Application.ViewModels.Finance;
using Domain.Entities;

namespace Application.Interfaces.Repositories;

public interface IStaffSalaryRepository : IBaseRepository<StaffSalary>
{
    Task<PagedResult<StaffSalaryDetailViewModel>> GetPagedDetailAsync(
        int? month, int? year, Guid? staffId,
        int page, int pageSize, CancellationToken ct = default);
}
