using Application.Common;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Mappings;
using Application.Services;
using Application.ViewModels.Common;
using Application.ViewModels.Customer;
using Domain.Entities;

namespace Infrastructure.Services;

public class CustomerService<TCreate, TUpdate, TDetail>
    : BaseCrudService<Customer, TCreate, TUpdate, TDetail>, ICustomerService<TCreate, TUpdate, TDetail>
    where TCreate : class, IBaseCrudViewModel, new()
    where TUpdate : class, IBaseCrudViewModel, IIdentification, new()
    where TDetail : class, IBaseCrudViewModel, new()
{
    private readonly ICustomerRepository _customerRepo;

    public CustomerService(ICustomerRepository repo, IObjectMapper mapper) : base(repo, mapper)
    {
        _customerRepo = repo;
    }

    protected override string EntityName => "Customer";

    public async Task<ApiResponse<CustomerLedgerViewModel>> GetLedgerAsync(Guid id, CancellationToken ct = default)
    {
        var ledger = await _customerRepo.GetLedgerAsync(id, ct);
        return ledger == null
            ? ApiResponse<CustomerLedgerViewModel>.NotFound("Customer not found.")
            : ApiResponse<CustomerLedgerViewModel>.Ok(ledger);
    }

    public override async Task<ApiResponse<Guid>> CreateAsync(TCreate vm, CancellationToken ct = default)
    {
        if (vm is CreateCustomerViewModel c && await _customerRepo.PhoneExistsAsync(c.Phone.Trim(), null, ct))
            return ApiResponse<Guid>.Conflict($"A customer with phone {c.Phone} already exists in this branch.");
        return await base.CreateAsync(vm, ct);
    }

    public override async Task<ApiResponse<Guid>> UpdateAsync(TUpdate vm, CancellationToken ct = default)
    {
        if (vm is UpdateCustomerViewModel u && u.Phone != null && await _customerRepo.PhoneExistsAsync(u.Phone.Trim(), u.Id, ct))
            return ApiResponse<Guid>.Conflict($"A customer with phone {u.Phone} already exists in this branch.");
        return await base.UpdateAsync(vm, ct);
    }
}
