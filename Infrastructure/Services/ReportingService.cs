using Application.Common;
using Application.Interfaces.Services;
using Application.ViewModels.Reporting;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

/// <summary>
/// Reports built from grouped database queries (the previous version loaded whole order tables
/// into memory). Day buckets use the branch's local calendar. With <c>tenantWide</c> the
/// branch filter is lifted and every branch of the tenant is included.
/// </summary>
public class ReportingService : IReportingService
{
    private static readonly OrderStatus[] Closed = { OrderStatus.Delivered, OrderStatus.Cancelled };
    private static readonly OrderStatus[] ProductionStatuses =
    {
        OrderStatus.Pending, OrderStatus.Received, OrderStatus.Cutting, OrderStatus.Stitching, OrderStatus.Embroidery,
        OrderStatus.Finishing, OrderStatus.TrialReady, OrderStatus.TrialDone, OrderStatus.FullyPaid
    };

    private readonly ApplicationDbContext _db;
    private readonly IBusinessCalendar _calendar;

    public ReportingService(ApplicationDbContext db, IBusinessCalendar calendar)
    {
        _db = db;
        _calendar = calendar;
    }

    private IQueryable<T> Q<T>(bool tenantWide) where T : BaseDBModel
        => tenantWide ? _db.TenantWide<T>().AsNoTracking() : _db.Set<T>().AsNoTracking();

    // ── Dashboard ────────────────────────────────────────────────────────────

