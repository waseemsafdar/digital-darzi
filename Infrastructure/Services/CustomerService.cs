using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Services;
using Application.ViewModels.Customer;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Services;

public class CustomerService
    : BaseCrudService<Customer, CreateCustomerViewModel, UpdateCustomerViewModel, CustomerDetailViewModel>,
      ICustomerService
{
    private readonly ICustomerRepository _customerRepo;
    private readonly ICurrentUserService _currentUser;

    public CustomerService(ICustomerRepository repo, ICurrentUserService currentUser, IMapper mapper)
        : base(repo, mapper)
    {
        _customerRepo = repo;
        _currentUser  = currentUser;
    }

    public async Task<ApiResponse<PagedResult<CustomerListViewModel>>> SearchAsync(
        CustomerSearchViewModel filter, CancellationToken ct = default)
    {
        var result = await _customerRepo.SearchAsync(filter, ct);
        return new ApiResponse<PagedResult<CustomerListViewModel>>(result);
    }

    public async Task<ApiResponse<CustomerLedgerViewModel>> GetLedgerAsync(Guid id, CancellationToken ct = default)
    {
        var ledger = await _customerRepo.GetLedgerAsync(id, ct);
        return ledger == null
            ? new ApiResponse<CustomerLedgerViewModel>("Customer not found.", 404)
            : new ApiResponse<CustomerLedgerViewModel>(ledger);
    }

    public override async Task<ApiResponse<CustomerDetailViewModel>> CreateAsync(
        CreateCustomerViewModel vm, CancellationToken ct = default)
    {
        var entity = _mapper.Map<Customer>(vm);
        entity.TenantId    = _currentUser.TenantId;
        entity.BranchId    = _currentUser.ShopId;
        entity.ActiveStatus= ActiveStatus.Active;
        entity.CreatedBy   = _currentUser.UserId;
        entity.CreatedOn   = DateTime.UtcNow;
        entity.UpdatedBy   = _currentUser.UserId;
        entity.UpdatedOn   = DateTime.UtcNow;
        await _repo.AddAsync(entity, ct);
        return new ApiResponse<CustomerDetailViewModel>(_mapper.Map<CustomerDetailViewModel>(entity), "Created successfully.");
    }

    public override async Task<ApiResponse<CustomerDetailViewModel>> UpdateAsync(
        UpdateCustomerViewModel vm, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(vm.Id, ct);
        if (entity == null) return new ApiResponse<CustomerDetailViewModel>("Customer not found.", 404);
        _mapper.Map(vm, entity);
        entity.UpdatedBy = _currentUser.UserId;
        entity.UpdatedOn = DateTime.UtcNow;
        await _repo.UpdateAsync(entity, ct);
        return new ApiResponse<CustomerDetailViewModel>(_mapper.Map<CustomerDetailViewModel>(entity), "Updated successfully.");
    }

    public override async Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity == null) return new ApiResponse<object>("Customer not found.", 404);
        await _repo.DeleteAsync(id, ct);
        return new ApiResponse<object>(null, "Customer deleted.");
    }
}
