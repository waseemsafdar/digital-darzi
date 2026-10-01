using Application.Common;
using Application.Interfaces.Services;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services;

/// <summary>Snapshot of the per-branch settings that most requests need.</summary>
public sealed record ShopSettings(
    Guid ShopId,
    Guid TenantId,
    string Name,
    string? Phone,
    string Currency,
    string CurrencySymbol,
    string TimeZoneId,
    bool NotificationsEnabled,
    SubscriptionStatus? Status,
    DateTime? TrialEndsAt,
    DateTime? SubscriptionEndsAt,
    Guid? SubscriptionPlanId)
{
    public TimeZoneInfo TimeZone => ShopCalendar.ResolveTimeZone(TimeZoneId);
}

public interface IShopSettingsCache
{
    Task<ShopSettings?> GetAsync(Guid shopId, CancellationToken ct = default);

    /// <summary>
    /// The branch whose subscription governs the tenant: the branch itself when it has a status,
    /// otherwise the tenant's earliest branch that does (branches added later inherit the plan).
    /// </summary>
    Task<ShopSettings?> GetGoverningSubscriptionAsync(Guid tenantId, Guid shopId, CancellationToken ct = default);

    void Invalidate(Guid shopId);
    void InvalidateTenant(Guid tenantId);
}

public sealed class ShopSettingsCache : IShopSettingsCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public ShopSettingsCache(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<ShopSettings?> GetAsync(Guid shopId, CancellationToken ct = default)
    {
        if (shopId == Guid.Empty) return null;
        if (_cache.TryGetValue(Key(shopId), out ShopSettings? cached)) return cached;

        var s = await _db.Shops.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == shopId && !x.IsDeleted)
            .Select(x => new ShopSettings(x.Id, x.TenantId, x.Name, x.Phone, x.Currency, x.CurrencySymbol,
                x.TimeZone, x.NotificationsEnabled, x.Status, x.TrialEndsAt, x.SubscriptionEndsAt, x.SubscriptionPlanId))
            .FirstOrDefaultAsync(ct);

        if (s != null) _cache.Set(Key(shopId), s, Ttl);
        return s;
    }

    public async Task<ShopSettings?> GetGoverningSubscriptionAsync(Guid tenantId, Guid shopId, CancellationToken ct = default)
    {
        var shop = await GetAsync(shopId, ct);
        if (shop != null && shop.TenantId == tenantId && shop.Status.HasValue) return shop;

        var key = TenantKey(tenantId);
        if (_cache.TryGetValue(key, out ShopSettings? governing)) return governing;

        var primaryId = await _db.Shops.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.Status != null)
            .OrderBy(x => x.CreatedOn)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);

        governing = primaryId.HasValue ? await GetAsync(primaryId.Value, ct) : null;
        _cache.Set(key, governing, Ttl);
        return governing;
    }

    public void Invalidate(Guid shopId) => _cache.Remove(Key(shopId));
    public void InvalidateTenant(Guid tenantId) => _cache.Remove(TenantKey(tenantId));

    private static string Key(Guid shopId) => $"shop-settings:{shopId}";
    private static string TenantKey(Guid tenantId) => $"tenant-subscription:{tenantId}";
}

public sealed class BusinessCalendar : IBusinessCalendar
{
    private readonly IShopSettingsCache _shops;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _clock;

    public BusinessCalendar(IShopSettingsCache shops, ICurrentUserService currentUser, TimeProvider clock)
    {
        _shops = shops;
        _currentUser = currentUser;
        _clock = clock;
    }

    public DateTime UtcNow => _clock.GetUtcNow().UtcDateTime;

    public async Task<TimeZoneInfo> GetTimeZoneAsync(CancellationToken ct = default)
    {
        var shop = await _shops.GetAsync(_currentUser.ShopId, ct);
        return ShopCalendar.ResolveTimeZone(shop?.TimeZoneId);
    }

    public async Task<DateTime> TodayAsync(CancellationToken ct = default)
        => ShopCalendar.Today(UtcNow, await GetTimeZoneAsync(ct));

    public async Task<TimeSpan> LocalTimeOfDayAsync(CancellationToken ct = default)
        => ShopCalendar.LocalTimeOfDay(UtcNow, await GetTimeZoneAsync(ct));

    public async Task<DateTime> ToCalendarDateAsync(DateTime value, CancellationToken ct = default)
        => ShopCalendar.ToCalendarDate(value, await GetTimeZoneAsync(ct));
}
