using Application.Common;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Services;
using Application.ViewModels.Finance;
using AutoMapper;
using Domain.Entities;

namespace Infrastructure.Services;

public class StaffSalaryService
    : BaseCrudService<StaffSalary, RecordStaffSalaryViewModel, UpdateStaffSalaryViewModel, StaffSalaryDetailViewModel>,
      IStaffSalaryService
{
    private readonly IStaffSalaryRepository _salaryRepo;
    private readonly ICurrentUserService _currentUser;

    public StaffSalaryService(IStaffSalaryRepository repo, ICurrentUserService currentUser, IMapper mapper)
        : base(repo, mapper)
    {
        _salaryRepo  = repo;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<PagedResult<StaffSalaryDetailViewModel>>> GetFilteredAsync(
        int? month, int? year, Guid? staffId,
        int page, int pageSize, CancellationToken ct = default)
    {
        var result = await _salaryRepo.GetPagedDetailAsync(month, year, staffId, page, pageSize, ct);
        return new ApiResponse<PagedResult<StaffSalaryDetailViewModel>>(result);
    }

    public override async Task<ApiResponse<PagedResult<StaffSalaryDetailViewModel>>> GetAllAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var result = await _salaryRepo.GetPagedDetailAsync(null, null, null, page, pageSize, ct);
        return new ApiResponse<PagedResult<StaffSalaryDetailViewModel>>(result);
    }

    public override async Task<ApiResponse<StaffSalaryDetailViewModel>> CreateAsync(
        RecordStaffSalaryViewModel vm, CancellationToken ct = default)
    {
        var entity = _mapper.Map<StaffSalary>(vm);
        entity.ShopId    = _currentUser.ShopId;
        entity.CreatedBy = _currentUser.UserId;
        entity.CreatedOn = DateTime.UtcNow;
        await _repo.AddAsync(entity, ct);
        return new ApiResponse<StaffSalaryDetailViewModel>(_mapper.Map<StaffSalaryDetailViewModel>(entity), "Salary recorded successfully.");
    }

    public override async Task<ApiResponse<StaffSalaryDetailViewModel>> UpdateAsync(
        UpdateStaffSalaryViewModel vm, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(vm.Id, ct);
        if (entity == null) return new ApiResponse<StaffSalaryDetailViewModel>("Salary record not found.", 404);
        _mapper.Map(vm, entity);
        // Recalculate net after partial update
        entity.NetSalary = entity.BaseSalary + entity.Bonus - entity.Deduction;
        entity.UpdatedBy = _currentUser.UserId;
        entity.UpdatedOn = DateTime.UtcNow;
        await _repo.UpdateAsync(entity, ct);
        return new ApiResponse<StaffSalaryDetailViewModel>(_mapper.Map<StaffSalaryDetailViewModel>(entity), "Updated successfully.");
    }
}
