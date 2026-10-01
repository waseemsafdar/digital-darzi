using Application.Common;
using Application.Interfaces.Services;
using Application.ViewModels.Admin;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public interface IBillingService
{
    // SystemAdmin
    Task<ApiResponse<List<SubscriptionPlanViewModel>>> GetPlansAsync(bool includeInactive, CancellationToken ct = default);
    Task<ApiResponse<SubscriptionPlanViewModel>> CreatePlanAsync(SaveSubscriptionPlanViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<SubscriptionPlanViewModel>> UpdatePlanAsync(Guid id, SaveSubscriptionPlanViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<object>> AssignPlanAsync(Guid shopId, Guid planId, CancellationToken ct = default);
    Task<ApiResponse<TenantInvoiceViewModel>> CreateInvoiceAsync(CreateInvoiceViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<TenantInvoiceViewModel>> MarkInvoicePaidAsync(Guid invoiceId, MarkInvoicePaidViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<TenantInvoiceViewModel>> VoidInvoiceAsync(Guid invoiceId, CancellationToken ct = default);
    Task<ApiResponse<PagedResult<TenantInvoiceViewModel>>> GetInvoicesAsync(Guid? tenantId, InvoiceStatus? status, int page, int pageSize, CancellationToken ct = default);

    // Owner
    Task<ApiResponse<TenantSubscriptionViewModel>> GetMySubscriptionAsync(CancellationToken ct = default);
    Task<ApiResponse<List<TenantInvoiceViewModel>>> GetMyInvoicesAsync(CancellationToken ct = default);
}

/// <summary>
/// Plans, limits and invoices for the SaaS. Online collection (JazzCash / EasyPaisa / card) needs
/// merchant credentials and is not wired; the platform records the payment against the invoice,
/// which moves the subscription to Active and extends its end date by the invoiced period.
/// </summary>
public sealed class BillingService : IBillingService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IShopSettingsCache _shops;
    private readonly TimeProvider _clock;

    public BillingService(ApplicationDbContext db, ICurrentUserService currentUser, IShopSettingsCache shops, TimeProvider clock)
    {
        _db = db;
        _currentUser = currentUser;
        _shops = shops;
        _clock = clock;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    public async Task<ApiResponse<List<SubscriptionPlanViewModel>>> GetPlansAsync(bool includeInactive, CancellationToken ct = default)
        => ApiResponse<List<SubscriptionPlanViewModel>>.Ok(await _db.SubscriptionPlans.AsNoTracking()
            .Where(p => includeInactive || p.IsActive)
            .OrderBy(p => p.MonthlyPrice)
            .Select(p => ToViewModel(p))
            .ToListAsync(ct));

    public async Task<ApiResponse<SubscriptionPlanViewModel>> CreatePlanAsync(SaveSubscriptionPlanViewModel vm, CancellationToken ct = default)
    {
        if (await _db.SubscriptionPlans.AnyAsync(p => p.Name == vm.Name.Trim(), ct))
            return ApiResponse<SubscriptionPlanViewModel>.Conflict($"A plan named '{vm.Name}' already exists.");
        var plan = new SubscriptionPlan { CreatedOn = Now };
        Apply(plan, vm);
        _db.SubscriptionPlans.Add(plan);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<SubscriptionPlanViewModel>.Ok(ToViewModel(plan), "Plan created.");
    }

    public async Task<ApiResponse<SubscriptionPlanViewModel>> UpdatePlanAsync(Guid id, SaveSubscriptionPlanViewModel vm, CancellationToken ct = default)
    {
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan == null) return ApiResponse<SubscriptionPlanViewModel>.NotFound("Plan not found.");
        if (await _db.SubscriptionPlans.AnyAsync(p => p.Id != id && p.Name == vm.Name.Trim(), ct))
            return ApiResponse<SubscriptionPlanViewModel>.Conflict($"A plan named '{vm.Name}' already exists.");
        Apply(plan, vm);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<SubscriptionPlanViewModel>.Ok(ToViewModel(plan), "Plan updated.");
    }

    public async Task<ApiResponse<object>> AssignPlanAsync(Guid shopId, Guid planId, CancellationToken ct = default)
    {
        var shop = await _db.Shops.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == shopId && !s.IsDeleted, ct);
        if (shop == null) return ApiResponse<object>.NotFound("Shop not found.");
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == planId && p.IsActive, ct);
        if (plan == null) return ApiResponse<object>.NotFound("Plan not found or inactive.");

        shop.SubscriptionPlanId = plan.Id;
        shop.SubscriptionPlanName = plan.Name;
        await _db.SaveChangesAsync(ct);
        _shops.Invalidate(shopId);
        _shops.InvalidateTenant(shop.TenantId);
        return ApiResponse<object>.Ok(null, $"Plan '{plan.Name}' assigned to '{shop.Name}'.");
    }

    public async Task<ApiResponse<TenantInvoiceViewModel>> CreateInvoiceAsync(CreateInvoiceViewModel vm, CancellationToken ct = default)
    {
        if (vm.Months is < 1 or > 12) return ApiResponse<TenantInvoiceViewModel>.Fail("Months must be between 1 and 12.");
        var shop = await _db.Shops.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == vm.ShopId && !s.IsDeleted, ct);
        if (shop == null) return ApiResponse<TenantInvoiceViewModel>.NotFound("Shop not found.");
        var plan = shop.SubscriptionPlanId.HasValue
            ? await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == shop.SubscriptionPlanId, ct)
            : null;
        if (plan == null && vm.Amount == null)
            return ApiResponse<TenantInvoiceViewModel>.Fail("Assign a plan to the shop first, or give an explicit amount.");

        var start = shop.SubscriptionEndsAt.HasValue && shop.SubscriptionEndsAt > Now ? shop.SubscriptionEndsAt.Value : Now.Date;
        var invoice = new TenantInvoice
        {
            TenantId      = shop.TenantId,
            ShopId        = shop.Id,
            PlanId        = plan?.Id,
            InvoiceNumber = $"INV-{Now:yyyyMM}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            PeriodStart   = DateTime.SpecifyKind(start, DateTimeKind.Utc),
            PeriodEnd     = DateTime.SpecifyKind(start.AddMonths(vm.Months), DateTimeKind.Utc),
            Amount        = vm.Amount ?? plan!.MonthlyPrice * vm.Months,
            Currency      = plan?.Currency ?? shop.Currency,
            Status        = InvoiceStatus.Pending,
            CreatedOn     = Now,
            Notes         = vm.Notes
        };
        _db.TenantInvoices.Add(invoice);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<TenantInvoiceViewModel>.Ok(ToViewModel(invoice, shop.Name, plan?.Name), "Invoice created.");
    }

    public async Task<ApiResponse<TenantInvoiceViewModel>> MarkInvoicePaidAsync(Guid invoiceId, MarkInvoicePaidViewModel vm, CancellationToken ct = default)
    {
        var invoice = await _db.TenantInvoices.FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (invoice == null) return ApiResponse<TenantInvoiceViewModel>.NotFound("Invoice not found.");
        if (invoice.Status != InvoiceStatus.Pending) return ApiResponse<TenantInvoiceViewModel>.Fail($"Invoice is {invoice.Status}.");

        var shop = await _db.Shops.IgnoreQueryFilters().FirstAsync(s => s.Id == invoice.ShopId, ct);
        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidAt = Now;
        invoice.PaymentMethod = vm.PaymentMethod;
        invoice.PaymentReference = vm.PaymentReference;

        shop.Status = SubscriptionStatus.Active;
        if (!shop.SubscriptionEndsAt.HasValue || shop.SubscriptionEndsAt < invoice.PeriodEnd)
            shop.SubscriptionEndsAt = invoice.PeriodEnd;

        await _db.SaveChangesAsync(ct);
        _shops.Invalidate(shop.Id);
        _shops.InvalidateTenant(shop.TenantId);
        var planName = invoice.PlanId.HasValue ? await _db.SubscriptionPlans.Where(p => p.Id == invoice.PlanId).Select(p => p.Name).FirstOrDefaultAsync(ct) : null;
        return ApiResponse<TenantInvoiceViewModel>.Ok(ToViewModel(invoice, shop.Name, planName), "Invoice paid; subscription extended.");
    }

    public async Task<ApiResponse<TenantInvoiceViewModel>> VoidInvoiceAsync(Guid invoiceId, CancellationToken ct = default)
    {
        var invoice = await _db.TenantInvoices.FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (invoice == null) return ApiResponse<TenantInvoiceViewModel>.NotFound("Invoice not found.");
        if (invoice.Status != InvoiceStatus.Pending) return ApiResponse<TenantInvoiceViewModel>.Fail($"Invoice is {invoice.Status}.");
        invoice.Status = InvoiceStatus.Void;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<TenantInvoiceViewModel>.Ok(ToViewModel(invoice, null, null), "Invoice voided.");
    }

    public async Task<ApiResponse<PagedResult<TenantInvoiceViewModel>>> GetInvoicesAsync(Guid? tenantId, InvoiceStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var q = _db.TenantInvoices.AsNoTracking();
        if (tenantId.HasValue) q = q.Where(i => i.TenantId == tenantId);
        if (status.HasValue) q = q.Where(i => i.Status == status);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(i => i.CreatedOn).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var items = await DecorateAsync(rows, ct);
        return ApiResponse<PagedResult<TenantInvoiceViewModel>>.Ok(PagedResult<TenantInvoiceViewModel>.From(items, total, page, pageSize));
    }

    public async Task<ApiResponse<TenantSubscriptionViewModel>> GetMySubscriptionAsync(CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;
        var governing = await _shops.GetGoverningSubscriptionAsync(tenantId, _currentUser.ShopId, ct);
        var shop = governing ?? await _shops.GetAsync(_currentUser.ShopId, ct);
        if (shop == null) return ApiResponse<TenantSubscriptionViewModel>.NotFound("Shop not found.");

        var plan = shop.SubscriptionPlanId.HasValue
            ? await _db.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == shop.SubscriptionPlanId, ct)
            : null;
        var monthStart = new DateTime(Now.Year, Now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        return ApiResponse<TenantSubscriptionViewModel>.Ok(new TenantSubscriptionViewModel
        {
            TenantId            = tenantId,
            GoverningShopId     = shop.ShopId,
            ShopName            = shop.Name,
            Status              = shop.Status,
            TrialEndsAt         = shop.TrialEndsAt,
            SubscriptionEndsAt  = shop.SubscriptionEndsAt,
            Plan                = plan == null ? null : ToViewModel(plan),
            Branches            = await _db.Shops.CountAsync(ct),
            ActiveUsers         = await _db.AppUsers.CountAsync(u => u.ActiveStatus == ActiveStatus.Active, ct),
            OrdersThisMonth     = await _db.TenantWide<Order>().CountAsync(o => o.CreatedOn >= monthStart, ct),
            OutstandingInvoices = await _db.TenantInvoices.Where(i => i.TenantId == tenantId && i.Status == InvoiceStatus.Pending)
                                      .SumAsync(i => (decimal?)i.Amount, ct) ?? 0
        });
    }

    public async Task<ApiResponse<List<TenantInvoiceViewModel>>> GetMyInvoicesAsync(CancellationToken ct = default)
    {
        var rows = await _db.TenantInvoices.AsNoTracking()
            .Where(i => i.TenantId == _currentUser.TenantId && i.Status != InvoiceStatus.Void)
            .OrderByDescending(i => i.CreatedOn).Take(100).ToListAsync(ct);
        return ApiResponse<List<TenantInvoiceViewModel>>.Ok(await DecorateAsync(rows, ct));
    }

    private async Task<List<TenantInvoiceViewModel>> DecorateAsync(List<TenantInvoice> rows, CancellationToken ct)
    {
        var shopIds = rows.Select(r => r.ShopId).Distinct().ToList();
        var planIds = rows.Where(r => r.PlanId.HasValue).Select(r => r.PlanId!.Value).Distinct().ToList();
        var shops = await _db.Shops.IgnoreQueryFilters().AsNoTracking().Where(s => shopIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var plans = await _db.SubscriptionPlans.AsNoTracking().Where(p => planIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        return rows.Select(r => ToViewModel(r, shops.GetValueOrDefault(r.ShopId), r.PlanId.HasValue ? plans.GetValueOrDefault(r.PlanId.Value) : null)).ToList();
    }

    private static void Apply(SubscriptionPlan plan, SaveSubscriptionPlanViewModel vm)
    {
        plan.Name              = vm.Name.Trim();
        plan.Description       = vm.Description;
        plan.MonthlyPrice      = vm.MonthlyPrice;
        plan.Currency          = vm.Currency;
        plan.MaxBranches       = vm.MaxBranches;
        plan.MaxUsers          = vm.MaxUsers;
        plan.MaxOrdersPerMonth = vm.MaxOrdersPerMonth;
        plan.TrialDays         = vm.TrialDays;
        plan.IsActive          = vm.IsActive;
    }

    private static SubscriptionPlanViewModel ToViewModel(SubscriptionPlan p) => new()
    {
        Id = p.Id, Name = p.Name, Description = p.Description, MonthlyPrice = p.MonthlyPrice, Currency = p.Currency,
        MaxBranches = p.MaxBranches, MaxUsers = p.MaxUsers, MaxOrdersPerMonth = p.MaxOrdersPerMonth,
        TrialDays = p.TrialDays, IsActive = p.IsActive
    };

    private static TenantInvoiceViewModel ToViewModel(TenantInvoice i, string? shopName, string? planName) => new()
    {
        Id = i.Id, InvoiceNumber = i.InvoiceNumber, TenantId = i.TenantId, ShopId = i.ShopId, ShopName = shopName,
        PlanName = planName, PeriodStart = i.PeriodStart, PeriodEnd = i.PeriodEnd, Amount = i.Amount, Currency = i.Currency,
        Status = i.Status, CreatedOn = i.CreatedOn, PaidAt = i.PaidAt, PaymentMethod = i.PaymentMethod,
        PaymentReference = i.PaymentReference, Notes = i.Notes
    };
}
