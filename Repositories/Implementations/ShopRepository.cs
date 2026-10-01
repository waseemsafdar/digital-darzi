using Application.Common;
using Application.Interfaces.Repositories;
using Application.ViewModels.Shop;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Implementations;

public class ShopRepository : BaseRepository<Shop>, IShopRepository
{
    public ShopRepository(ApplicationDbContext db) : base(db) { }

    protected override Task<IQueryable<Shop>> ApplyFiltersAsync(IQueryable<Shop> query, Application.ViewModels.Common.IBaseSearchModel search)
    {
        if (!string.IsNullOrWhiteSpace(search.Keyword))
        {
            var pattern = Like.Contains(search.Keyword);
            query = query.Where(s => EF.Functions.ILike(s.Name, pattern, "\\"));
        }
        return Task.FromResult<IQueryable<Shop>>(query.OrderBy(s => s.Name));
    }

    public async Task<PagedResult<ShopListViewModel>> GetListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var query = _db.Shops.AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new ShopListViewModel
            {
                Id = s.Id, Name = s.Name, City = s.City, Phone = s.Phone, ActiveStatus = s.ActiveStatus, CreatedOn = s.CreatedOn
            })
            .ToListAsync(ct);

        return PagedResult<ShopListViewModel>.From(items, total, page, pageSize);
    }

    public async Task<ShopDetailViewModel?> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var s = await _db.Shops.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s == null) return null;

        string? ownerName = null;
        if (s.OwnerId.HasValue)
            ownerName = await _db.AppUsers.AsNoTracking().Where(u => u.Id == s.OwnerId).Select(u => u.Name).FirstOrDefaultAsync(ct);

        return new ShopDetailViewModel
        {
            Id                   = s.Id,
            TenantId             = s.TenantId,
            BranchId             = s.BranchId,
            Name                 = s.Name,
            Address              = s.Address,
            City                 = s.City,
            Phone                = s.Phone,
            WhatsAppNumber       = s.WhatsAppNumber,
            LogoUrl              = s.LogoUrl,
            Currency             = s.Currency,
            CurrencySymbol       = s.CurrencySymbol,
            TimeZone             = s.TimeZone,
            NotificationsEnabled = s.NotificationsEnabled,
            OwnerId              = s.OwnerId,
            OwnerName            = ownerName,
            ActiveStatus         = s.ActiveStatus,
            Status               = s.Status,
            TrialEndsAt          = s.TrialEndsAt,
            SubscriptionEndsAt   = s.SubscriptionEndsAt,
            SubscriptionPlanName = s.SubscriptionPlanName,
            SubscriptionPlanId   = s.SubscriptionPlanId,
            CreatedOn            = s.CreatedOn,
            CreatedBy            = s.CreatedBy,
            UpdatedBy            = s.UpdatedBy,
            UpdatedOn            = s.UpdatedOn
        };
    }

    public override async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var shop = await GetByIdAsync(id, ct);
        if (shop == null) return;
        shop.IsDeleted = true;
        shop.ActiveStatus = ActiveStatus.Inactive;
        await _db.SaveChangesAsync(ct);
    }
}