    public async Task<ApiResponse<DashboardSummaryViewModel>> GetDashboardAsync(DateTime from, DateTime to, bool tenantWide = false, CancellationToken ct = default)
    {
        var tz = await _calendar.GetTimeZoneAsync(ct);
        var today = ShopCalendar.Today(_calendar.UtcNow, tz);
        var tomorrow = today.AddDays(1);
        var (todayStart, todayEnd) = ShopCalendar.LocalDayBounds(today, tz);

        var orders = Q<Order>(tenantWide);
        var period = await orders.Where(o => o.CreatedOn >= from && o.CreatedOn <= to)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total        = g.Count(),
                Pending      = g.Count(o => o.Status != OrderStatus.Delivered && o.Status != OrderStatus.Cancelled),
                Delivered    = g.Count(o => o.Status == OrderStatus.Delivered),
                Cancelled    = g.Count(o => o.Status == OrderStatus.Cancelled),
                NonCancelled = g.Count(o => o.Status != OrderStatus.Cancelled),
                Revenue      = g.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.GrandTotal),
                Due          = g.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.BalanceDue)
            }).FirstOrDefaultAsync(ct);

        var current = await orders.Where(o => !Closed.Contains(o.Status))
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Active       = g.Count(),
                Overdue      = g.Count(o => o.DeliveryDate < today),
                DueToday     = g.Count(o => o.DeliveryDate >= today && o.DeliveryDate < tomorrow),
                DueTomorrow  = g.Count(o => o.DeliveryDate >= tomorrow && o.DeliveryDate < tomorrow.AddDays(1)),
                Ready        = g.Count(o => o.Status == OrderStatus.ReadyToDeliver || o.Status == OrderStatus.PartiallyDelivered),
                InProduction = g.Count(o => ProductionStatuses.Contains(o.Status))
            }).FirstOrDefaultAsync(ct);

        var outstanding = await orders.Where(o => o.Status != OrderStatus.Cancelled && o.BalanceDue > 0).SumAsync(o => (decimal?)o.BalanceDue, ct) ?? 0;

        var todayOrders = await orders.Where(o => o.CreatedOn >= todayStart && o.CreatedOn < todayEnd)
            .GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), Sales = g.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.GrandTotal) })
            .FirstOrDefaultAsync(ct);

        var payments = Q<OrderPayment>(tenantWide);
        var collected = await payments.Where(p => p.PaidAt >= from && p.PaidAt <= to).SumAsync(p => (decimal?)p.Amount, ct) ?? 0;
        var todayCollections = await payments.Where(p => p.PaidAt >= todayStart && p.PaidAt < todayEnd).SumAsync(p => (decimal?)p.Amount, ct) ?? 0;

        var expenses = await Q<ShopExpense>(tenantWide).Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to).SumAsync(e => (decimal?)e.Amount, ct) ?? 0;
        var todayExpenses = await Q<ShopExpense>(tenantWide).Where(e => e.ExpenseDate == today).SumAsync(e => (decimal?)e.Amount, ct) ?? 0;
        var salaries = await Q<StaffSalary>(tenantWide).Where(s => s.PaidOn >= from && s.PaidOn <= to).SumAsync(s => (decimal?)s.AmountPaid, ct) ?? 0;
        var karigarPaid = await Q<KarigarLedgerEntry>(tenantWide)
            .Where(e => e.EntryDate >= from && e.EntryDate <= to
                && (e.EntryType == KarigarLedgerEntryType.Payment || e.EntryType == KarigarLedgerEntryType.Advance))
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0;

        var customers = await Q<Customer>(tenantWide).CountAsync(ct);
        var newCustomers = await Q<Customer>(tenantWide).CountAsync(c => c.CreatedOn >= from && c.CreatedOn <= to, ct);

        var logs = Q<OrderItemStageLog>(tenantWide);
        var rework = await logs.CountAsync(l => l.Status == StageLogStatus.ReworkRequired
            && !logs.Any(x => x.OrderItemId == l.OrderItemId && x.Stage == l.Stage && x.CreatedOn > l.CreatedOn), ct);
        var openAlterations = await Q<OrderAlteration>(tenantWide)
            .CountAsync(a => a.Status == AlterationStatus.Received || a.Status == AlterationStatus.InProgress, ct);

        var revenue = period?.Revenue ?? 0;
        var nonCancelled = period?.NonCancelled ?? 0;

        return ApiResponse<DashboardSummaryViewModel>.Ok(new DashboardSummaryViewModel
        {
            From                       = from,
            To                         = to,
            TotalOrders                = period?.Total ?? 0,
            PendingOrders              = period?.Pending ?? 0,
            DeliveredOrders            = period?.Delivered ?? 0,
            CancelledOrders            = period?.Cancelled ?? 0,
            OverdueOrders              = current?.Overdue ?? 0,
            DueTodayOrders             = current?.DueToday ?? 0,
            DueTomorrowOrders          = current?.DueTomorrow ?? 0,
            ActiveOrders               = current?.Active ?? 0,
            InProduction               = current?.InProduction ?? 0,
            ReadyForDelivery           = current?.Ready ?? 0,
            TotalRevenue               = revenue,
            TotalCollected             = collected,
            TotalBalanceDue            = period?.Due ?? 0,
            TotalExpenses              = expenses,
            TotalSalaries              = salaries,
            NetProfit                  = collected - expenses - salaries - karigarPaid,
            TotalCustomers             = customers,
            NewCustomersThisPeriod     = newCustomers,
            TodayOrders                = todayOrders?.Count ?? 0,
            TodaySales                 = todayOrders?.Sales ?? 0,
            TodayCollections           = todayCollections,
            TodayExpenses              = todayExpenses,
            ReworkItems                = rework,
            OpenAlterations            = openAlterations,
            OutstandingCustomerBalance = outstanding,
            KarigarWorkload            = await GetKarigarWorkloadAsync(tenantWide, today, ct),
            AverageOrderValue          = nonCancelled > 0 ? Math.Round(revenue / nonCancelled, 2) : 0,
            DailyRevenue               = (await GetDailyRevenueAsync(from, to, tenantWide, ct)).Data ?? new(),
            TopCustomers               = (await GetTopCustomersAsync(from, to, 5, tenantWide, ct)).Data ?? new(),
            StageQueueCounts           = await GetStageQueueCountsAsync(tenantWide, ct)
        });
    }

    // ── Revenue ──────────────────────────────────────────────────────────────

    public async Task<ApiResponse<RevenueReportViewModel>> GetRevenueReportAsync(DateTime from, DateTime to, bool tenantWide = false, CancellationToken ct = default)
    {
        var inWindow = Q<Order>(tenantWide).Where(o => o.CreatedOn >= from && o.CreatedOn <= to && o.Status != OrderStatus.Cancelled);
        var totals = await inWindow.GroupBy(_ => 1)
            .Select(g => new
            {
                Count    = g.Count(),
                Revenue  = g.Sum(o => o.GrandTotal),
                Discount = g.Sum(o => o.Discount),
                Due      = g.Sum(o => o.BalanceDue)
            }).FirstOrDefaultAsync(ct);

        var payments = Q<OrderPayment>(tenantWide).Where(p => p.PaidAt >= from && p.PaidAt <= to);
        var collected = await payments.SumAsync(p => (decimal?)p.Amount, ct) ?? 0;
        var expenses = await Q<ShopExpense>(tenantWide).Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to).SumAsync(e => (decimal?)e.Amount, ct) ?? 0;
        var salaries = await Q<StaffSalary>(tenantWide).Where(s => s.PaidOn >= from && s.PaidOn <= to).SumAsync(s => (decimal?)s.AmountPaid, ct) ?? 0;
        var karigarPaid = await Q<KarigarLedgerEntry>(tenantWide)
            .Where(e => e.EntryDate >= from && e.EntryDate <= to
                && (e.EntryType == KarigarLedgerEntryType.Payment || e.EntryType == KarigarLedgerEntryType.Advance))
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0;

        var byGarment = await Q<OrderItem>(tenantWide)
            .Where(i => i.Order.CreatedOn >= from && i.Order.CreatedOn <= to && i.Order.Status != OrderStatus.Cancelled
                && i.Status != OrderItemStatus.Cancelled)
            .GroupBy(i => i.GarmentType)
            .Select(g => new RevenueByGarmentViewModel
            {
                GarmentType = g.Key,
                Count       = g.Sum(i => i.Qty),
                Revenue     = g.Sum(i => i.Price * i.Qty + i.FabricCharge)
            })
            .OrderByDescending(g => g.Revenue)
            .ToListAsync(ct);

        var byMethod = await payments.Where(p => !p.IsRefund)
            .GroupBy(p => p.PaymentMethod)
            .Select(g => new RevenueByPaymentMethodViewModel { PaymentMethod = g.Key, Amount = g.Sum(p => p.Amount), Count = g.Count() })
            .OrderByDescending(g => g.Amount)
            .ToListAsync(ct);

        var revenue = totals?.Revenue ?? 0;
        var count = totals?.Count ?? 0;
        return ApiResponse<RevenueReportViewModel>.Ok(new RevenueReportViewModel
        {
            From              = from,
            To                = to,
            TotalRevenue      = revenue,
            TotalCollected    = collected,
            TotalDiscount     = totals?.Discount ?? 0,
            TotalBalanceDue   = totals?.Due ?? 0,
            TotalExpenses     = expenses,
            TotalSalaries     = salaries,
            GrossProfit       = revenue - expenses,
            NetProfit         = collected - expenses - salaries - karigarPaid,
            TotalOrders       = count,
            AverageOrderValue = count > 0 ? Math.Round(revenue / count, 2) : 0,
            DailyBreakdown    = (await GetDailyRevenueAsync(from, to, tenantWide, ct)).Data ?? new(),
            ByGarmentType     = byGarment,
            ByPaymentMethod   = byMethod
        });
    }

    // ── Customers ────────────────────────────────────────────────────────────

    public async Task<ApiResponse<List<TopCustomerViewModel>>> GetTopCustomersAsync(DateTime from, DateTime to, int limit, bool tenantWide = false, CancellationToken ct = default)
    {
        var result = await Q<Order>(tenantWide)
            .Where(o => o.CreatedOn >= from && o.CreatedOn <= to && o.Status != OrderStatus.Cancelled)
            .GroupBy(o => new { o.CustomerId, o.Customer.Name, o.Customer.Phone, o.Customer.LoyaltyTier, o.Customer.LoyaltyPoints })
            .Select(g => new TopCustomerViewModel
            {
                CustomerId    = g.Key.CustomerId,
                Name          = g.Key.Name,
                Phone         = g.Key.Phone,
                OrderCount    = g.Count(),
                TotalSpend    = g.Sum(o => o.GrandTotal),
                BalanceDue    = g.Sum(o => o.BalanceDue),
                LoyaltyTier   = g.Key.LoyaltyTier,
                LoyaltyPoints = g.Key.LoyaltyPoints,
                LastOrderDate = g.Max(o => (DateTime?)o.CreatedOn)
            })
            .OrderByDescending(c => c.TotalSpend)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(ct);

        return ApiResponse<List<TopCustomerViewModel>>.Ok(result);
    }

    public async Task<ApiResponse<List<PendingBalanceViewModel>>> GetPendingBalancesAsync(int limit, bool tenantWide = false, CancellationToken ct = default)
    {
        var today = await _calendar.TodayAsync(ct);
        var rows = await Q<Order>(tenantWide)
            .Where(o => o.BalanceDue > 0 && o.Status != OrderStatus.Cancelled)
            .OrderByDescending(o => o.BalanceDue)
            .Take(Math.Clamp(limit, 1, 1000))
            .Select(o => new PendingBalanceViewModel
            {
                OrderId       = o.Id,
                OrderNumber   = o.OrderNumber,
                CustomerId    = o.CustomerId,
                CustomerName  = o.Customer.Name,
                CustomerPhone = o.Customer.Phone,
                GrandTotal    = o.GrandTotal,
                AmountPaid    = o.AmountPaid,
                BalanceDue    = o.BalanceDue,
                DeliveryDate  = o.DeliveryDate,
                Status        = o.Status
            })
            .ToListAsync(ct);

        foreach (var r in rows)
            r.DaysOverdue = r.DeliveryDate < today ? (int)(today - r.DeliveryDate.Date).TotalDays : 0;
        return ApiResponse<List<PendingBalanceViewModel>>.Ok(rows);
    }

    // ── Staff ────────────────────────────────────────────────────────────────

    public async Task<ApiResponse<List<StaffPerformanceViewModel>>> GetStaffPerformanceAsync(DateTime from, DateTime to, bool tenantWide = false, CancellationToken ct = default)
    {
        var completions = await Q<OrderItemStageLog>(tenantWide)
            .Where(l => l.Status == StageLogStatus.Completed && l.CompletedAt >= from && l.CompletedAt <= to)
            .GroupBy(l => l.KarigarId)
            .Select(g => new { KarigarId = g.Key, Count = g.Count(), Orders = g.Select(l => l.OrderItem.OrderId).Distinct().Count() })
            .ToListAsync(ct);

        var earnings = await Q<KarigarLedgerEntry>(tenantWide)
            .Where(e => e.EntryType == KarigarLedgerEntryType.Earning && e.EntryDate >= from && e.EntryDate <= to)
            .GroupBy(e => e.KarigarId)
            .Select(g => new { KarigarId = g.Key, Total = g.Sum(e => e.Amount) })
            .ToListAsync(ct);

        var attendance = await Q<StaffAttendance>(tenantWide)
            .Where(a => a.Date >= from && a.Date <= to)
            .GroupBy(a => a.StaffUserId)
            .Select(g => new
            {
                StaffId = g.Key,
                Present = g.Count(a => a.Status == AttendanceStatus.Present),
                Half    = g.Count(a => a.Status == AttendanceStatus.HalfDay),
                Absent  = g.Count(a => a.Status == AttendanceStatus.Absent),
                Total   = g.Count()
            })
            .ToListAsync(ct);

        var staffIds = completions.Select(c => c.KarigarId)
            .Union(earnings.Select(e => e.KarigarId))
            .Union(attendance.Select(a => a.StaffId))
            .Distinct().ToList();

        var users = await _db.AppUsers.AsNoTracking().Where(u => staffIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Phone, u.RoleIds }).ToListAsync(ct);
        var roleNames = await _db.Roles.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Name ?? string.Empty, ct);

        var result = users.Select(u =>
        {
            var att = attendance.FirstOrDefault(a => a.StaffId == u.Id);
            var done = completions.FirstOrDefault(c => c.KarigarId == u.Id);
            var total = att?.Total ?? 0;
            return new StaffPerformanceViewModel
            {
                UserId               = u.Id,
                Name                 = u.Name,
                Phone                = u.Phone,
                Roles                = u.RoleIds.Select(r => roleNames.GetValueOrDefault(r)).Where(r => !string.IsNullOrEmpty(r)).Select(r => r!).ToList(),
                OrdersHandled        = done?.Orders ?? 0,
                StagesCompleted      = done?.Count ?? 0,
                TotalKarigarEarnings = earnings.FirstOrDefault(e => e.KarigarId == u.Id)?.Total ?? 0,
                PresentDays          = att?.Present ?? 0,
                AbsentDays           = att?.Absent ?? 0,
                AttendanceRate       = total > 0 ? Math.Round(((att?.Present ?? 0) + (att?.Half ?? 0) * 0.5m) / total * 100, 1) : 0
            };
        })
        .OrderByDescending(s => s.StagesCompleted)
        .ToList();

        return ApiResponse<List<StaffPerformanceViewModel>>.Ok(result);
    }

    // ── Orders ───────────────────────────────────────────────────────────────

    public async Task<ApiResponse<OrderStatusReportViewModel>> GetOrderStatusReportAsync(bool tenantWide = false, CancellationToken ct = default)
    {
        var statusCounts = await Q<Order>(tenantWide)
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int Get(OrderStatus s) => statusCounts.FirstOrDefault(x => x.Status == s)?.Count ?? 0;

        var today = await _calendar.TodayAsync(ct);
        var overdue = await Q<Order>(tenantWide).CountAsync(o => o.DeliveryDate < today && !Closed.Contains(o.Status), ct);

        return ApiResponse<OrderStatusReportViewModel>.Ok(new OrderStatusReportViewModel
        {
            TotalActive    = statusCounts.Where(x => !Closed.Contains(x.Status)).Sum(x => x.Count),
            Pending        = Get(OrderStatus.Pending) + Get(OrderStatus.Received) + Get(OrderStatus.FullyPaid),
            Cutting        = Get(OrderStatus.Cutting),
            Stitching      = Get(OrderStatus.Stitching),
            Embroidery     = Get(OrderStatus.Embroidery),
            Finishing      = Get(OrderStatus.Finishing) + Get(OrderStatus.TrialReady) + Get(OrderStatus.TrialDone),
            ReadyToDeliver = Get(OrderStatus.ReadyToDeliver) + Get(OrderStatus.PartiallyDelivered),
            Delivered      = Get(OrderStatus.Delivered),
            Cancelled      = Get(OrderStatus.Cancelled),
            Overdue        = overdue,
            StageQueues    = await GetStageQueueCountsAsync(tenantWide, ct)
        });
    }

    /// <summary>Per shop-local day: orders booked, money collected (net of refunds) and alteration upsell.</summary>
    public async Task<ApiResponse<List<DailyRevenueViewModel>>> GetDailyRevenueAsync(DateTime from, DateTime to, bool tenantWide = false, CancellationToken ct = default)
    {
        if ((to - from).TotalDays > 400) from = to.AddDays(-400);   // bound the flat pull
        var tz = await _calendar.GetTimeZoneAsync(ct);

        var payments = await Q<OrderPayment>(tenantWide).Where(p => p.PaidAt >= from && p.PaidAt <= to)
            .Select(p => new { p.PaidAt, p.Amount }).ToListAsync(ct);
        var orders = await Q<Order>(tenantWide).Where(o => o.CreatedOn >= from && o.CreatedOn <= to && o.Status != OrderStatus.Cancelled)
            .Select(o => new { o.CreatedOn, o.GrandTotal }).ToListAsync(ct);
        var alterations = await Q<OrderAlteration>(tenantWide)
            .Where(a => a.CreatedOn >= from && a.CreatedOn <= to && a.Status != AlterationStatus.Cancelled)
            .Select(a => new { a.CreatedOn, a.AdditionalCharge }).ToListAsync(ct);

        var collected = payments.GroupBy(p => ShopCalendar.LocalDateOf(p.PaidAt, tz)).ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));
        var revenue = orders.GroupBy(o => ShopCalendar.LocalDateOf(o.CreatedOn, tz))
            .ToDictionary(g => g.Key, g => (Revenue: g.Sum(o => o.GrandTotal), Count: g.Count()));
        var upsell = alterations.GroupBy(a => ShopCalendar.LocalDateOf(a.CreatedOn, tz)).ToDictionary(g => g.Key, g => g.Sum(a => a.AdditionalCharge));

        var result = collected.Keys.Union(revenue.Keys).Union(upsell.Keys).OrderBy(d => d)
            .Select(date => new DailyRevenueViewModel
            {
                Date         = date,
                Revenue      = revenue.TryGetValue(date, out var r) ? r.Revenue : 0,
                OrderCount   = revenue.TryGetValue(date, out var rc) ? rc.Count : 0,
                Collected    = collected.GetValueOrDefault(date),
                UpsellAmount = upsell.GetValueOrDefault(date)
            }).ToList();

        return ApiResponse<List<DailyRevenueViewModel>>.Ok(result);
    }

    public async Task<ApiResponse<List<BranchComparisonViewModel>>> GetBranchComparisonAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var shops = await _db.Shops.AsNoTracking().Select(s => new { s.Id, s.Name }).ToListAsync(ct);

        var orders = await _db.TenantWide<Order>().AsNoTracking()
            .GroupBy(o => o.BranchId)
            .Select(g => new
            {
                BranchId    = g.Key,
                Orders      = g.Count(o => o.CreatedOn >= from && o.CreatedOn <= to),
                Revenue     = g.Where(o => o.CreatedOn >= from && o.CreatedOn <= to && o.Status != OrderStatus.Cancelled).Sum(o => o.GrandTotal),
                Outstanding = g.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.BalanceDue),
                Active      = g.Count(o => o.Status != OrderStatus.Delivered && o.Status != OrderStatus.Cancelled)
            }).ToListAsync(ct);
        var payments = await _db.TenantWide<OrderPayment>().AsNoTracking()
            .Where(p => p.PaidAt >= from && p.PaidAt <= to)
            .GroupBy(p => p.BranchId).Select(g => new { BranchId = g.Key, Amount = g.Sum(p => p.Amount) }).ToListAsync(ct);
        var expenses = await _db.TenantWide<ShopExpense>().AsNoTracking()
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to)
            .GroupBy(e => e.BranchId).Select(g => new { BranchId = g.Key, Amount = g.Sum(e => e.Amount) }).ToListAsync(ct);

        return ApiResponse<List<BranchComparisonViewModel>>.Ok(shops.Select(s =>
        {
            var o = orders.FirstOrDefault(x => x.BranchId == s.Id);
            return new BranchComparisonViewModel
            {
                ShopId             = s.Id,
                ShopName           = s.Name,
                Orders             = o?.Orders ?? 0,
                Revenue            = o?.Revenue ?? 0,
                OutstandingBalance = o?.Outstanding ?? 0,
                ActiveOrders       = o?.Active ?? 0,
                Collected          = payments.FirstOrDefault(p => p.BranchId == s.Id)?.Amount ?? 0,
                Expenses           = expenses.FirstOrDefault(e => e.BranchId == s.Id)?.Amount ?? 0
            };
        }).OrderByDescending(b => b.Revenue).ToList());
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    /// <summary>Three grouped queries (previously three queries per stage).</summary>
    private async Task<List<StageQueueCountViewModel>> GetStageQueueCountsAsync(bool tenantWide, CancellationToken ct)
    {
        var logs = Q<OrderItemStageLog>(tenantWide);
        var pending = await Q<OrderItemStageAssignment>(tenantWide)
            .Where(a => !a.OrderItem.IsDeleted && a.OrderItem.Status != OrderItemStatus.Cancelled
                && a.OrderItem.Order.Status != OrderStatus.Cancelled && a.OrderItem.Order.Status != OrderStatus.Delivered
                && !logs.Any(l => l.OrderItemId == a.OrderItemId && l.Stage == a.Stage))
            .GroupBy(a => a.Stage)
            .Select(g => new { Stage = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byStatus = await logs
            .GroupBy(l => new { l.Stage, l.Status })
            .Select(g => new { g.Key.Stage, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        return Enum.GetValues<ProductionStage>().Select(stage => new StageQueueCountViewModel
        {
            Stage      = stage,
            Pending    = pending.FirstOrDefault(p => p.Stage == stage)?.Count ?? 0,
            InProgress = byStatus.Where(s => s.Stage == stage && s.Status == StageLogStatus.InProgress).Sum(s => s.Count),
            Completed  = byStatus.Where(s => s.Stage == stage && s.Status == StageLogStatus.Completed).Sum(s => s.Count)
        }).ToList();
    }

    private async Task<List<KarigarWorkloadViewModel>> GetKarigarWorkloadAsync(bool tenantWide, DateTime today, CancellationToken ct)
    {
        var logs = Q<OrderItemStageLog>(tenantWide);
        var open = await Q<OrderItemStageAssignment>(tenantWide)
            .Where(a => a.AssignedKarigarId != null
                && !a.OrderItem.IsDeleted && a.OrderItem.Status != OrderItemStatus.Cancelled
                && a.OrderItem.Order.Status != OrderStatus.Cancelled && a.OrderItem.Order.Status != OrderStatus.Delivered
                && !logs.Any(l => l.OrderItemId == a.OrderItemId && l.Stage == a.Stage && l.Status == StageLogStatus.Completed))
            .GroupBy(a => a.AssignedKarigarId!.Value)
            .Select(g => new
            {
                KarigarId  = g.Key,
                Open       = g.Count(),
                Overdue    = g.Count(a => a.OrderItem.Order.DeliveryDate < today),
                InProgress = g.Count(a => logs.Any(l => l.OrderItemId == a.OrderItemId && l.Stage == a.Stage
                                                     && l.Status == StageLogStatus.InProgress && l.CompletedAt == null))
            })
            .OrderByDescending(x => x.Open)
            .Take(10)
            .ToListAsync(ct);

        var ids = open.Select(o => o.KarigarId).ToList();
        var names = await _db.AppUsers.IgnoreQueryFilters().AsNoTracking()
            .Where(u => ids.Contains(u.Id) && u.TenantId == _db.CurrentTenantId)
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        return open.Select(o => new KarigarWorkloadViewModel
        {
            KarigarId = o.KarigarId, KarigarName = names.GetValueOrDefault(o.KarigarId) ?? "Unknown",
            OpenTasks = o.Open, InProgress = o.InProgress, OverdueTasks = o.Overdue
        }).ToList();
    }
}
