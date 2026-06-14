using Application.Common;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.ViewModels.Customer;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Services;

public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public CustomerService(ICustomerRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<PagedResult<CustomerListViewModel>>> SearchAsync(CustomerSearchViewModel filter, CancellationToken ct = default)
        => ApiResponse<PagedResult<CustomerListViewModel>>.Ok(await _repo.SearchAsync(filter, ct));

    public async Task<ApiResponse<CustomerDetailViewModel>> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var detail = await _repo.GetDetailAsync(id, ct);
        return detail == null
            ? ApiResponse<CustomerDetailViewModel>.Fail("Customer not found.")
            : ApiResponse<CustomerDetailViewModel>.Ok(detail);
    }

    public async Task<ApiResponse<CustomerLedgerViewModel>> GetLedgerAsync(Guid id, CancellationToken ct = default)
    {
        var ledger = await _repo.GetLedgerAsync(id, ct);
        return ledger == null
            ? ApiResponse<CustomerLedgerViewModel>.Fail("Customer not found.")
            : ApiResponse<CustomerLedgerViewModel>.Ok(ledger);
    }

    public async Task<ApiResponse<CustomerDetailViewModel>> CreateAsync(CreateCustomerViewModel vm, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Id          = Guid.NewGuid(),
            TenantId    = _currentUser.TenantId,
            BranchId    = _currentUser.ShopId,
            Name        = vm.Name,
            Phone       = vm.Phone,
            Email       = vm.Email,
            Address     = vm.Address,
            City        = vm.City,
            Gender      = vm.Gender,
            DateOfBirth = vm.DateOfBirth,
            Notes       = vm.Notes,
            ActiveStatus= ActiveStatus.Active,
            CreatedBy   = _currentUser.UserId,
            CreatedOn   = now,
            UpdatedBy   = _currentUser.UserId,
            UpdatedOn   = now
        };

        await _repo.CreateAsync(customer, ct);
        var detail = await _repo.GetDetailAsync(customer.Id, ct);
        return ApiResponse<CustomerDetailViewModel>.Ok(detail);
    }

    public async Task<ApiResponse<CustomerDetailViewModel>> UpdateAsync(Guid id, UpdateCustomerViewModel vm, CancellationToken ct = default)
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
        var detail = await _repo.GetDetailAsync(customer.Id, ct);
        return ApiResponse<CustomerDetailViewModel>.Ok(detail);
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var customer = await _repo.GetByIdAsync(id, ct);
        if (customer == null) return ApiResponse<object>.Fail("Customer not found.");
        await _repo.DeleteAsync(id, ct);
        return ApiResponse<object>.Ok((object?)null, "Customer deleted.");
    }
}
