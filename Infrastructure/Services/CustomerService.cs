using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.ViewModels.Customer;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Services;

public class CustomerService : BaseCrudService<Customer, CreateCustomerViewModel, UpdateCustomerViewModel, CustomerDetailViewModel>, ICustomerService
{
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _uow;

    public CustomerService(ICustomerRepository repo, ICurrentUserService currentUser, IUnitOfWork uow)
        : base(repo)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<ApiResponse<PagedResult<CustomerListViewModel>>> SearchAsync(CustomerSearchViewModel filter, CancellationToken ct = default)
        => ApiResponse<PagedResult<CustomerListViewModel>>.Ok(await ((ICustomerRepository)_repo).SearchAsync(filter, ct));

    public async Task<ApiResponse<CustomerLedgerViewModel>> GetLedgerAsync(Guid id, CancellationToken ct = default)
    {
        var ledger = await ((ICustomerRepository)_repo).GetLedgerAsync(id, ct);
        return ledger == null
            ? ApiResponse<CustomerLedgerViewModel>.Fail("Customer not found.")
            : ApiResponse<CustomerLedgerViewModel>.Ok(ledger);
    }

    public override async Task<ApiResponse<CustomerDetailViewModel>> CreateAsync(CreateCustomerViewModel vm, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            BranchId = _currentUser.ShopId,
            Name = vm.Name,
            Phone = vm.Phone,
            Email = vm.Email,
            Address = vm.Address,
            City = vm.City,
            Gender = vm.Gender,
            DateOfBirth = vm.DateOfBirth,
            Notes = vm.Notes,
            ActiveStatus = ActiveStatus.Active,
            CreatedBy = _currentUser.UserId,
            CreatedOn = now,
            UpdatedBy = _currentUser.UserId,
            UpdatedOn = now
        };
        await _repo.AddAsync(customer, ct);
        var detail = await ((ICustomerRepository)_repo).GetDetailAsync(customer.Id, ct);
        return ApiResponse<CustomerDetailViewModel>.Ok(detail);
    }

    public override async Task<ApiResponse<CustomerDetailViewModel>> UpdateAsync(Guid id, UpdateCustomerViewModel vm, CancellationToken ct = default)
    {
        var customer = await _repo.GetByIdAsync(id, ct);
        if (customer == null) return ApiResponse<CustomerDetailViewModel>.Fail("Customer not found.");
        if (vm.Name != null) customer.Name = vm.Name;
        if (vm.Phone != null) customer.Phone = vm.Phone;
        if (vm.Email != null) customer.Email = vm.Email;
        if (vm.Address != null) customer.Address = vm.Address;
        if (vm.City != null) customer.City = vm.City;
        if (vm.Gender.HasValue) customer.Gender = vm.Gender.Value;
        if (vm.DateOfBirth.HasValue) customer.DateOfBirth = vm.DateOfBirth;
        if (vm.Notes != null) customer.Notes = vm.Notes;
        if (vm.ActiveStatus.HasValue) customer.ActiveStatus = vm.ActiveStatus.Value;
        customer.UpdatedBy = _currentUser.UserId;
        customer.UpdatedOn = DateTime.UtcNow;
        await _repo.UpdateAsync(customer, ct);
        var detail = await ((ICustomerRepository)_repo).GetDetailAsync(customer.Id, ct);
        return ApiResponse<CustomerDetailViewModel>.Ok(detail);
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var customer = await _repo.GetByIdAsync(id, ct);
        if (customer == null) return ApiResponse<object>.Fail("Customer not found.");
        await _repo.DeleteAsync(id, ct);
        return ApiResponse<object>.Ok(null, "Customer deleted.");
    }
}
