using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Services;
using Application.ViewModels.Common;
using Application.ViewModels.Customer;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Services;

public class CustomerService<TCreate, TUpdate, TDetail> 
    : BaseCrudService<Customer, TCreate, TUpdate, TDetail>, ICustomerService<TCreate, TUpdate, TDetail>
    where TCreate : class, IBaseCrudViewModel, new()
    where TUpdate : class, IBaseCrudViewModel, IIdentification, new()
    where TDetail : class, IBaseCrudViewModel, new()
{
    private readonly ICustomerRepository _customerRepo;
    private readonly ICurrentUserService _currentUser;

    public CustomerService(ICustomerRepository repo, IMapper mapper, ICurrentUserService currentUser)
        : base(repo, mapper)
    {
        _customerRepo = repo;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<CustomerLedgerViewModel>> GetLedgerAsync(Guid id, CancellationToken ct = default)
    {
        var dto = await _customerRepo.GetLedgerAsync(id, ct);
        if (dto == null) return new ApiResponse<CustomerLedgerViewModel>("Customer not found", 404);

        var vm = new CustomerLedgerViewModel
        {
            CustomerId = dto.CustomerId,
            Name = dto.Name,
            Phone = dto.Phone,
            TotalGrand = dto.TotalGrand,
            TotalPaid = dto.TotalPaid,
            TotalDue = dto.TotalDue,
            Orders = dto.Orders.Select(o => new CustomerLedgerOrderViewModel
            {
                OrderId = o.OrderId,
                OrderNumber = o.OrderNumber,
                GrandTotal = o.GrandTotal,
                AmountPaid = o.AmountPaid,
                BalanceDue = o.BalanceDue,
                Status = o.Status,
                DeliveryDate = o.DeliveryDate
            }).ToList()
        };

        return new ApiResponse<CustomerLedgerViewModel>(vm);
    }

    public override async Task<ApiResponse<Guid>> CreateAsync(
        TCreate vm, CancellationToken ct = default)
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
        return new ApiResponse<Guid>(entity.Id);
    }

    public override async Task<ApiResponse<Guid>> UpdateAsync(
        TUpdate vm, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(vm.Id, ct);
        if (entity == null) return new ApiResponse<Guid>("Customer not found.", 404);
        _mapper.Map(vm, entity);
        entity.UpdatedBy = _currentUser.UserId;
        entity.UpdatedOn = DateTime.UtcNow;
        await _repo.UpdateAsync(entity, ct);
        return new ApiResponse<Guid>(entity.Id);
    }

    public override async Task<ApiResponse<string>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity == null) return new ApiResponse<string>("Customer not found.", 404);
        await _repo.DeleteAsync(id, ct);
        return new ApiResponse<string>(id.ToString(), "Customer deleted.");
    }
}

