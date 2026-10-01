using Application.Common;
using Application.Interfaces.Services;
using Application.ViewModels.Operations;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

// ─────────────────────────────────────────────────────────────────────────────
// Cash register / daily closing
// ─────────────────────────────────────────────────────────────────────────────

public interface ICashService
{
    Task<ApiResponse<CashSessionViewModel>> OpenAsync(OpenCashSessionViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<CashSessionViewModel>> GetCurrentAsync(CancellationToken ct = default);
    Task<ApiResponse<CashSessionViewModel>> AddMovementAsync(CashMovementRequestViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<CashSessionViewModel>> CloseAsync(CloseCashSessionViewModel vm, CancellationToken ct = default);
    Task<ApiResponse<PagedResult<CashSessionViewModel>>> GetSessionsAsync(int page, int pageSize, CancellationToken ct = default);
}

/// <summary>
/// One open till per branch. Expected cash = opening float + cash payments − cash refunds
/// − cash expenses − cash salaries − cash karigar payouts ± manual in/out, all within the session.
/// Closing records the counted cash and the variance; a closed session is never edited.
/// </summary>
public sealed class CashService : ICashService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBusinessCalendar _calendar;

    public CashService(ApplicationDbContext db, ICurrentUserService currentUser, IBusinessCalendar calendar)
    {
        _db = db;
        _currentUser = currentUser;
        _calendar = calendar;
    }

    public async Task<ApiResponse<CashSessionViewModel>> OpenAsync(OpenCashSessionViewModel vm, CancellationToken ct = default)
    {
        if (vm.OpeningFloat < 0) return ApiResponse<CashSessionViewModel>.Fail("Opening float cannot be negative.");
        if (await _db.CashSessions.AnyAsync(s => s.Status == CashSessionStatus.Open, ct))
            return ApiResponse<CashSessionViewModel>.Conflict("A cash session is already open for this branch. Close it first.");

        var session = new CashSession
        {
            Status = CashSessionStatus.Open, OpenedBy = _currentUser.UserId, OpenedAt = _calendar.UtcNow,
            OpeningFloat = vm.OpeningFloat, Notes = vm.Notes
        };
        _db.CashSessions.Add(session);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<CashSessionViewModel>.Ok(await BuildAsync(session, ct), "Cash session opened.");
    }

    public async Task<ApiResponse<CashSessionViewModel>> GetCurrentAsync(CancellationToken ct = default)
    {
        var session = await _db.CashSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Status == CashSessionStatus.Open, ct);
        return session == null
            ? ApiResponse<CashSessionViewModel>.NotFound("No cash session is open for this branch.")
            : ApiResponse<CashSessionViewModel>.Ok(await BuildAsync(session, ct));
    }

