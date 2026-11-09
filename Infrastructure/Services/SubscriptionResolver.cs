using Application.ViewModels.Admin;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services;

/// <summary>A business's subscription as it applies right now.</summary>
public sealed record ResolvedSubscription(
    string State,
    ShopSettings? Governing,
    SubscriptionPlan? EffectivePlan,
    int? DaysLeft,
    string? BlockReason);

public interface ISubscriptionResolver
{
    Task<ResolvedSubscription> ResolveAsync(Guid tenantId, Guid shopId, CancellationToken ct = default);
    Task<SubscriptionPlan?> GetFreePlanAsync(CancellationToken ct = default);
    void InvalidatePlans();
}

/// <summary>
/// Rules (2026-11): a trial or paid period that has ended drops the business to the free plan
/// instead of locking it out, as long as a free plan exists. Suspended or cancelled businesses
/// are always locked. Businesses with no subscription data are never limited.
/// </summary>
public sealed class SubscriptionResolver : ISubscriptionResolver
{
    private const string PlansKey = "subscription-plans";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(2);

    private readonly ApplicationDbContext _db;
    private readonly IShopSettingsCache _shops;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _clock;

    public SubscriptionResolver(ApplicationDbContext db, IShopSettingsCache shops, IMemoryCache cache, TimeProvider clock)
    {
        _db = db;
        _shops = shops;
        _cache = cache;
        _clock = clock;
    }

    public async Task<ResolvedSubscription> ResolveAsync(Guid tenantId, Guid shopId, CancellationToken ct = default)
    {
        var governing = await _shops.GetGoverningSubscriptionAsync(tenantId, shopId, ct);
        var plans = await PlansAsync(ct);
        var free = plans.FirstOrDefault(p => p.IsFreeTier && p.IsActive);
        var now = _clock.GetUtcNow().UtcDateTime;
        var (state, reason) = Classify(governing, free != null, now);

        var plan = state switch
        {
            SubscriptionStates.Free => free,
            SubscriptionStates.Trial or SubscriptionStates.Active => plans.FirstOrDefault(p => p.Id == governing?.SubscriptionPlanId),
            _ => null
        };
        var ends = state switch
        {
            SubscriptionStates.Trial => governing?.TrialEndsAt,
            SubscriptionStates.Active => governing?.SubscriptionEndsAt,
            _ => null
        };
        int? daysLeft = ends.HasValue ? Math.Max(0, (int)Math.Ceiling((ends.Value - now).TotalDays)) : null;
        return new ResolvedSubscription(state, governing, plan, daysLeft, reason);
    }

    public async Task<SubscriptionPlan?> GetFreePlanAsync(CancellationToken ct = default)
        => (await PlansAsync(ct)).FirstOrDefault(p => p.IsFreeTier && p.IsActive);

    public void InvalidatePlans() => _cache.Remove(PlansKey);

    /// <summary>The state and, when locked, the message to show.</summary>
    public static (string State, string? BlockReason) Classify(ShopSettings? governing, bool freePlanExists, DateTime utcNow)
    {
        if (governing?.Status == null) return (SubscriptionStates.Legacy, null);
        switch (governing.Status)
        {
            case SubscriptionStatus.Suspended:
            case SubscriptionStatus.Cancelled:
                return (SubscriptionStates.Blocked, "Your shop's subscription is suspended or cancelled.");
            case SubscriptionStatus.Trial when governing.TrialEndsAt.HasValue && governing.TrialEndsAt.Value < utcNow:
                return freePlanExists
                    ? (SubscriptionStates.Free, null)
                    : (SubscriptionStates.Blocked, "Your trial has expired. Please contact support to upgrade to a paid plan.");
            case SubscriptionStatus.Active when governing.SubscriptionEndsAt.HasValue && governing.SubscriptionEndsAt.Value < utcNow:
                return freePlanExists
                    ? (SubscriptionStates.Free, null)
                    : (SubscriptionStates.Blocked, "Your subscription has expired. Please renew to continue using the system.");
            case SubscriptionStatus.Trial:
                return (SubscriptionStates.Trial, null);
            default:
                return (SubscriptionStates.Active, null);
        }
    }

    private async Task<List<SubscriptionPlan>> PlansAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(PlansKey, out List<SubscriptionPlan>? cached) && cached != null) return cached;
        var plans = await _db.SubscriptionPlans.AsNoTracking().ToListAsync(ct);
        _cache.Set(PlansKey, plans, Ttl);
        return plans;
    }
}
