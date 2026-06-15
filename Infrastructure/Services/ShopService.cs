using Application.Common;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Services;
using Application.ViewModels.Shop;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Services;

public class ShopService :
    BaseCrudService<Shop, CreateShopViewModel, UpdateShopViewModel, ShopDetailViewModel>,
    IShopService
{
    private readonly IShopRepository _shopRepo;
    private readonly ICurrentUserService _currentUser;

    public ShopService(IShopRepository repo, ICurrentUserService currentUser, IMapper mapper)
        : base(repo, mapper)
    {
        _shopRepo    = repo;
        _currentUser = currentUser;
    }

    public override async Task<ApiResponse<ShopDetailViewModel>> CreateAsync(CreateShopViewModel vm, CancellationToken ct = default)
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
        return new ApiResponse<ShopDetailViewModel>(_mapper.Map<ShopDetailViewModel>(entity), "Created successfully.");
    }

    public override async Task<ApiResponse<ShopDetailViewModel>> UpdateAsync(
        UpdateShopViewModel vm, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(vm.Id, ct);
        if (entity == null) return new ApiResponse<ShopDetailViewModel>("Shop not found.", 404);
        _mapper.Map(vm, entity);
        entity.UpdatedBy = _currentUser.UserId;
        entity.UpdatedOn = DateTime.UtcNow;
        await _repo.UpdateAsync(entity, ct);
        return new ApiResponse<ShopDetailViewModel>(_mapper.Map<ShopDetailViewModel>(entity), "Updated successfully.");
    }

    public async Task<ApiResponse<PagedResult<ShopListViewModel>>> GetListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var result = await _shopRepo.GetListAsync(page, pageSize, ct);
        return new ApiResponse<PagedResult<ShopListViewModel>>(result);
    }
}
