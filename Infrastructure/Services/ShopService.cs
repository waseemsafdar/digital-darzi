using Application.Common;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.ViewModels.Shop;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Services;

public class ShopService : IShopService
{
    private readonly IShopRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public ShopService(IShopRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<PagedResult<ShopListViewModel>>> GetListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var result = await _repo.GetListAsync(page, pageSize, ct);
        return ApiResponse<PagedResult<ShopListViewModel>>.Ok(result);
    }

    public async Task<ApiResponse<ShopDetailViewModel>> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var detail = await _repo.GetDetailAsync(id, ct);
        return detail == null
            ? ApiResponse<ShopDetailViewModel>.Fail("Shop not found.")
            : ApiResponse<ShopDetailViewModel>.Ok(detail);
    }

    public async Task<ApiResponse<ShopDetailViewModel>> CreateAsync(CreateShopViewModel vm, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var shop = new Shop
        {
            Id             = Guid.NewGuid(),
            TenantId       = _currentUser.TenantId,
            BranchId       = _currentUser.ShopId,
            Name           = vm.Name,
            Address        = vm.Address,
            City           = vm.City,
            Phone          = vm.Phone,
            WhatsAppNumber = vm.WhatsAppNumber,
            LogoUrl        = vm.LogoUrl,
            Currency       = vm.Currency,
            CurrencySymbol = vm.CurrencySymbol,
            ActiveStatus   = ActiveStatus.Active,
            CreatedBy      = _currentUser.UserId,
            CreatedOn      = now,
            UpdatedBy      = _currentUser.UserId,
            UpdatedOn      = now
        };

        await _repo.CreateAsync(shop, ct);
        var detail = await _repo.GetDetailAsync(shop.Id, ct);
        return ApiResponse<ShopDetailViewModel>.Ok(detail);
    }

    public async Task<ApiResponse<ShopDetailViewModel>> UpdateAsync(Guid id, UpdateShopViewModel vm, CancellationToken ct = default)
    {
        var shop = await _repo.GetByIdAsync(id, ct);
        if (shop == null) return ApiResponse<ShopDetailViewModel>.Fail("Shop not found.");

        if (vm.Name != null) shop.Name = vm.Name;
        if (vm.Address != null) shop.Address = vm.Address;
        if (vm.City != null) shop.City = vm.City;
        if (vm.Phone != null) shop.Phone = vm.Phone;
        if (vm.WhatsAppNumber != null) shop.WhatsAppNumber = vm.WhatsAppNumber;
        if (vm.LogoUrl != null) shop.LogoUrl = vm.LogoUrl;
        if (vm.Currency != null) shop.Currency = vm.Currency;
        if (vm.CurrencySymbol != null) shop.CurrencySymbol = vm.CurrencySymbol;
        if (vm.ActiveStatus.HasValue) shop.ActiveStatus = vm.ActiveStatus.Value;

        shop.UpdatedBy = _currentUser.UserId;
        shop.UpdatedOn = DateTime.UtcNow;

        await _repo.UpdateAsync(shop, ct);
        var detail = await _repo.GetDetailAsync(shop.Id, ct);
        return ApiResponse<ShopDetailViewModel>.Ok(detail);
    }

    public async Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var shop = await _repo.GetByIdAsync(id, ct);
        if (shop == null) return ApiResponse<object>.Fail("Shop not found.");
        await _repo.DeleteAsync(id, ct);
        return ApiResponse<object>.Ok((object?)null, "Shop deleted.");
    }
}
