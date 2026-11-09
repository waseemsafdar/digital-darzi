using Application.Common;
using Application.Interfaces.Services;
using Application.ViewModels.Admin;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
    Task<ApiResponse<PagedResult<TenantInvoiceViewModel>>> GetInvoicesAsync(Guid? tenantId, InvoiceStatus? status, int page, int pageSize, CancellationToken ct = default,
        InvoiceReviewStatus? review = null);
    Task<ApiResponse<TenantInvoiceViewModel>> RejectProofAsync(Guid invoiceId, string reason, CancellationToken ct = default);
    /// <summary>The proof file; <paramref name="tenantId"/> limits it to one business (null for the platform admin).</summary>
    Task<AttachmentFile?> OpenProofAsync(Guid invoiceId, Guid? tenantId, CancellationToken ct = default);

    // Owner
    Task<ApiResponse<TenantSubscriptionViewModel>> GetMySubscriptionAsync(CancellationToken ct = default);
    Task<ApiResponse<List<TenantInvoiceViewModel>>> GetMyInvoicesAsync(CancellationToken ct = default);
    Task<ApiResponse<BillingOptionsViewModel>> GetOptionsAsync(CancellationToken ct = default);
    /// <summary>The Owner picks a plan and period; creates (or returns) the invoice to pay.</summary>
    Task<ApiResponse<TenantInvoiceViewModel>> CheckoutAsync(CheckoutViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<TenantInvoiceViewModel>> SubmitProofAsync(Guid invoiceId, IFormFile? file, string? reference, string? paidVia, string? note, CancellationToken ct = default);
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
    private readonly ISubscriptionResolver _subscriptions;
    private readonly IFileStorage _storage;
    private readonly FileStorageOptions _fileOptions;
    private readonly BillingOptions _billing;

    public BillingService(ApplicationDbContext db, ICurrentUserService currentUser, IShopSettingsCache shops, TimeProvider clock,
        ISubscriptionResolver subscriptions, IFileStorage storage, IOptions<FileStorageOptions> fileOptions, IOptions<BillingOptions> billing)
    {
        _db = db;
        _currentUser = currentUser;
        _shops = shops;
        _clock = clock;
        _subscriptions = subscriptions;
        _storage = storage;
        _fileOptions = fileOptions.Value;
        _billing = billing.Value;
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
        await ClearOtherFreeTiersAsync(plan, ct);
        await _db.SaveChangesAsync(ct);
        _subscriptions.InvalidatePlans();
        return ApiResponse<SubscriptionPlanViewModel>.Ok(ToViewModel(plan), "Plan created.");
    }

    public async Task<ApiResponse<SubscriptionPlanViewModel>> UpdatePlanAsync(Guid id, SaveSubscriptionPlanViewModel vm, CancellationToken ct = default)
    {
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plan == null) return ApiResponse<SubscriptionPlanViewModel>.NotFound("Plan not found.");
        if (await _db.SubscriptionPlans.AnyAsync(p => p.Id != id && p.Name == vm.Name.Trim(), ct))
            return ApiResponse<SubscriptionPlanViewModel>.Conflict($"A plan named '{vm.Name}' already exists.");
        Apply(plan, vm);
        await ClearOtherFreeTiersAsync(plan, ct);
        await _db.SaveChangesAsync(ct);
        _subscriptions.InvalidatePlans();
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
        invoice.PaymentReference = vm.PaymentReference ?? invoice.ProofReference;
        invoice.ReviewStatus = InvoiceReviewStatus.None;
        invoice.RejectReason = null;

        if (invoice.PlanId.HasValue)
        {
            var plan = await _db.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == invoice.PlanId, ct);
            if (plan != null)
            {
                shop.SubscriptionPlanId = plan.Id;
                shop.SubscriptionPlanName = plan.Name;
            }
        }
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

    public async Task<ApiResponse<PagedResult<TenantInvoiceViewModel>>> GetInvoicesAsync(Guid? tenantId, InvoiceStatus? status, int page, int pageSize, CancellationToken ct = default,
        InvoiceReviewStatus? review = null)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var q = _db.TenantInvoices.AsNoTracking();
        if (tenantId.HasValue) q = q.Where(i => i.TenantId == tenantId);
        if (status.HasValue) q = q.Where(i => i.Status == status);
        if (review.HasValue) q = q.Where(i => i.ReviewStatus == review);
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
        var resolved = await _subscriptions.ResolveAsync(tenantId, _currentUser.ShopId, ct);
        var awaiting = await _db.TenantInvoices.AnyAsync(i => i.TenantId == tenantId && i.Status == InvoiceStatus.Pending
            && i.ReviewStatus == InvoiceReviewStatus.Submitted, ct);

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
                                      .SumAsync(i => (decimal?)i.Amount, ct) ?? 0,
            State                 = resolved.State,
            EffectivePlan         = resolved.EffectivePlan == null ? null : ToViewModel(resolved.EffectivePlan),
            DaysLeft              = resolved.DaysLeft,
            PaymentAwaitingReview = awaiting
        });
    }

    public async Task<ApiResponse<List<TenantInvoiceViewModel>>> GetMyInvoicesAsync(CancellationToken ct = default)
    {
        var rows = await _db.TenantInvoices.AsNoTracking()
            .Where(i => i.TenantId == _currentUser.TenantId && i.Status != InvoiceStatus.Void)
            .OrderByDescending(i => i.CreatedOn).Take(100).ToListAsync(ct);
        return ApiResponse<List<TenantInvoiceViewModel>>.Ok(await DecorateAsync(rows, ct));
    }

    // ── The Owner chooses a plan and sends proof of payment ──────────────────

    public async Task<ApiResponse<BillingOptionsViewModel>> GetOptionsAsync(CancellationToken ct = default)
    {
        var plans = await _db.SubscriptionPlans.AsNoTracking()
            .Where(p => p.IsActive && !p.IsFreeTier && p.MonthlyPrice > 0)
            .OrderBy(p => p.MonthlyPrice).ToListAsync(ct);
        return ApiResponse<BillingOptionsViewModel>.Ok(new BillingOptionsViewModel
        {
            Plans = plans.Select(ToViewModel).ToList(),
            Periods = _billing.Periods.OrderBy(p => p.Months).Select(p => new BillingPeriodOptionViewModel { Months = p.Months, DiscountPercent = p.DiscountPercent }).ToList(),
            PaymentAccounts = _billing.PaymentAccounts
                .Where(a => !string.IsNullOrWhiteSpace(a.AccountNumber))
                .Select(a => new PaymentAccountViewModel { Method = a.Method, AccountTitle = a.AccountTitle, AccountNumber = a.AccountNumber, Details = a.Details })
                .ToList(),
            Instructions = _billing.Instructions
        });
    }

    public async Task<ApiResponse<TenantInvoiceViewModel>> CheckoutAsync(CheckoutViewModel vm, CancellationToken ct = default)
    {
        var period = _billing.Periods.FirstOrDefault(p => p.Months == vm.Months);
        if (period == null)
            return ApiResponse<TenantInvoiceViewModel>.Fail($"Choose {string.Join(", ", _billing.Periods.Select(p => p.Months))} months.");
        var plan = await _db.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == vm.PlanId && p.IsActive && !p.IsFreeTier, ct);
        if (plan == null) return ApiResponse<TenantInvoiceViewModel>.NotFound("Plan not found.");

        var tenantId = _currentUser.TenantId;
        var resolved = await _subscriptions.ResolveAsync(tenantId, _currentUser.ShopId, ct);
        if (resolved.Governing == null) return ApiResponse<TenantInvoiceViewModel>.NotFound("Shop not found.");

        var pending = await _db.TenantInvoices
            .Where(i => i.TenantId == tenantId && i.Status == InvoiceStatus.Pending && i.IsSelfService)
            .ToListAsync(ct);
        if (pending.Any(i => i.ReviewStatus == InvoiceReviewStatus.Submitted))
            return ApiResponse<TenantInvoiceViewModel>.Conflict("A payment you sent is still being checked. You can choose a new plan after it is approved or rejected.");
        foreach (var old in pending) old.Status = InvoiceStatus.Void;   // replaced by this choice

        // A new paid period starts when the current trial or paid period ends, so no days are lost.
        var start = Now;
        if (resolved.State == SubscriptionStates.Trial && resolved.Governing.TrialEndsAt > start) start = resolved.Governing.TrialEndsAt!.Value;
        if (resolved.State == SubscriptionStates.Active && resolved.Governing.SubscriptionEndsAt > start) start = resolved.Governing.SubscriptionEndsAt!.Value;

        var amount = Math.Round(plan.MonthlyPrice * vm.Months * (100 - period.DiscountPercent) / 100m, 0, MidpointRounding.AwayFromZero);
        var invoice = new TenantInvoice
        {
            TenantId      = tenantId,
            ShopId        = resolved.Governing.ShopId,
            PlanId        = plan.Id,
            InvoiceNumber = $"INV-{Now:yyyyMM}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            PeriodStart   = DateTime.SpecifyKind(start, DateTimeKind.Utc),
            PeriodEnd     = DateTime.SpecifyKind(start.AddMonths(vm.Months), DateTimeKind.Utc),
            Amount        = amount,
            Currency      = plan.Currency,
            Status        = InvoiceStatus.Pending,
            CreatedOn     = Now,
            IsSelfService = true,
            Notes         = period.DiscountPercent > 0 ? $"{plan.Name}, {vm.Months} months, {period.DiscountPercent}% off" : $"{plan.Name}, {vm.Months} month(s)"
        };
        _db.TenantInvoices.Add(invoice);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<TenantInvoiceViewModel>.Ok(ToViewModel(invoice, resolved.Governing.Name, plan.Name), "Invoice created. Pay it and send the receipt.");
    }

    public async Task<ApiResponse<TenantInvoiceViewModel>> SubmitProofAsync(Guid invoiceId, IFormFile? file, string? reference, string? paidVia, string? note, CancellationToken ct = default)
    {
        var invoice = await _db.TenantInvoices.FirstOrDefaultAsync(i => i.Id == invoiceId && i.TenantId == _currentUser.TenantId, ct);
        if (invoice == null) return ApiResponse<TenantInvoiceViewModel>.NotFound("Invoice not found.");
        if (invoice.Status != InvoiceStatus.Pending) return ApiResponse<TenantInvoiceViewModel>.Fail($"This invoice is already {invoice.Status.ToString().ToLowerInvariant()}.");
        if (string.IsNullOrWhiteSpace(reference) && file == null)
            return ApiResponse<TenantInvoiceViewModel>.Fail("Add the transaction ID or a photo of the receipt.");

        if (file != null)
        {
            if (file.Length == 0) return ApiResponse<TenantInvoiceViewModel>.Fail("The file is empty.");
            if (file.Length > _fileOptions.MaxFileBytes)
                return ApiResponse<TenantInvoiceViewModel>.Fail($"The file is larger than {_fileOptions.MaxFileBytes / (1024 * 1024)} MB.");
            if (!_fileOptions.AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
                return ApiResponse<TenantInvoiceViewModel>.Fail("Send a photo (JPEG, PNG, WebP, HEIC) or a PDF of the receipt.");

            var extension = Path.GetExtension(file.FileName);
            if (extension.Length > 10) extension = string.Empty;
            var key = $"{invoice.TenantId:N}/billing/{invoice.Id:N}-{Guid.NewGuid():N}{extension}";
            await using (var stream = file.OpenReadStream())
                await _storage.SaveAsync(key, stream, ct);
            invoice.ProofStorageKey = key;
            invoice.ProofContentType = file.ContentType;
            invoice.ProofFileName = Path.GetFileName(file.FileName);
        }

        invoice.ProofReference = Trim(reference, 100);
        invoice.ProofPaidVia = Trim(paidVia, 50);
        invoice.ProofNote = Trim(note, 500);
        invoice.ProofSubmittedAt = Now;
        invoice.ReviewStatus = InvoiceReviewStatus.Submitted;
        invoice.RejectReason = null;
        await _db.SaveChangesAsync(ct);

        var planName = invoice.PlanId.HasValue ? await _db.SubscriptionPlans.Where(p => p.Id == invoice.PlanId).Select(p => p.Name).FirstOrDefaultAsync(ct) : null;
        return ApiResponse<TenantInvoiceViewModel>.Ok(ToViewModel(invoice, null, planName), "Thanks. We will check your payment and switch on your plan.");
    }

    public async Task<ApiResponse<TenantInvoiceViewModel>> RejectProofAsync(Guid invoiceId, string reason, CancellationToken ct = default)
    {
        var invoice = await _db.TenantInvoices.FirstOrDefaultAsync(i => i.Id == invoiceId, ct);
        if (invoice == null) return ApiResponse<TenantInvoiceViewModel>.NotFound("Invoice not found.");
        if (invoice.Status != InvoiceStatus.Pending || invoice.ReviewStatus != InvoiceReviewStatus.Submitted)
            return ApiResponse<TenantInvoiceViewModel>.Fail("There is no payment waiting to be checked on this invoice.");
        invoice.ReviewStatus = InvoiceReviewStatus.Rejected;
        invoice.RejectReason = reason.Trim();
        await _db.SaveChangesAsync(ct);
        var decorated = await DecorateAsync(new List<TenantInvoice> { invoice }, ct);
        return ApiResponse<TenantInvoiceViewModel>.Ok(decorated[0], "Payment rejected. The shop will see the reason.");
    }

    public async Task<AttachmentFile?> OpenProofAsync(Guid invoiceId, Guid? tenantId, CancellationToken ct = default)
    {
        var invoice = await _db.TenantInvoices.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == invoiceId && (tenantId == null || i.TenantId == tenantId), ct);
        if (invoice?.ProofStorageKey == null) return null;
        var stream = await _storage.OpenReadAsync(invoice.ProofStorageKey, ct);
        return stream == null ? null : new AttachmentFile(stream, invoice.ProofContentType ?? "application/octet-stream", invoice.ProofFileName ?? "receipt");
    }

    private static string? Trim(string? s, int max) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim().Length > max ? s.Trim()[..max] : s.Trim();

    private async Task<List<TenantInvoiceViewModel>> DecorateAsync(List<TenantInvoice> rows, CancellationToken ct)
    {
        var shopIds = rows.Select(r => r.ShopId).Distinct().ToList();
        var planIds = rows.Where(r => r.PlanId.HasValue).Select(r => r.PlanId!.Value).Distinct().ToList();
        var shops = await _db.Shops.IgnoreQueryFilters().AsNoTracking().Where(s => shopIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var plans = await _db.SubscriptionPlans.AsNoTracking().Where(p => planIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        return rows.Select(r => ToViewModel(r, shops.GetValueOrDefault(r.ShopId), r.PlanId.HasValue ? plans.GetValueOrDefault(r.PlanId.Value) : null)).ToList();
    }

    /// <summary>Only one plan can be the free fallback.</summary>
    private async Task ClearOtherFreeTiersAsync(SubscriptionPlan plan, CancellationToken ct)
    {
        if (!plan.IsFreeTier) return;
        foreach (var other in await _db.SubscriptionPlans.Where(p => p.Id != plan.Id && p.IsFreeTier).ToListAsync(ct))
            other.IsFreeTier = false;
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
        plan.IsFreeTier        = vm.IsFreeTier;
        plan.IncludesPhotos    = vm.IncludesPhotos;
        plan.IncludesReports   = vm.IncludesReports;
    }

    private static SubscriptionPlanViewModel ToViewModel(SubscriptionPlan p) => new()
    {
        Id = p.Id, Name = p.Name, Description = p.Description, MonthlyPrice = p.MonthlyPrice, Currency = p.Currency,
        MaxBranches = p.MaxBranches, MaxUsers = p.MaxUsers, MaxOrdersPerMonth = p.MaxOrdersPerMonth,
        TrialDays = p.TrialDays, IsActive = p.IsActive, IsFreeTier = p.IsFreeTier,
        IncludesPhotos = p.IncludesPhotos, IncludesReports = p.IncludesReports
    };

    private static TenantInvoiceViewModel ToViewModel(TenantInvoice i, string? shopName, string? planName) => new()
    {
        Id = i.Id, InvoiceNumber = i.InvoiceNumber, TenantId = i.TenantId, ShopId = i.ShopId, ShopName = shopName,
        PlanName = planName, PeriodStart = i.PeriodStart, PeriodEnd = i.PeriodEnd, Amount = i.Amount, Currency = i.Currency,
        Status = i.Status, CreatedOn = i.CreatedOn, PaidAt = i.PaidAt, PaymentMethod = i.PaymentMethod,
        IsSelfService = i.IsSelfService, ReviewStatus = i.ReviewStatus, HasProof = i.ProofStorageKey != null,
        ProofReference = i.ProofReference, ProofPaidVia = i.ProofPaidVia, ProofNote = i.ProofNote,
        ProofSubmittedAt = i.ProofSubmittedAt, RejectReason = i.RejectReason,
        PaymentReference = i.PaymentReference, Notes = i.Notes
    };
}
