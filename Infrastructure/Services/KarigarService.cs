using Application.Common;
using Application.Interfaces.Services;
using Application.ViewModels.Order;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class KarigarService : IKarigarService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public KarigarService(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<KarigarWorkQueueViewModel>> GetMyWorkQueueAsync(CancellationToken ct = default)
        => await GetKarigarWorkQueueAsync(_currentUser.UserId, ct);

    public async Task<ApiResponse<KarigarWorkQueueViewModel>> GetKarigarWorkQueueAsync(Guid karigarId, CancellationToken ct = default)
    {
        // Get karigar name
        var karigar = await _db.AppUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == karigarId, ct);

        // Get all pending stage assignments for this karigar
        var assignments = await _db.OrderItemStageAssignments
            .AsNoTracking()
            .Include(a => a.OrderItem)
                .ThenInclude(i => i.Order)
                    .ThenInclude(o => o.Customer)
            .Where(a => a.AssignedKarigarId == karigarId
                     && a.OrderItem.Status != OrderItemStatus.Done
                     && a.OrderItem.Status != OrderItemStatus.Delivered
                     && a.OrderItem.Status != OrderItemStatus.Cancelled
                     && a.OrderItem.Order.Status != OrderStatus.Cancelled
                     && a.OrderItem.Order.Status != OrderStatus.Delivered)
            .OrderBy(a => a.OrderItem.Order.DeliveryDate)
            .ToListAsync(ct);

        // Get log status per assignment to determine pending vs in-progress
        var itemIds = assignments.Select(a => a.OrderItemId).Distinct().ToList();
        var activeLogs = await _db.OrderItemStageLogs
            .AsNoTracking()
            .Where(l => itemIds.Contains(l.OrderItemId) && l.KarigarId == karigarId && !l.CompletedAt.HasValue)
            .ToListAsync(ct);

        var activeLogSet = activeLogs.Select(l => (l.OrderItemId, l.Stage)).ToHashSet();

        // Completed today
        var today = DateTime.UtcNow.Date;
        var completedToday = await _db.OrderItemStageLogs.AsNoTracking()
            .CountAsync(l => l.KarigarId == karigarId
                          && l.CompletedAt.HasValue
                          && l.CompletedAt.Value.Date == today, ct);

        var pending    = new List<KarigarStageTaskViewModel>();
        var inProgress = new List<KarigarStageTaskViewModel>();

        foreach (var a in assignments)
        {
            var task = new KarigarStageTaskViewModel
            {
                OrderItemId       = a.OrderItemId,
                OrderId           = a.OrderItem.OrderId,
                OrderNumber       = a.OrderItem.Order.OrderNumber,
                CustomerName      = a.OrderItem.Order.Customer?.Name ?? string.Empty,
                CustomerPhone     = a.OrderItem.Order.Customer?.Phone ?? string.Empty,
                GarmentType       = a.OrderItem.GarmentType,
                Stage             = a.Stage,
                OrderDeliveryDate = a.OrderItem.Order.DeliveryDate,
                StagePrice        = a.StagePrice,
                EstimatedDays     = a.EstimatedDays,
                StyleNotes        = a.OrderItem.StyleNotes,
                FabricColor       = a.OrderItem.FabricColor,
                AssignedOn        = a.CreatedOn
            };

            if (activeLogSet.Contains((a.OrderItemId, a.Stage)))
                inProgress.Add(task);
            else
                pending.Add(task);
        }

        return ApiResponse<KarigarWorkQueueViewModel>.Ok(new KarigarWorkQueueViewModel
        {
            KarigarId          = karigarId,
            KarigarName        = karigar?.Name ?? string.Empty,
            TotalPending       = pending.Count,
            TotalInProgress    = inProgress.Count,
            TotalCompletedToday= completedToday,
            PendingTasks       = pending,
            InProgressTasks    = inProgress
        });
    }

    public async Task<ApiResponse<List<KarigarPerformanceSummaryViewModel>>> GetAllKarigarsSummaryAsync(CancellationToken ct = default)
    {
        // Get all stage assignments grouped by karigar
        var assignmentGroups = await _db.OrderItemStageAssignments
            .AsNoTracking()
            .Where(a => a.AssignedKarigarId.HasValue)
            .GroupBy(a => a.AssignedKarigarId!.Value)
            .Select(g => new
            {
                KarigarId    = g.Key,
                TotalAssigned= g.Count(),
                TotalEarnings= g.Sum(a => a.StagePrice ?? 0),
                ByStage      = g.GroupBy(a => a.Stage)
                                .Select(sg => new { sg.Key, Count = sg.Count(), Earnings = sg.Sum(a => a.StagePrice ?? 0) })
                                .ToList()
            })
            .ToListAsync(ct);

        // Completed counts from logs
        var completedByKarigar = await _db.OrderItemStageLogs
            .AsNoTracking()
            .Where(l => l.CompletedAt.HasValue)
            .GroupBy(l => new { l.KarigarId, l.Stage })
            .Select(g => new { g.Key.KarigarId, g.Key.Stage, Count = g.Count() })
            .ToListAsync(ct);

        var completedMap = completedByKarigar
            .GroupBy(x => x.KarigarId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Count));

        // Karigar details
        var karigarIds = assignmentGroups.Select(g => g.KarigarId).ToList();
        var karigarUsers = await _db.AppUsers.AsNoTracking()
            .Where(u => karigarIds.Contains(u.Id))
            .ToListAsync(ct);
        var karigarMap = karigarUsers.ToDictionary(u => u.Id);

        var result = assignmentGroups.Select(g =>
        {
            var totalCompleted  = completedMap.GetValueOrDefault(g.KarigarId, 0);
            var totalPending    = g.TotalAssigned - totalCompleted;
            var karigar         = karigarMap.GetValueOrDefault(g.KarigarId);

            return new KarigarPerformanceSummaryViewModel
            {
                KarigarId      = g.KarigarId,
                KarigarName    = karigar?.Name ?? "Unknown",
                Phone          = karigar?.Phone,
                TotalAssigned  = g.TotalAssigned,
                TotalCompleted = totalCompleted,
                TotalPending   = Math.Max(0, totalPending),
                TotalInProgress= 0,  // Can be computed from logs if needed
                TotalEarnings  = g.TotalEarnings,
                StageBreakdown = g.ByStage.Select(s => new StageBreakdownViewModel
                {
                    Stage    = s.Key,
                    Count    = s.Count,
                    Earnings = s.Earnings
                }).ToList()
            };
        }).OrderByDescending(k => k.TotalCompleted).ToList();

        return ApiResponse<List<KarigarPerformanceSummaryViewModel>>.Ok(result);
    }
}
