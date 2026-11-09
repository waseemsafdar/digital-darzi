using Application.Interfaces.Services;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public enum PlanLimit
{
    Branches,
    Users,
    OrdersPerMonth
}

/// <summary>Parts of the app a plan can leave out (the free plan does).</summary>
public enum PlanFeature
{
    Photos,
    Reports
}

public interface IPlanLimitService
{
    /// <summary>Null when the tenant may create one more of <paramref name="limit"/>; otherwise the reason.</summary>
    Task<string?> CheckAsync(PlanLimit limit, CancellationToken ct = default);

    /// <summary>Null when the tenant's current plan includes <paramref name="feature"/>; otherwise the reason.</summary>
    Task<string?> CheckFeatureAsync(PlanFeature feature, CancellationToken ct = default);
}

/// <summary>
/// Enforces the limits of the plan that applies now (see <see cref="ISubscriptionResolver"/>):
/// the assigned plan during a trial or paid period, the free plan once that period has ended.
/// A tenant with no plan is unlimited, which is how every tenant provisioned before plans existed behaves.
/// </summary>
public sealed class PlanLimitService : IPlanLimitService
{
    private readonly ApplicationDbContext _db;
    private readonly ISubscriptionResolver _subscriptions;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _clock;

    public PlanLimitService(ApplicationDbContext db, ISubscriptionResolver subscriptions, ICurrentUserService currentUser, TimeProvider clock)
    {
        _db = db;
        _subscriptions = subscriptions;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<string?> CheckAsync(PlanLimit limit, CancellationToken ct = default)
    {
        var plan = await CurrentPlanAsync(ct);
        if (plan == null) return null;

        switch (limit)
        {
            case PlanLimit.Branches when plan.MaxBranches.HasValue:
            {
                var count = await _db.Shops.CountAsync(ct);   // tenant-filtered
                return count >= plan.MaxBranches ? $"Your '{plan.Name}' plan allows {plan.MaxBranches} branch(es). Upgrade to add more." : null;
            }
            case PlanLimit.Users when plan.MaxUsers.HasValue:
            {
                var count = await _db.AppUsers.CountAsync(u => u.ActiveStatus == ActiveStatus.Active, ct);
                return count >= plan.MaxUsers ? $"Your '{plan.Name}' plan allows {plan.MaxUsers} active user(s). Upgrade or deactivate a user first." : null;
            }
            case PlanLimit.OrdersPerMonth when plan.MaxOrdersPerMonth.HasValue:
            {
                var now = _clock.GetUtcNow().UtcDateTime;
                var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var count = await _db.TenantWide<Domain.Entities.Order>().CountAsync(o => o.CreatedOn >= monthStart, ct);
                return count >= plan.MaxOrdersPerMonth
                    ? $"Your '{plan.Name}' plan allows {plan.MaxOrdersPerMonth} orders per month. Upgrade to create more this month."
                    : null;
            }
            default:
                return null;
        }
    }

    public async Task<string?> CheckFeatureAsync(PlanFeature feature, CancellationToken ct = default)
    {
        var plan = await CurrentPlanAsync(ct);
        if (plan == null) return null;
        return feature switch
        {
            PlanFeature.Photos when !plan.IncludesPhotos => $"Photos are not part of the '{plan.Name}' plan. Upgrade to add design and fabric photos.",
            PlanFeature.Reports when !plan.IncludesReports => $"Reports are not part of the '{plan.Name}' plan. Upgrade to see them.",
            _ => null
        };
    }

    private async Task<Domain.Entities.SubscriptionPlan?> CurrentPlanAsync(CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == Guid.Empty || _currentUser.IsSystemAdmin) return null;
        return (await _subscriptions.ResolveAsync(tenantId, _currentUser.ShopId, ct)).EffectivePlan;
    }
}
