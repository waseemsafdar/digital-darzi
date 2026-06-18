using Application.Common;
using Application.Interfaces;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Services;
using Application.ViewModels.Common;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using System.Net;

namespace Infrastructure.Services;

public class ShopService<TCreate, TUpdate, TDetail> :
    BaseCrudService<Shop, TCreate, TUpdate, TDetail>,
    IShopService<TCreate, TUpdate, TDetail>
    where TCreate : class, IBaseCrudViewModel, new()
    where TUpdate : class, IBaseCrudViewModel, IIdentification, new()
    where TDetail : class, IBaseCrudViewModel, new()
{
    private readonly IShopRepository _shopRepo;
    private readonly ICurrentUserService _currentUser;

    public ShopService(IShopRepository repo, ICurrentUserService currentUser, IMapper mapper)
        : base(repo, mapper)
    {
        _shopRepo    = repo;
        _currentUser = currentUser;
    }

    public override async Task<ApiResponse<TDetail>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var dto = await _shopRepo.GetDetailAsync(id, ct);
        if (dto == null) return new ApiResponse<TDetail>("Shop not found", 404);
        
        return new ApiResponse<TDetail>(_mapper.Map<TDetail>(dto));
    }

    public override async Task<ApiResponse<Guid>> CreateAsync(TCreate vm, CancellationToken ct = default)
    {
        var entity = _mapper.Map<Shop>(vm);
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

    public override async Task<ApiResponse<Guid>> UpdateAsync(TUpdate vm, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(vm.Id, ct);
        if (entity == null) return new ApiResponse<Guid>("Shop not found.", 404);
        _mapper.Map(vm, entity);
        entity.UpdatedBy = _currentUser.UserId;
        entity.UpdatedOn = DateTime.UtcNow;
        await _repo.UpdateAsync(entity, ct);
        return new ApiResponse<Guid>(entity.Id);
    }

    public async Task<ApiResponse<bool>> UpdateSubscriptionAsync(Guid id, Application.ViewModels.Shop.UpdateShopSubscriptionRequest request, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity == null) return new ApiResponse<bool>("Shop not found.", 404);

        entity.Status = request.Status;
        entity.SubscriptionEndsAt = request.SubscriptionEndsAt;
        entity.SubscriptionPlanName = request.SubscriptionPlanName;
        entity.UpdatedBy = _currentUser.UserId;
        entity.UpdatedOn = DateTime.UtcNow;

        await _repo.UpdateAsync(entity, ct);
        return new ApiResponse<bool>(true, "Subscription updated successfully.");
    }
}

