using Application.Common;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Services;
using Application.ViewModels.Finance;
using AutoMapper;
using Domain.Entities;

namespace Infrastructure.Services;

public class StaffAttendanceService
    : BaseCrudService<StaffAttendance, RecordAttendanceViewModel, UpdateAttendanceViewModel, AttendanceDetailViewModel>,
      IStaffAttendanceService
{
    private readonly IStaffAttendanceRepository _attendanceRepo;
    private readonly ICurrentUserService _currentUser;

    public StaffAttendanceService(IStaffAttendanceRepository repo, ICurrentUserService currentUser, IMapper mapper)
        : base(repo, mapper)
    {
        _attendanceRepo = repo;
        _currentUser    = currentUser;
    }

    public async Task<ApiResponse<PagedResult<AttendanceDetailViewModel>>> GetFilteredAsync(
        Guid? staffId, DateTime? from, DateTime? to,
        int page, int pageSize, CancellationToken ct = default)
    {
        var result = await _attendanceRepo.GetPagedDetailAsync(staffId, from, to, page, pageSize, ct);
        return new ApiResponse<PagedResult<AttendanceDetailViewModel>>(result);
    }

    public override async Task<ApiResponse<PagedResult<AttendanceDetailViewModel>>> GetAllAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var result = await _attendanceRepo.GetPagedDetailAsync(null, null, null, page, pageSize, ct);
        return new ApiResponse<PagedResult<AttendanceDetailViewModel>>(result);
    }

    public async Task<ApiResponse<AttendanceSummaryViewModel>> GetSummaryAsync(
        Guid staffId, int month, int year, CancellationToken ct = default)
    {
        var result = await _attendanceRepo.GetSummaryAsync(staffId, month, year, ct);
        return new ApiResponse<AttendanceSummaryViewModel>(result);
    }

    public override async Task<ApiResponse<AttendanceDetailViewModel>> CreateAsync(
        RecordAttendanceViewModel vm, CancellationToken ct = default)
    {
        var entity = _mapper.Map<StaffAttendance>(vm);
        entity.ShopId    = _currentUser.ShopId;
        entity.CreatedBy = _currentUser.UserId;
        entity.CreatedOn = DateTime.UtcNow;
        await _repo.AddAsync(entity, ct);
        return new ApiResponse<AttendanceDetailViewModel>(_mapper.Map<AttendanceDetailViewModel>(entity), "Attendance recorded successfully.");
    }

    public override async Task<ApiResponse<AttendanceDetailViewModel>> UpdateAsync(
        UpdateAttendanceViewModel vm, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(vm.Id, ct);
        if (entity == null) return new ApiResponse<AttendanceDetailViewModel>("Attendance record not found.", 404);
        _mapper.Map(vm, entity);
        entity.UpdatedBy = _currentUser.UserId;
        entity.UpdatedOn = DateTime.UtcNow;
        await _repo.UpdateAsync(entity, ct);
        return new ApiResponse<AttendanceDetailViewModel>(_mapper.Map<AttendanceDetailViewModel>(entity), "Updated successfully.");
    }
}
