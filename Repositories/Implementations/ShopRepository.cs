using Application.Common;
using Application.Interfaces.Repositories;
using Application.ViewModels.Shop;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Implementations;

public class ShopRepository : IShopRepository
{
    private readonly ApplicationDbContext _db;
    public ShopRepository(ApplicationDbContext db) => _db = db;

    public async Task<Shop?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Shops.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<PagedResult<ShopListViewModel>> GetListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Shops.AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new ShopListViewModel
            {
                Id           = s.Id,
                Name         = s.Name,
                City         = s.City,
                Phone        = s.Phone,
                ActiveStatus = s.ActiveStatus,
                CreatedOn    = s.CreatedOn
            })
            .ToListAsync(ct);

        return PagedResult<ShopListViewModel>.From(items, total, page, pageSize);
    }

    public async Task<ShopDetailViewModel?> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var s = await _db.Shops
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s == null) return null;

        // Try to fetch owner name from AppUsers
        string? ownerName = null;
        if (s.OwnerId.HasValue)
        {
            ownerName = await _db.AppUsers
                .IgnoreQueryFilters()
                .Where(u => u.Id == s.OwnerId)
                .Select(u => u.Name)
                .FirstOrDefaultAsync(ct);
        }

        return new ShopDetailViewModel
        {
            Id              = s.Id,
            Name            = s.Name,
            Address         = s.Address,
            City            = s.City,
            Phone           = s.Phone,
            WhatsAppNumber  = s.WhatsAppNumber,
            LogoUrl         = s.LogoUrl,
            Currency        = s.Currency,
            CurrencySymbol  = s.CurrencySymbol,
            OwnerId         = s.OwnerId,
            OwnerName       = ownerName,
            ActiveStatus    = s.ActiveStatus,
            CreatedOn       = s.CreatedOn
        };
    }

    public async Task<Shop> CreateAsync(Shop shop, CancellationToken ct = default)
    {
        _db.Shops.Add(shop);
        await _db.SaveChangesAsync(ct);
        return shop;
    }

    public async Task UpdateAsync(Shop shop, CancellationToken ct = default)
    {
        _db.Shops.Update(shop);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var shop = await GetByIdAsync(id, ct);
        if (shop == null) return;
        shop.IsDeleted = true;
        shop.ActiveStatus = ActiveStatus.Inactive;
        await _db.SaveChangesAsync(ct);
    }
}
