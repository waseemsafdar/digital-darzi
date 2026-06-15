using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Services;
using Application.ViewModels.Common;
using AutoMapper;
using Domain.Entities;
using System.Net;

namespace Infrastructure.Services;

public class StaffSalaryService<TCreate, TUpdate, TDetail>
    : BaseCrudService<StaffSalary, TCreate, TUpdate, TDetail>,
      IStaffSalaryService<TCreate, TUpdate, TDetail>
    where TCreate : class, IBaseCrudViewModel, new()
    where TUpdate : class, IBaseCrudViewModel, IIdentification, new()
    where TDetail : class, IBaseCrudViewModel, new()
{
    private readonly IStaffSalaryRepository _salaryRepo;
    private readonly ICurrentUserService _currentUser;

    public StaffSalaryService(IStaffSalaryRepository repo, ICurrentUserService currentUser, IMapper mapper)
        : base(repo, mapper)
    {
        _salaryRepo  = repo;
        _currentUser = currentUser;
    }

    public override async Task<ApiResponse<Guid>> CreateAsync(
        TCreate vm, CancellationToken ct = default)
    {
        try
        {
            var entity = _mapper.Map<StaffSalary>(vm);
            entity.ShopId    = _currentUser.ShopId;
            entity.CreatedBy = _currentUser.UserId;
            entity.CreatedOn = DateTime.UtcNow;
            entity.ActiveStatus = Domain.Enums.ActiveStatus.Active;
            
            var created = await _repo.AddAsync(entity, ct);
            return new ApiResponse<Guid>(created.Id);
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
            if (entity == null) 
                return new ApiResponse<Guid>("Salary record not found.", (int)HttpStatusCode.NotFound);
                
            _mapper.Map(vm, entity);
            // Recalculate net after partial update
            entity.NetSalary = entity.BaseSalary + entity.Bonus - entity.Deduction;
            entity.UpdatedBy = _currentUser.UserId;
            entity.UpdatedOn = DateTime.UtcNow;
            
            await _repo.UpdateAsync(entity, ct);
            return new ApiResponse<Guid>(vm.Id);
        }
        catch (Exception ex)
        {
            var response = new ApiResponse<Guid>("An error occurred while updating the record.", (int)HttpStatusCode.InternalServerError);
            response.Errors.Add(ex.InnerException?.Message ?? ex.Message);
            return response;
        }
    }
}
