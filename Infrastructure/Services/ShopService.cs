using Application.Common;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Mappings;
using Application.Services;
using Application.ViewModels.Common;
using Application.ViewModels.Shop;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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
    private readonly ApplicationDbContext _db;
    private readonly ITenantSetupService _tenantSetup;
    private readonly IPlanLimitService _limits;
    private readonly IShopSettingsCache _cache;

    public ShopService(IShopRepository repo, ICurrentUserService currentUser, IObjectMapper mapper, ApplicationDbContext db,
        ITenantSetupService tenantSetup, IPlanLimitService limits, IShopSettingsCache cache)
        : base(repo, mapper)
    {
        _shopRepo    = repo;
        _currentUser = currentUser;
        _db          = db;
        _tenantSetup = tenantSetup;
        _limits      = limits;
        _cache       = cache;
    }

    protected override string EntityName => "Shop";

    public override async Task<ApiResponse<TDetail>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var dto = await _shopRepo.GetDetailAsync(id, ct);
        return dto == null ? ApiResponse<TDetail>.NotFound("Shop not found.") : ApiResponse<TDetail>.Ok(_mapper.Map<TDetail>(dto));
    }

    /// <summary>
    /// Creates a new branch for the caller's tenant. The branch is added to the creating owner's
    /// branch list (so they can switch to it) and receives its own copy of the measurement
    /// catalogue, garment attributes and expense categories — the same setup the first branch got.
    /// </summary>
    public override async Task<ApiResponse<Guid>> CreateAsync(TCreate vm, CancellationToken ct = default)
    {
        if (_currentUser.TenantId == Guid.Empty)
            return ApiResponse<Guid>.Fail("Branches are created inside a tenant. SystemAdmin uses /api/admin/provision-shop.");
        if (vm is CreateShopViewModel c && !string.IsNullOrWhiteSpace(c.TimeZone) && !ShopCalendar.IsKnownTimeZone(c.TimeZone))
            return ApiResponse<Guid>.Fail($"Unknown time zone '{c.TimeZone}'. Use an IANA id such as Asia/Karachi.");

        var limit = await _limits.CheckAsync(PlanLimit.Branches, ct);
        if (limit != null) return ApiResponse<Guid>.PaymentRequired(limit);

        var entity = _mapper.Map<Shop>(vm);
        entity.Id        = Guid.NewGuid();
        entity.TenantId  = _currentUser.TenantId;
        entity.BranchId  = entity.Id;          // BranchId = Shop.Id — core multi-branch rule
        entity.OwnerId   = _currentUser.UserId;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        _db.Shops.Add(entity);

        var creator = await _db.AppUsers.FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, ct);
        if (creator != null && !creator.ShopIds.Contains(entity.Id))
            creator.ShopIds = creator.ShopIds.Append(entity.Id).ToList();

        await _db.SaveChangesAsync(ct);
        await _tenantSetup.SetupNewTenantAsync(entity.TenantId, entity.Id, _currentUser.UserId, ct);
        await tx.CommitAsync(ct);

        _cache.InvalidateTenant(entity.TenantId);
        return ApiResponse<Guid>.Ok(entity.Id, "Branch created.");
    }

    protected override Task BeforeUpdateAsync(Shop entity, TUpdate vm, CancellationToken ct)
    {
        _cache.Invalidate(entity.Id);
        return Task.CompletedTask;
    }

    public override async Task<ApiResponse<Guid>> UpdateAsync(TUpdate vm, CancellationToken ct = default)
    {
        if (vm is UpdateShopViewModel u && u.TimeZone != null && !ShopCalendar.IsKnownTimeZone(u.TimeZone))
            return ApiResponse<Guid>.Fail($"Unknown time zone '{u.TimeZone}'. Use an IANA id such as Asia/Karachi.");
        return await base.UpdateAsync(vm, ct);
    }

    public override async Task<ApiResponse<string>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        if (id == _currentUser.ShopId)
            return ApiResponse<string>.Fail("You cannot delete the branch you are signed in to. Switch branch first.");
        var result = await base.DeleteAsync(id, ct);
        _cache.Invalidate(id);
        return result;
    }

    /// <summary>SystemAdmin endpoint: works across tenants (the tenant filter would always hide the shop).</summary>
    public async Task<ApiResponse<bool>> UpdateSubscriptionAsync(Guid id, UpdateShopSubscriptionRequest request, CancellationToken ct)
    {
        var entity = await _db.Shops.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);
        if (entity == null) return ApiResponse<bool>.NotFound("Shop not found.");

        entity.Status               = request.Status;
        entity.SubscriptionEndsAt   = request.SubscriptionEndsAt;
        entity.SubscriptionPlanName = request.SubscriptionPlanName;
        await _db.SaveChangesAsync(ct);

        _cache.Invalidate(id);
        _cache.InvalidateTenant(entity.TenantId);
        return ApiResponse<bool>.Ok(true, "Subscription updated successfully.");
    }
}
