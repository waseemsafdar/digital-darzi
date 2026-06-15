using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Services;
using Application.ViewModels.Common;
using Application.ViewModels.Finance;
using AutoMapper;
using Domain.Entities;
using System.Net;

namespace Infrastructure.Services;

public class StaffAttendanceService<TCreate, TUpdate, TDetail>
    : BaseCrudService<StaffAttendance, TCreate, TUpdate, TDetail>,
      IStaffAttendanceService<TCreate, TUpdate, TDetail>
    where TCreate : class, IBaseCrudViewModel, new()
    where TUpdate : class, IBaseCrudViewModel, IIdentification, new()
    where TDetail : class, IBaseCrudViewModel, new()
{
    private readonly IStaffAttendanceRepository _attendanceRepo;
    private readonly ICurrentUserService _currentUser;

    public StaffAttendanceService(IStaffAttendanceRepository repo, ICurrentUserService currentUser, IMapper mapper)
        : base(repo, mapper)
    {
        _attendanceRepo = repo;
        _currentUser    = currentUser;
    }

    public async Task<ApiResponse<AttendanceSummaryViewModel>> GetSummaryAsync(
        Guid staffId, int month, int year, CancellationToken ct = default)
    {
        var result = await _attendanceRepo.GetSummaryAsync(staffId, month, year, ct);
        return new ApiResponse<AttendanceSummaryViewModel>(result);
    }

    public override async Task<ApiResponse<Guid>> CreateAsync(
        TCreate vm, CancellationToken ct = default)
    {
        try
        {
            var entity = _mapper.Map<StaffAttendance>(vm);
            entity.ShopId    = _currentUser.ShopId;
            entity.CreatedBy = _currentUser.UserId;
            entity.CreatedOn = DateTime.UtcNow;
            entity.ActiveStatus = Domain.Enums.ActiveStatus.Active;
            await _repo.AddAsync(entity, ct);
            return new ApiResponse<Guid>(entity.Id);
        }
        catch (Exception ex)
        {
            var response = new ApiResponse<Guid>("An error occurred while creating the record.", (int)HttpStatusCode.InternalServerError);
            response.Errors.Add(ex.InnerException?.Message ?? ex.Message);
            return response;
        }
    }

    public override async Task<ApiResponse<Guid>> UpdateAsync(
        TUpdate vm, CancellationToken ct = default)
    {
        try
        {
            var entity = await _repo.GetByIdAsync(vm.Id, ct);
            if (entity == null) return new ApiResponse<Guid>("Attendance record not found.", 404);
            _mapper.Map(vm, entity);
            entity.UpdatedBy = _currentUser.UserId;
            entity.UpdatedOn = DateTime.UtcNow;
            await _repo.UpdateAsync(entity, ct);
            return new ApiResponse<Guid>(entity.Id);
        }
        catch (Exception ex)
        {
            var response = new ApiResponse<Guid>("An error occurred while updating the record.", (int)HttpStatusCode.InternalServerError);
            response.Errors.Add(ex.InnerException?.Message ?? ex.Message);
            return response;
        }
    }
}
