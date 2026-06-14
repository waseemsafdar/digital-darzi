using Application.Common;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.ViewModels.Shop;
using Domain.Entities;
using Domain.Enums;
using Application.Interfaces;

namespace Infrastructure.Services;

public class ShopService :
    BaseCrudService<Shop, CreateShopViewModel, UpdateShopViewModel, ShopDetailViewModel>,
    IShopService
{
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _uow;

    public ShopService(IShopRepository repo, ICurrentUserService currentUser, IUnitOfWork uow)
        : base(repo)
    {
        _currentUser = currentUser;
        _uow = uow;
    }

    public async Task<ApiResponse<PagedResult<ShopListViewModel>>> GetListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var result = await ((IShopRepository)_repo).GetListAsync(page, pageSize, ct);
        return ApiResponse<PagedResult<ShopListViewModel>>.Ok(result);
    }

    protected override ShopDetailViewModel MapToDetail(Shop entity) => new()
    {
        Id = entity.Id,
        TenantId = entity.TenantId,
        BranchId = entity.BranchId,
        Name = entity.Name,
        Address = entity.Address,
        City = entity.City,
        Phone = entity.Phone,
        WhatsAppNumber = entity.WhatsAppNumber,
        LogoUrl = entity.LogoUrl,
        Currency = entity.Currency,
        CurrencySymbol = entity.CurrencySymbol,
        ActiveStatus = entity.ActiveStatus,
        CreatedBy = entity.CreatedBy,
        CreatedOn = entity.CreatedOn,
        UpdatedBy = entity.UpdatedBy,
        UpdatedOn = entity.UpdatedOn
    };

    protected override Shop MapFromCreate(CreateShopViewModel vm)
    {
        var now = DateTime.UtcNow;
        return new Shop
        {
            Id = Guid.NewGuid(),
            TenantId = _currentUser.TenantId,
            BranchId = _currentUser.ShopId,
            Name = vm.Name,
            Address = vm.Address,
            City = vm.City,
            Phone = vm.Phone,
            WhatsAppNumber = vm.WhatsAppNumber,
            LogoUrl = vm.LogoUrl,
            Currency = vm.Currency,
            CurrencySymbol = vm.CurrencySymbol,
            ActiveStatus = ActiveStatus.Active,
            CreatedBy = _currentUser.UserId,
            CreatedOn = now,
            UpdatedBy = _currentUser.UserId,
            UpdatedOn = now
        };
    }

    protected override void ApplyUpdate(Shop entity, UpdateShopViewModel vm)
    {
        if (vm.Name != null) entity.Name = vm.Name;
        if (vm.Address != null) entity.Address = vm.Address;
        if (vm.City != null) entity.City = vm.City;
        if (vm.Phone != null) entity.Phone = vm.Phone;
        if (vm.WhatsAppNumber != null) entity.WhatsAppNumber = vm.WhatsAppNumber;
        if (vm.LogoUrl != null) entity.LogoUrl = vm.LogoUrl;
        if (vm.Currency != null) entity.Currency = vm.Currency;
        if (vm.CurrencySymbol != null) entity.CurrencySymbol = vm.CurrencySymbol;
        if (vm.ActiveStatus.HasValue) entity.ActiveStatus = vm.ActiveStatus.Value;
        entity.UpdatedBy = _currentUser.UserId;
        entity.UpdatedOn = DateTime.UtcNow;
    }
}