    public async Task<ApiResponse<CashSessionViewModel>> AddMovementAsync(CashMovementRequestViewModel vm, CancellationToken ct = default)
    {
        if (vm.Amount <= 0) return ApiResponse<CashSessionViewModel>.Fail("Amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(vm.Reason)) return ApiResponse<CashSessionViewModel>.Fail("A reason is required.");
        var session = await _db.CashSessions.FirstOrDefaultAsync(s => s.Status == CashSessionStatus.Open, ct);
        if (session == null) return ApiResponse<CashSessionViewModel>.NotFound("No cash session is open for this branch.");

        _db.CashMovements.Add(new CashMovement
        {
            SessionId = session.Id, Direction = vm.Direction, Amount = vm.Amount, Reason = vm.Reason.Trim(), MovementAt = _calendar.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        return ApiResponse<CashSessionViewModel>.Ok(await BuildAsync(session, ct), "Cash movement recorded.");
    }

    public async Task<ApiResponse<CashSessionViewModel>> CloseAsync(CloseCashSessionViewModel vm, CancellationToken ct = default)
    {
        if (vm.CountedCash < 0) return ApiResponse<CashSessionViewModel>.Fail("Counted cash cannot be negative.");
        var session = await _db.CashSessions.FirstOrDefaultAsync(s => s.Status == CashSessionStatus.Open, ct);
        if (session == null) return ApiResponse<CashSessionViewModel>.NotFound("No cash session is open for this branch.");

        var now = _calendar.UtcNow;
        var summary = await BuildAsync(session, ct, now);
        session.Status = CashSessionStatus.Closed;
        session.ClosedBy = _currentUser.UserId;
        session.ClosedAt = now;
        session.ExpectedCash = summary.ExpectedCash;
        session.CountedCash = vm.CountedCash;
        session.Variance = vm.CountedCash - summary.ExpectedCash;
        if (vm.Notes != null) session.Notes = string.IsNullOrWhiteSpace(session.Notes) ? vm.Notes : $"{session.Notes}; {vm.Notes}";
        await _db.SaveChangesAsync(ct);

        return ApiResponse<CashSessionViewModel>.Ok(await BuildAsync(session, ct),
            session.Variance == 0 ? "Cash session closed. The till balances." : $"Cash session closed with a variance of {session.Variance:N2}.");
    }

    public async Task<ApiResponse<PagedResult<CashSessionViewModel>>> GetSessionsAsync(int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var q = _db.CashSessions.AsNoTracking();
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(s => s.OpenedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var list = new List<CashSessionViewModel>();
        foreach (var s in rows) list.Add(await BuildAsync(s, ct));
        return ApiResponse<PagedResult<CashSessionViewModel>>.Ok(PagedResult<CashSessionViewModel>.From(list, total, page, pageSize));
    }

    private async Task<CashSessionViewModel> BuildAsync(CashSession s, CancellationToken ct, DateTime? asOf = null)
    {
        var end = s.ClosedAt ?? asOf ?? _calendar.UtcNow;
        var start = s.OpenedAt;

        var payments = await _db.OrderPayments.AsNoTracking()
            .Where(p => p.PaymentMethod == PaymentMethod.Cash && p.PaidAt >= start && p.PaidAt <= end)
            .GroupBy(p => p.IsRefund).Select(g => new { g.Key, Sum = g.Sum(p => p.Amount) }).ToListAsync(ct);
        var sales = payments.Where(p => !p.Key).Sum(p => p.Sum);
        var refunds = -payments.Where(p => p.Key).Sum(p => p.Sum);

        var expenses = await _db.ShopExpenses.AsNoTracking()
            .Where(e => e.PaymentMethod == PaymentMethod.Cash && e.CreatedOn >= start && e.CreatedOn <= end)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0;
        var salaries = await _db.StaffSalaries.AsNoTracking()
            .Where(x => x.PaymentMethod == PaymentMethod.Cash && x.PaidOn >= start && x.PaidOn <= end)
            .SumAsync(x => (decimal?)x.AmountPaid, ct) ?? 0;
        var karigar = await _db.KarigarLedgerEntries.AsNoTracking()
            .Where(e => e.PaymentMethod == PaymentMethod.Cash && e.CreatedOn >= start && e.CreatedOn <= end
                && (e.EntryType == KarigarLedgerEntryType.Payment || e.EntryType == KarigarLedgerEntryType.Advance))
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0;

        var movements = await _db.CashMovements.AsNoTracking().Where(m => m.SessionId == s.Id).OrderBy(m => m.MovementAt).ToListAsync(ct);
        var manualIn = movements.Where(m => m.Direction == CashMovementDirection.In).Sum(m => m.Amount);
        var manualOut = movements.Where(m => m.Direction == CashMovementDirection.Out).Sum(m => m.Amount);

        var expected = s.ExpectedCash ?? s.OpeningFloat + sales - refunds - expenses - salaries - karigar + manualIn - manualOut;
        var openedBy = await _db.AppUsers.AsNoTracking().Where(u => u.Id == s.OpenedBy).Select(u => u.Name).FirstOrDefaultAsync(ct);

        return new CashSessionViewModel
        {
            Id = s.Id, Status = s.Status, OpenedBy = s.OpenedBy, OpenedByName = openedBy, OpenedAt = s.OpenedAt,
            OpeningFloat = s.OpeningFloat, ClosedBy = s.ClosedBy, ClosedAt = s.ClosedAt, CashSales = sales,
            CashRefunds = refunds, CashExpenses = expenses, CashSalaries = salaries, CashKarigarPayments = karigar,
            ManualIn = manualIn, ManualOut = manualOut, ExpectedCash = expected, CountedCash = s.CountedCash,
            Variance = s.Variance, Notes = s.Notes,
            Movements = movements.Select(m => new CashMovementItemViewModel
            {
                Id = m.Id, Direction = m.Direction, Amount = m.Amount, Reason = m.Reason, MovementAt = m.MovementAt
            }).ToList()
        };
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Audit log
// ─────────────────────────────────────────────────────────────────────────────

public interface IAuditService
{
    Task<ApiResponse<PagedResult<AuditLogViewModel>>> SearchAsync(AuditSearchViewModel filter, CancellationToken ct = default);
}

public sealed class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;
    public AuditService(ApplicationDbContext db) => _db = db;

    /// <summary>Audit rows are platform-level; every query is pinned to the caller's tenant and branch.</summary>
    public async Task<ApiResponse<PagedResult<AuditLogViewModel>>> SearchAsync(AuditSearchViewModel filter, CancellationToken ct = default)
    {
        var (page, pageSize) = Paging.Normalize(filter.Page, filter.PageSize, 50);
        var q = _db.AuditLogs.AsNoTracking().Where(a => a.TenantId == _db.CurrentTenantId && a.BranchId == _db.CurrentShopId);
        if (!string.IsNullOrWhiteSpace(filter.EntityType)) q = q.Where(a => a.EntityType == filter.EntityType);
        if (filter.EntityId.HasValue) q = q.Where(a => a.EntityId == filter.EntityId);
        if (filter.UserId.HasValue) q = q.Where(a => a.UserId == filter.UserId);
        if (filter.From.HasValue) q = q.Where(a => a.CreatedOn >= filter.From);
        if (filter.To.HasValue) q = q.Where(a => a.CreatedOn <= filter.To);

        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(a => a.CreatedOn).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var userIds = rows.Select(r => r.UserId).Distinct().ToList();
        var names = await _db.AppUsers.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        var items = rows.Select(r => new AuditLogViewModel
        {
            Id = r.Id, UserId = r.UserId, UserName = names.GetValueOrDefault(r.UserId), Action = r.Action,
            EntityType = r.EntityType, EntityId = r.EntityId, OldValues = r.OldValues, NewValues = r.NewValues, CreatedOn = r.CreatedOn
        }).ToList();
        return ApiResponse<PagedResult<AuditLogViewModel>>.Ok(PagedResult<AuditLogViewModel>.From(items, total, page, pageSize));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Production work board (Kanban)
// ─────────────────────────────────────────────────────────────────────────────

public interface IWorkBoardService
{
    Task<ApiResponse<WorkBoardViewModel>> GetBoardAsync(ProductionStage stage, int perColumn, CancellationToken ct = default);
}

public sealed class WorkBoardService : IWorkBoardService
{
    private readonly ApplicationDbContext _db;
    private readonly IBusinessCalendar _calendar;

    public WorkBoardService(ApplicationDbContext db, IBusinessCalendar calendar)
    {
        _db = db;
        _calendar = calendar;
    }

    /// <summary>
    /// Columns for one stage across active orders: Unassigned (planned without a karigar),
    /// Assigned (not started), In progress, Rework, and Completed in the last 7 days.
    /// </summary>
    public async Task<ApiResponse<WorkBoardViewModel>> GetBoardAsync(ProductionStage stage, int perColumn, CancellationToken ct = default)
    {
        perColumn = Math.Clamp(perColumn, 1, 200);
        var today = await _calendar.TodayAsync(ct);
        var recent = _calendar.UtcNow.AddDays(-7);

        var rows = await _db.OrderItemStageAssignments.AsNoTracking()
            .Where(a => a.Stage == stage && !a.OrderItem.IsDeleted
                && a.OrderItem.Status != OrderItemStatus.Cancelled && a.OrderItem.Status != OrderItemStatus.Delivered
                && a.OrderItem.Order.Status != OrderStatus.Cancelled && a.OrderItem.Order.Status != OrderStatus.Delivered)
            .Select(a => new BoardRow
            {
                OrderItemId       = a.OrderItemId,
                AssignedKarigarId = a.AssignedKarigarId,
                KarigarName       = a.AssignedKarigar != null ? a.AssignedKarigar.Name : null,
                OrderId           = a.OrderItem.OrderId,
                OrderNumber       = a.OrderItem.Order.OrderNumber,
                CustomerName      = a.OrderItem.Order.Customer.Name,
                GarmentType       = a.OrderItem.GarmentType,
                Qty               = a.OrderItem.Qty,
                Priority          = a.OrderItem.Order.Priority,
                DeliveryDate      = a.OrderItem.Order.DeliveryDate,
                Latest            = a.OrderItem.StageLogs.Where(l => l.Stage == stage).OrderByDescending(l => l.CreatedOn)
                    .Select(l => new BoardLog { Status = l.Status, CreatedOn = l.CreatedOn, CompletedAt = l.CompletedAt, ReworkReason = l.ReworkReason })
                    .FirstOrDefault()
            })
            .OrderBy(r => r.DeliveryDate)
            .Take(2000)
            .ToListAsync(ct);

        var board = new WorkBoardViewModel { Stage = stage };
        foreach (var r in rows)
        {
            var list = r.Latest?.Status switch
            {
                StageLogStatus.Completed      => r.Latest.CompletedAt >= recent ? board.CompletedRecently : null,
                StageLogStatus.ReworkRequired => board.Rework,
                StageLogStatus.InProgress     => board.InProgress,
                _                             => r.AssignedKarigarId == null ? board.Unassigned : board.Assigned
            };
            if (list == null || list.Count >= perColumn) continue;
            list.Add(new WorkBoardCardViewModel
            {
                OrderId = r.OrderId, OrderItemId = r.OrderItemId, OrderNumber = r.OrderNumber, CustomerName = r.CustomerName,
                GarmentType = r.GarmentType, Qty = r.Qty, Priority = r.Priority, DeliveryDate = r.DeliveryDate,
                IsLate = r.DeliveryDate < today, DueToday = r.DeliveryDate == today,
                KarigarId = r.AssignedKarigarId, KarigarName = r.KarigarName,
                StartedAt = r.Latest?.CreatedOn, CompletedAt = r.Latest?.CompletedAt, ReworkReason = r.Latest?.ReworkReason
            });
        }
        return ApiResponse<WorkBoardViewModel>.Ok(board);
    }

    private sealed class BoardRow
    {
        public Guid OrderItemId { get; init; }
        public Guid? AssignedKarigarId { get; init; }
        public string? KarigarName { get; init; }
        public Guid OrderId { get; init; }
        public string OrderNumber { get; init; } = string.Empty;
        public string CustomerName { get; init; } = string.Empty;
        public GarmentType GarmentType { get; init; }
        public int Qty { get; init; }
        public OrderPriority Priority { get; init; }
        public DateTime DeliveryDate { get; init; }
        public BoardLog? Latest { get; init; }
    }

    private sealed class BoardLog
    {
        public StageLogStatus Status { get; init; }
        public DateTime CreatedOn { get; init; }
        public DateTime? CompletedAt { get; init; }
        public string? ReworkReason { get; init; }
    }
}
