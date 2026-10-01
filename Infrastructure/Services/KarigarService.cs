using System.Text.Json;
using Application.Common;
using Application.Interfaces.Services;
using Application.ViewModels.Order;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class KarigarService : IKarigarService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBusinessCalendar _calendar;

    public KarigarService(ApplicationDbContext db, ICurrentUserService currentUser, IBusinessCalendar calendar)
    {
        _db          = db;
        _currentUser = currentUser;
        _calendar    = calendar;
    }

    public async Task<ApiResponse<KarigarWorkQueueViewModel>> GetMyWorkQueueAsync(CancellationToken ct = default)
        => await GetKarigarWorkQueueAsync(_currentUser.UserId, ct);

    /// <summary>
    /// Open work for a karigar: every stage assigned to them whose latest log is not completed
    /// (pending, in progress, or sent back for rework) on an active order, plus open alterations.
    /// </summary>
    public async Task<ApiResponse<KarigarWorkQueueViewModel>> GetKarigarWorkQueueAsync(Guid karigarId, CancellationToken ct = default)
    {
        var karigar = await _db.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == karigarId, ct);
        if (karigar == null) return ApiResponse<KarigarWorkQueueViewModel>.NotFound("Karigar not found.");

        var assignments = await _db.OrderItemStageAssignments.AsNoTracking()
            .Where(a => a.AssignedKarigarId == karigarId
                     && !a.OrderItem.IsDeleted
                     && a.OrderItem.Status != OrderItemStatus.Delivered
                     && a.OrderItem.Status != OrderItemStatus.Cancelled
                     && a.OrderItem.Order.Status != OrderStatus.Cancelled
                     && a.OrderItem.Order.Status != OrderStatus.Delivered)
            .Select(a => new
            {
                a.OrderItemId, a.Stage, a.StagePrice, a.EstimatedDays, a.CreatedOn,
                a.OrderItem.OrderId, a.OrderItem.GarmentType, a.OrderItem.StyleNotes, a.OrderItem.FabricColor,
                a.OrderItem.Qty, a.OrderItem.AttributesJson,
                a.OrderItem.Order.OrderNumber, a.OrderItem.Order.DeliveryDate, a.OrderItem.Order.Priority,
                CustomerName = a.OrderItem.Order.Customer.Name, CustomerPhone = a.OrderItem.Order.Customer.Phone
            })
            .OrderBy(a => a.DeliveryDate)
            .Take(500)
            .ToListAsync(ct);

        var itemIds = assignments.Select(a => a.OrderItemId).Distinct().ToList();
        var logs = await _db.OrderItemStageLogs.AsNoTracking()
            .Where(l => itemIds.Contains(l.OrderItemId))
            .Select(l => new { l.OrderItemId, l.Stage, l.Status, l.CompletedAt, l.CreatedOn, l.ReworkReason })
            .ToListAsync(ct);
        var latest = logs.GroupBy(l => (l.OrderItemId, l.Stage))
            .ToDictionary(g => g.Key, g => g.OrderBy(l => l.CreatedOn).Last());

        var tz = await _calendar.GetTimeZoneAsync(ct);
        var today = ShopCalendar.Today(_calendar.UtcNow, tz);
        var (dayStart, dayEnd) = ShopCalendar.LocalDayBounds(today, tz);

        var completedToday = await _db.OrderItemStageLogs.AsNoTracking()
            .CountAsync(l => l.KarigarId == karigarId && l.Status == StageLogStatus.Completed
                          && l.CompletedAt >= dayStart && l.CompletedAt < dayEnd, ct);

        var pending = new List<KarigarStageTaskViewModel>();
        var inProgress = new List<KarigarStageTaskViewModel>();
        foreach (var a in assignments)
        {
            latest.TryGetValue((a.OrderItemId, a.Stage), out var log);
            if (log?.Status == StageLogStatus.Completed) continue;

            var task = new KarigarStageTaskViewModel
            {
                OrderItemId       = a.OrderItemId,
                OrderId           = a.OrderId,
                OrderNumber       = a.OrderNumber,
                CustomerName      = a.CustomerName,
                CustomerPhone     = a.CustomerPhone,
                GarmentType       = a.GarmentType,
                Stage             = a.Stage,
                OrderDeliveryDate = a.DeliveryDate,
                IsUrgent          = a.DeliveryDate <= today.AddDays(1),
                IsOverdue         = a.DeliveryDate < today,
                Priority          = a.Priority,
                StagePrice        = a.StagePrice,
                EstimatedDays     = a.EstimatedDays,
                StyleNotes        = a.StyleNotes,
                FabricColor       = a.FabricColor,
                Qty               = a.Qty,
                Attributes        = ReadAttributes(a.AttributesJson),
                AssignedOn        = a.CreatedOn,
                IsRework          = log?.Status == StageLogStatus.ReworkRequired || log?.ReworkReason != null,
                ReworkReason      = log?.Status == StageLogStatus.ReworkRequired ? log.ReworkReason : log?.ReworkReason
            };
            if (log?.Status == StageLogStatus.InProgress && !log.CompletedAt.HasValue) inProgress.Add(task);
            else pending.Add(task);
        }

        var alterations = await _db.OrderAlterations.AsNoTracking()
            .Where(x => x.AssignedKarigarId == karigarId
                && (x.Status == AlterationStatus.Received || x.Status == AlterationStatus.InProgress))
            .OrderBy(x => x.DeliveryDate ?? x.CreatedOn)
            .Take(200)
            .Select(x => new OrderAlterationViewModel
            {
                Id = x.Id, OrderId = x.OrderId, OrderItemId = x.OrderItemId, Description = x.Description,
                RequiredChange = x.RequiredChange, Status = x.Status, AdditionalCharge = x.AdditionalCharge,
                DeliveryDate = x.DeliveryDate, AssignedKarigarId = x.AssignedKarigarId, AssignedKarigarName = karigar.Name,
                Notes = x.Notes, CreatedOn = x.CreatedOn
            })
            .ToListAsync(ct);

        return ApiResponse<KarigarWorkQueueViewModel>.Ok(new KarigarWorkQueueViewModel
        {
            KarigarId           = karigarId,
            KarigarName         = karigar.Name,
            TotalPending        = pending.Count,
            TotalInProgress     = inProgress.Count,
            TotalRework         = pending.Count(t => t.IsRework) + inProgress.Count(t => t.IsRework),
            TotalCompletedToday = completedToday,
            PendingTasks        = pending.OrderByDescending(t => t.IsOverdue).ThenByDescending(t => t.Priority).ThenBy(t => t.OrderDeliveryDate).ToList(),
            InProgressTasks     = inProgress,
            AlterationTasks     = alterations
        });
    }

    public async Task<ApiResponse<List<KarigarPerformanceSummaryViewModel>>> GetAllKarigarsSummaryAsync(CancellationToken ct = default)
    {
        var assigned = await _db.OrderItemStageAssignments.AsNoTracking()
            .Where(a => a.AssignedKarigarId.HasValue && !a.OrderItem.IsDeleted && a.OrderItem.Order.Status != OrderStatus.Cancelled)
            .GroupBy(a => new { KarigarId = a.AssignedKarigarId!.Value, a.Stage })
            .Select(g => new { g.Key.KarigarId, g.Key.Stage, Count = g.Count() })
            .ToListAsync(ct);

        var logs = await _db.OrderItemStageLogs.AsNoTracking()
            .GroupBy(l => new { l.KarigarId, l.Status, Open = l.CompletedAt == null })
            .Select(g => new { g.Key.KarigarId, g.Key.Status, g.Key.Open, Count = g.Count() })
            .ToListAsync(ct);

        var earnings = await _db.KarigarLedgerEntries.AsNoTracking()
            .GroupBy(e => new { e.KarigarId, e.Stage })
            .Select(g => new
            {
                g.Key.KarigarId, g.Key.Stage,
                Earned  = g.Where(e => e.EntryType == KarigarLedgerEntryType.Earning).Sum(e => e.Amount),
                Balance = g.Sum(e => e.SignedAmount)
            })
            .ToListAsync(ct);

        var ids = assigned.Select(a => a.KarigarId).Concat(logs.Select(l => l.KarigarId)).Distinct().ToList();
        var users = await _db.AppUsers.AsNoTracking().Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Phone }).ToDictionaryAsync(u => u.Id, ct);

        var result = ids.Select(id =>
        {
            var mine = logs.Where(l => l.KarigarId == id).ToList();
            var totalAssigned = assigned.Where(a => a.KarigarId == id).Sum(a => a.Count);
            var completed = mine.Where(l => l.Status == StageLogStatus.Completed).Sum(l => l.Count);
            var inProgress = mine.Where(l => l.Status == StageLogStatus.InProgress && l.Open).Sum(l => l.Count);
            var rework = mine.Where(l => l.Status == StageLogStatus.ReworkRequired).Sum(l => l.Count);
            var stages = assigned.Where(a => a.KarigarId == id).Select(a => a.Stage)
                .Union(earnings.Where(e => e.KarigarId == id && e.Stage.HasValue).Select(e => e.Stage!.Value)).Distinct();

            return new KarigarPerformanceSummaryViewModel
            {
                KarigarId       = id,
                KarigarName     = users.GetValueOrDefault(id)?.Name ?? "Unknown",
                Phone           = users.GetValueOrDefault(id)?.Phone,
                TotalAssigned   = totalAssigned,
                TotalCompleted  = completed,
                TotalInProgress = inProgress,
                TotalRework     = rework,
                TotalPending    = Math.Max(0, totalAssigned - completed - inProgress),
                TotalEarnings   = earnings.Where(e => e.KarigarId == id).Sum(e => e.Earned),
                Balance         = earnings.Where(e => e.KarigarId == id).Sum(e => e.Balance),
                StageBreakdown  = stages.Select(s => new StageBreakdownViewModel
                {
                    Stage    = s,
                    Count    = assigned.Where(a => a.KarigarId == id && a.Stage == s).Sum(a => a.Count),
                    Earnings = earnings.Where(e => e.KarigarId == id && e.Stage == s).Sum(e => e.Earned)
                }).OrderBy(b => b.Stage).ToList()
            };
        }).OrderByDescending(k => k.TotalCompleted).ToList();

        return ApiResponse<List<KarigarPerformanceSummaryViewModel>>.Ok(result);
    }

    public async Task<ApiResponse<KarigarCompletedHistoryViewModel>> GetMyCompletedAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var karigarId = _currentUser.UserId;
        var karigar = await _db.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == karigarId, ct);

        var q = _db.OrderItemStageLogs.AsNoTracking()
            .Where(l => l.KarigarId == karigarId && l.Status == StageLogStatus.Completed && l.CompletedAt.HasValue);
        if (from.HasValue) q = q.Where(l => l.CompletedAt >= from.Value);
        if (to.HasValue)   q = q.Where(l => l.CompletedAt < to.Value.Date.AddDays(1));

        var logs = await q.OrderByDescending(l => l.CompletedAt).Take(1000)
            .Select(l => new
            {
                l.Id, l.OrderItemId, l.Stage, l.CreatedOn, CompletedAt = l.CompletedAt!.Value,
                l.OrderItem.OrderId, l.OrderItem.GarmentType, l.OrderItem.Order.OrderNumber,
                CustomerName = l.OrderItem.Order.Customer.Name
            })
            .ToListAsync(ct);

        var logIds = logs.Select(l => l.Id).ToList();
        var earned = await _db.KarigarLedgerEntries.AsNoTracking()
            .Where(e => e.StageLogId != null && logIds.Contains(e.StageLogId.Value) && e.EntryType == KarigarLedgerEntryType.Earning)
            .ToDictionaryAsync(e => e.StageLogId!.Value, e => e.Amount, ct);

        var stages = logs.Select(l => new KarigarCompletedStageViewModel
        {
            OrderItemId  = l.OrderItemId,
            OrderId      = l.OrderId,
            OrderNumber  = l.OrderNumber,
            CustomerName = l.CustomerName,
            GarmentType  = l.GarmentType,
            Stage        = l.Stage,
            StagePrice   = earned.TryGetValue(l.Id, out var amount) ? amount : null,
            StartedAt    = l.CreatedOn,
            CompletedAt  = l.CompletedAt
        }).ToList();

        return ApiResponse<KarigarCompletedHistoryViewModel>.Ok(new KarigarCompletedHistoryViewModel
        {
            KarigarId       = karigarId,
            KarigarName     = karigar?.Name ?? string.Empty,
            TotalCompleted  = stages.Count,
            TotalEarnings   = stages.Sum(s => s.StagePrice ?? 0),
            CompletedStages = stages
        });
    }

    private static Dictionary<string, string> ReadAttributes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new(); }
        catch (JsonException) { return new(); }
    }
}
