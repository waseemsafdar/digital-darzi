using Application.Common;
using Application.Interfaces.Services;
using Application.ViewModels.Order;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public interface IKarigarLedgerService
{
    Task<ApiResponse<List<KarigarRateViewModel>>> GetRatesAsync(Guid? karigarId, CancellationToken ct = default);
    Task<ApiResponse<KarigarRateViewModel>> UpsertRateAsync(UpsertKarigarRateViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<object>> DeleteRateAsync(Guid id, CancellationToken ct = default);

    Task<ApiResponse<KarigarLedgerViewModel>> GetLedgerAsync(Guid karigarId, DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<ApiResponse<List<KarigarBalanceViewModel>>> GetBalancesAsync(CancellationToken ct = default);
    Task<ApiResponse<KarigarLedgerEntryViewModel>> RecordEntryAsync(Guid karigarId, RecordKarigarLedgerEntryViewModel vm, CancellationToken ct = default);

    /// <summary>Piece-rate price for a stage of an item: karigar-specific rate, else the shop default, × quantity. Null when no rate exists.</summary>
    Task<decimal?> ResolveStagePriceAsync(Guid? karigarId, GarmentType garment, ProductionStage stage, int qty, CancellationToken ct = default);

    /// <summary>Adds (without saving) the earning line for a completed stage log. Idempotent per log.</summary>
    Task AddEarningAsync(OrderItemStageLog log, OrderItem item, CancellationToken ct = default);

    /// <summary>Adds (without saving) a deduction line.</summary>
    void AddDeduction(Guid karigarId, decimal amount, Guid? orderItemId, ProductionStage? stage, string? notes);
}

public sealed class KarigarLedgerService : IKarigarLedgerService
{
    private readonly ApplicationDbContext _db;
    private readonly IBusinessCalendar _calendar;

    public KarigarLedgerService(ApplicationDbContext db, IBusinessCalendar calendar)
    {
        _db = db;
        _calendar = calendar;
    }

    // ── Rates ────────────────────────────────────────────────────────────────

    public async Task<ApiResponse<List<KarigarRateViewModel>>> GetRatesAsync(Guid? karigarId, CancellationToken ct = default)
    {
        var q = _db.KarigarRates.AsNoTracking();
        if (karigarId.HasValue) q = q.Where(r => r.KarigarId == karigarId || r.KarigarId == null);

        var rows = await q.OrderBy(r => r.GarmentType).ThenBy(r => r.Stage).ThenBy(r => r.KarigarId).ToListAsync(ct);
        var names = await KarigarNamesAsync(rows.Where(r => r.KarigarId.HasValue).Select(r => r.KarigarId!.Value), ct);
        return ApiResponse<List<KarigarRateViewModel>>.Ok(rows.Select(r => new KarigarRateViewModel
        {
            Id = r.Id, KarigarId = r.KarigarId, GarmentType = r.GarmentType, Stage = r.Stage, Rate = r.Rate,
            KarigarName = r.KarigarId.HasValue ? names.GetValueOrDefault(r.KarigarId.Value) : null
        }).ToList());
    }

    public async Task<ApiResponse<KarigarRateViewModel>> UpsertRateAsync(UpsertKarigarRateViewModel vm, CancellationToken ct = default)
    {
        if (vm.KarigarId.HasValue && !await _db.AppUsers.AnyAsync(u => u.Id == vm.KarigarId, ct))
            return ApiResponse<KarigarRateViewModel>.NotFound("Karigar not found.");
        var garmentError = (await GarmentTypeCatalogue.LoadAsync(_db, ct)).CheckUsable((int)vm.GarmentType, allowHidden: true);
        if (garmentError != null) return ApiResponse<KarigarRateViewModel>.Fail(garmentError);

        var row = await _db.KarigarRates.FirstOrDefaultAsync(r =>
            r.KarigarId == vm.KarigarId && r.GarmentType == vm.GarmentType && r.Stage == vm.Stage, ct);
        if (row == null)
        {
            row = new KarigarRate { KarigarId = vm.KarigarId, GarmentType = vm.GarmentType, Stage = vm.Stage };
            _db.KarigarRates.Add(row);
        }
        row.Rate = vm.Rate;
        await _db.SaveChangesAsync(ct);

        return ApiResponse<KarigarRateViewModel>.Ok(new KarigarRateViewModel
        {
            Id = row.Id, KarigarId = row.KarigarId, GarmentType = row.GarmentType, Stage = row.Stage, Rate = row.Rate
        }, "Rate saved.");
    }

    public async Task<ApiResponse<object>> DeleteRateAsync(Guid id, CancellationToken ct = default)
    {
        var row = await _db.KarigarRates.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (row == null) return ApiResponse<object>.NotFound("Rate not found.");
        row.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(null, "Rate deleted.");
    }

    public async Task<decimal?> ResolveStagePriceAsync(Guid? karigarId, GarmentType garment, ProductionStage stage, int qty, CancellationToken ct = default)
    {
        var rates = await _db.KarigarRates.AsNoTracking()
            .Where(r => r.GarmentType == garment && r.Stage == stage && (r.KarigarId == null || r.KarigarId == karigarId))
            .ToListAsync(ct);
        var rate = rates.FirstOrDefault(r => karigarId.HasValue && r.KarigarId == karigarId)
                   ?? rates.FirstOrDefault(r => r.KarigarId == null);
        return rate == null ? null : rate.Rate * Math.Max(1, qty);
    }

    // ── Ledger ───────────────────────────────────────────────────────────────

    public async Task AddEarningAsync(OrderItemStageLog log, OrderItem item, CancellationToken ct = default)
    {
        var alreadyTracked = _db.ChangeTracker.Entries<KarigarLedgerEntry>()
            .Any(e => e.Entity.StageLogId == log.Id && e.Entity.EntryType == KarigarLedgerEntryType.Earning);
        if (alreadyTracked || await _db.KarigarLedgerEntries.AnyAsync(e => e.StageLogId == log.Id && e.EntryType == KarigarLedgerEntryType.Earning, ct))
            return;

        var assignment = item.StageAssignments.FirstOrDefault(a => a.Stage == log.Stage)
            ?? await _db.OrderItemStageAssignments.AsNoTracking().FirstOrDefaultAsync(a => a.OrderItemId == item.Id && a.Stage == log.Stage, ct);

        var amount = assignment?.StagePrice
            ?? await ResolveStagePriceAsync(log.KarigarId, item.GarmentType, log.Stage, item.Qty, ct)
            ?? 0;
        if (amount <= 0) return;
        var garmentName = (await GarmentTypeCatalogue.LoadAsync(_db, ct)).NameOf(item.GarmentType);

        _db.KarigarLedgerEntries.Add(new KarigarLedgerEntry
        {
            KarigarId    = log.KarigarId,
            EntryType    = KarigarLedgerEntryType.Earning,
            Amount       = amount,
            SignedAmount = amount,
            EntryDate    = log.CompletedAt ?? _calendar.UtcNow,
            OrderItemId  = item.Id,
            Stage        = log.Stage,
            StageLogId   = log.Id,
            Notes        = $"{log.Stage} × {item.Qty} {garmentName}"
        });
    }

    public void AddDeduction(Guid karigarId, decimal amount, Guid? orderItemId, ProductionStage? stage, string? notes)
    {
        if (amount <= 0) return;
        _db.KarigarLedgerEntries.Add(new KarigarLedgerEntry
        {
            KarigarId    = karigarId,
            EntryType    = KarigarLedgerEntryType.Deduction,
            Amount       = amount,
            SignedAmount = -amount,
            EntryDate    = _calendar.UtcNow,
            OrderItemId  = orderItemId,
            Stage        = stage,
            Notes        = notes
        });
    }

    public async Task<ApiResponse<KarigarLedgerEntryViewModel>> RecordEntryAsync(Guid karigarId, RecordKarigarLedgerEntryViewModel vm, CancellationToken ct = default)
    {
        if (vm.EntryType == KarigarLedgerEntryType.Earning)
            return ApiResponse<KarigarLedgerEntryViewModel>.Fail("Earnings are recorded automatically when stage work is completed.");
        if (vm.Amount <= 0)
            return ApiResponse<KarigarLedgerEntryViewModel>.Fail("Amount must be greater than zero.");
        if (!await _db.AppUsers.AnyAsync(u => u.Id == karigarId, ct))
            return ApiResponse<KarigarLedgerEntryViewModel>.NotFound("Karigar not found.");

        var signed = vm.EntryType switch
        {
            KarigarLedgerEntryType.Adjustment => vm.IsCredit ? vm.Amount : -vm.Amount,
            _ => -vm.Amount   // Advance, Deduction, Payment all reduce what the shop owes
        };

        var entry = new KarigarLedgerEntry
        {
            KarigarId     = karigarId,
            EntryType     = vm.EntryType,
            Amount        = vm.Amount,
            SignedAmount  = signed,
            EntryDate     = vm.EntryDate.HasValue ? await _calendar.ToCalendarDateAsync(vm.EntryDate.Value, ct) : _calendar.UtcNow,
            PaymentMethod = vm.EntryType is KarigarLedgerEntryType.Payment or KarigarLedgerEntryType.Advance
                                ? vm.PaymentMethod ?? PaymentMethod.Cash : vm.PaymentMethod,
            Notes         = vm.Notes
        };
        _db.KarigarLedgerEntries.Add(entry);
        await _db.SaveChangesAsync(ct);

        var balance = await _db.KarigarLedgerEntries.Where(e => e.KarigarId == karigarId).SumAsync(e => e.SignedAmount, ct);
        return ApiResponse<KarigarLedgerEntryViewModel>.Ok(ToViewModel(entry, balance, null, null), $"{vm.EntryType} recorded.");
    }

    public async Task<ApiResponse<KarigarLedgerViewModel>> GetLedgerAsync(Guid karigarId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var karigar = await _db.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == karigarId, ct);
        if (karigar == null) return ApiResponse<KarigarLedgerViewModel>.NotFound("Karigar not found.");

        var all = _db.KarigarLedgerEntries.AsNoTracking().Where(e => e.KarigarId == karigarId);
        var opening = from.HasValue ? await all.Where(e => e.EntryDate < from.Value).SumAsync(e => e.SignedAmount, ct) : 0m;

        var window = all;
        if (from.HasValue) window = window.Where(e => e.EntryDate >= from.Value);
        if (to.HasValue) window = window.Where(e => e.EntryDate < to.Value.Date.AddDays(1));

        var rows = await window.OrderBy(e => e.EntryDate).ThenBy(e => e.CreatedOn).Take(2000).ToListAsync(ct);

        var itemIds = rows.Where(r => r.OrderItemId.HasValue).Select(r => r.OrderItemId!.Value).Distinct().ToList();
        var items = await _db.OrderItems.AsNoTracking().Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.GarmentType, i.Order.OrderNumber })
            .ToDictionaryAsync(i => i.Id, ct);

        var running = opening;
        var entries = rows.Select(r =>
        {
            running += r.SignedAmount;
            var item = r.OrderItemId.HasValue ? items.GetValueOrDefault(r.OrderItemId.Value) : null;
            return ToViewModel(r, running, item?.OrderNumber, item?.GarmentType);
        }).ToList();

        decimal Sum(KarigarLedgerEntryType t) => rows.Where(r => r.EntryType == t).Sum(r => r.Amount);
        return ApiResponse<KarigarLedgerViewModel>.Ok(new KarigarLedgerViewModel
        {
            KarigarId      = karigarId,
            KarigarName    = karigar.Name,
            From           = from,
            To             = to,
            OpeningBalance = opening,
            Earnings       = Sum(KarigarLedgerEntryType.Earning),
            Advances       = Sum(KarigarLedgerEntryType.Advance),
            Deductions     = Sum(KarigarLedgerEntryType.Deduction),
            Payments       = Sum(KarigarLedgerEntryType.Payment),
            Adjustments    = rows.Where(r => r.EntryType == KarigarLedgerEntryType.Adjustment).Sum(r => r.SignedAmount),
            ClosingBalance = running,
            Entries        = entries
        });
    }

    public async Task<ApiResponse<List<KarigarBalanceViewModel>>> GetBalancesAsync(CancellationToken ct = default)
    {
        var sums = await _db.KarigarLedgerEntries.AsNoTracking()
            .GroupBy(e => e.KarigarId)
            .Select(g => new
            {
                KarigarId = g.Key,
                Earned    = g.Where(e => e.EntryType == KarigarLedgerEntryType.Earning).Sum(e => e.Amount),
                Paid      = g.Where(e => e.EntryType == KarigarLedgerEntryType.Payment || e.EntryType == KarigarLedgerEntryType.Advance).Sum(e => e.Amount),
                Balance   = g.Sum(e => e.SignedAmount)
            })
            .ToListAsync(ct);

        var ids = sums.Select(s => s.KarigarId).ToList();
        var users = await _db.AppUsers.AsNoTracking().Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Phone }).ToDictionaryAsync(u => u.Id, ct);

        return ApiResponse<List<KarigarBalanceViewModel>>.Ok(sums.Select(s => new KarigarBalanceViewModel
        {
            KarigarId   = s.KarigarId,
            KarigarName = users.GetValueOrDefault(s.KarigarId)?.Name ?? "Unknown",
            Phone       = users.GetValueOrDefault(s.KarigarId)?.Phone,
            TotalEarned = s.Earned,
            TotalPaid   = s.Paid,
            Balance     = s.Balance
        }).OrderByDescending(b => b.Balance).ToList());
    }

    private async Task<Dictionary<Guid, string>> KarigarNamesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        return await _db.AppUsers.AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
    }

    private static KarigarLedgerEntryViewModel ToViewModel(KarigarLedgerEntry e, decimal running, string? orderNumber, GarmentType? garment) => new()
    {
        Id = e.Id, EntryType = e.EntryType, Amount = e.Amount, SignedAmount = e.SignedAmount, RunningBalance = running,
        EntryDate = e.EntryDate, OrderItemId = e.OrderItemId, OrderNumber = orderNumber, GarmentType = garment,
        Stage = e.Stage, PaymentMethod = e.PaymentMethod, Notes = e.Notes
    };
}
