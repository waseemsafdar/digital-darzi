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

public interface IPlanLimitService
{
    /// <summary>Null when the tenant may create one more of <paramref name="limit"/>; otherwise the reason.</summary>
    Task<string?> CheckAsync(PlanLimit limit, CancellationToken ct = default);
}

/// <summary>
/// Enforces the limits of the tenant's subscription plan. The plan comes from the governing
/// branch (see <see cref="IShopSettingsCache.GetGoverningSubscriptionAsync"/>); a tenant with no
/// plan assigned is unlimited, which is how every tenant provisioned before plans existed behaves.
/// </summary>
public sealed class PlanLimitService : IPlanLimitService
{
    private readonly ApplicationDbContext _db;
    private readonly IShopSettingsCache _shops;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _clock;

    public PlanLimitService(ApplicationDbContext db, IShopSettingsCache shops, ICurrentUserService currentUser, TimeProvider clock)
    {
        _db = db;
        _shops = shops;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<string?> CheckAsync(PlanLimit limit, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;
        if (tenantId == Guid.Empty || _currentUser.IsSystemAdmin) return null;

        var governing = await _shops.GetGoverningSubscriptionAsync(tenantId, _currentUser.ShopId, ct);
        if (governing?.SubscriptionPlanId == null) return null;

        var plan = await _db.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == governing.SubscriptionPlanId, ct);
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
}
