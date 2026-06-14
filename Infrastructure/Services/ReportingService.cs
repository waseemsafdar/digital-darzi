using Application.Common;
using Application.Interfaces.Services;
using Application.ViewModels.Reporting;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class ReportingService : IReportingService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ReportingService(ApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db          = db;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<DashboardSummaryViewModel>> GetDashboardAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var orders = await _db.Orders.AsNoTracking()
            .Where(o => o.CreatedOn >= from && o.CreatedOn <= to)
            .ToListAsync(ct);

        var allOrders = await _db.Orders.AsNoTracking()
            .Where(o => o.Status != OrderStatus.Cancelled)
            .ToListAsync(ct);

        var today = DateTime.UtcNow.Date;
        var overdue = allOrders.Count(o => o.DeliveryDate.Date < today
            && o.Status != OrderStatus.Delivered && o.Status != OrderStatus.Cancelled);
        var dueToday = allOrders.Count(o => o.DeliveryDate.Date == today
            && o.Status != OrderStatus.Delivered && o.Status != OrderStatus.Cancelled);

        var expenses = await _db.ShopExpenses.AsNoTracking()
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0;

        var salaries = await _db.StaffSalaries.AsNoTracking()
            .Where(s => s.PaidOn >= from && s.PaidOn <= to)
            .SumAsync(s => (decimal?)s.NetSalary, ct) ?? 0;

        var customers = await _db.Customers.AsNoTracking().CountAsync(ct);
        var newCustomers = await _db.Customers.AsNoTracking()
            .CountAsync(c => c.CreatedOn >= from && c.CreatedOn <= to, ct);

        var revenue = orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.GrandTotal);
        var collected = orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.AmountPaid);
        var totalDue = orders.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.BalanceDue);

        var periodOrders = orders.Count(o => o.Status != OrderStatus.Cancelled);

        var daily = await GetDailyRevenueAsync(from, to, ct);
        var topCust = await GetTopCustomersAsync(from, to, 5, ct);
        var stageQueues = await GetStageQueueCountsAsync(ct);

        return ApiResponse<DashboardSummaryViewModel>.Ok(new DashboardSummaryViewModel
        {
            From                   = from,
            To                     = to,
            TotalOrders            = orders.Count,
            PendingOrders          = orders.Count(o => o.Status != OrderStatus.Delivered && o.Status != OrderStatus.Cancelled),
            DeliveredOrders        = orders.Count(o => o.Status == OrderStatus.Delivered),
            CancelledOrders        = orders.Count(o => o.Status == OrderStatus.Cancelled),
            OverdueOrders          = overdue,
            DueTodayOrders         = dueToday,
            TotalRevenue           = revenue,
            TotalCollected         = collected,
            TotalBalanceDue        = totalDue,
            TotalExpenses          = expenses,
            TotalSalaries          = salaries,
            NetProfit              = collected - expenses - salaries,
            TotalCustomers         = customers,
            NewCustomersThisPeriod = newCustomers,
            AverageOrderValue      = periodOrders > 0 ? Math.Round(revenue / periodOrders, 2) : 0,
            DailyRevenue           = daily.Data ?? new(),
            TopCustomers           = topCust.Data ?? new(),
            StageQueueCounts       = stageQueues
        });
    }

    public async Task<ApiResponse<RevenueReportViewModel>> GetRevenueReportAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var orders = await _db.Orders.AsNoTracking()
            .Include(o => o.Payments)
            .Where(o => o.CreatedOn >= from && o.CreatedOn <= to && o.Status != OrderStatus.Cancelled)
            .ToListAsync(ct);

        var revenue    = orders.Sum(o => o.GrandTotal);
        var collected  = orders.Sum(o => o.AmountPaid);
        var discount   = orders.Sum(o => o.Discount);
        var due        = orders.Sum(o => o.BalanceDue);
        var expenses   = await _db.ShopExpenses.AsNoTracking()
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0;
        var salaries   = await _db.StaffSalaries.AsNoTracking()
            .Where(s => s.PaidOn >= from && s.PaidOn <= to)
            .SumAsync(s => (decimal?)s.NetSalary, ct) ?? 0;

        // By garment type
        var byGarment = await _db.OrderItems.AsNoTracking()
            .Include(i => i.Order)
            .Where(i => i.Order.CreatedOn >= from && i.Order.CreatedOn <= to && i.Order.Status != OrderStatus.Cancelled)
            .GroupBy(i => i.GarmentType)
            .Select(g => new RevenueByGarmentViewModel
            {
                GarmentType = g.Key,
                Count       = g.Count(),
                Revenue     = g.Sum(i => i.Price * i.Qty)
            })
            .OrderByDescending(g => g.Revenue)
            .ToListAsync(ct);

        // By payment method
        var byMethod = await _db.OrderPayments.AsNoTracking()
            .Include(p => p.Order)
            .Where(p => p.PaidAt >= from && p.PaidAt <= to)
            .GroupBy(p => p.PaymentMethod)
            .Select(g => new RevenueByPaymentMethodViewModel
            {
                PaymentMethod = g.Key,
                Amount        = g.Sum(p => p.Amount),
                Count         = g.Count()
            })
            .OrderByDescending(g => g.Amount)
            .ToListAsync(ct);

        var daily = await GetDailyRevenueAsync(from, to, ct);

        return ApiResponse<RevenueReportViewModel>.Ok(new RevenueReportViewModel
        {
            From              = from,
            To                = to,
            TotalRevenue      = revenue,
            TotalCollected    = collected,
            TotalDiscount     = discount,
            TotalBalanceDue   = due,
            TotalExpenses     = expenses,
            TotalSalaries     = salaries,
            GrossProfit       = revenue - expenses,
            NetProfit         = collected - expenses - salaries,
            TotalOrders       = orders.Count,
            AverageOrderValue = orders.Count > 0 ? Math.Round(revenue / orders.Count, 2) : 0,
            DailyBreakdown    = daily.Data ?? new(),
            ByGarmentType     = byGarment,
            ByPaymentMethod   = byMethod
        });
    }

    public async Task<ApiResponse<List<TopCustomerViewModel>>> GetTopCustomersAsync(DateTime from, DateTime to, int limit, CancellationToken ct = default)
    {
        var result = await _db.Orders.AsNoTracking()
            .Include(o => o.Customer)
            .Where(o => o.CreatedOn >= from && o.CreatedOn <= to
                     && o.Status != OrderStatus.Cancelled
                     && o.CustomerId != Guid.Empty)
            .GroupBy(o => new { o.CustomerId, o.Customer!.Name, o.Customer.Phone,
                                o.Customer.LoyaltyTier, o.Customer.LoyaltyPoints })
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
            .Take(limit)
            .ToListAsync(ct);

        return ApiResponse<List<TopCustomerViewModel>>.Ok(result);
    }

    public async Task<ApiResponse<List<StaffPerformanceViewModel>>> GetStaffPerformanceAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        // Stage completions by karigar
        var completions = await _db.OrderItemStageLogs.AsNoTracking()
            .Where(l => l.CompletedAt.HasValue && l.CompletedAt >= from && l.CompletedAt <= to)
            .GroupBy(l => l.KarigarId)
            .Select(g => new { KarigarId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // Earnings from stage assignments
        var earnings = await _db.OrderItemStageAssignments.AsNoTracking()
            .Where(a => a.AssignedKarigarId.HasValue && a.StagePrice.HasValue)
            .GroupBy(a => a.AssignedKarigarId!.Value)
            .Select(g => new { KarigarId = g.Key, Total = g.Sum(a => a.StagePrice ?? 0) })
            .ToListAsync(ct);

        // Attendance in period
        var attendance = await _db.StaffAttendances.AsNoTracking()
            .Where(a => a.Date >= from && a.Date <= to)
            .GroupBy(a => a.StaffUserId)
            .Select(g => new
            {
                StaffId    = g.Key,
                Present    = g.Count(a => a.Status == AttendanceStatus.Present),
                Absent     = g.Count(a => a.Status == AttendanceStatus.Absent),
                Total      = g.Count()
            })
            .ToListAsync(ct);

        var staffIds = completions.Select(c => c.KarigarId)
            .Union(earnings.Select(e => e.KarigarId))
            .Union(attendance.Select(a => a.StaffId))
            .Distinct().ToList();

        var users = await _db.AppUsers.AsNoTracking()
            .Where(u => staffIds.Contains(u.Id))
            .ToListAsync(ct);

        var completionMap  = completions.ToDictionary(c => c.KarigarId, c => c.Count);
        var earningsMap    = earnings.ToDictionary(e => e.KarigarId, e => e.Total);
        var attendanceMap  = attendance.ToDictionary(a => a.StaffId);

        var result = users.Select(u =>
        {
            var att  = attendanceMap.GetValueOrDefault(u.Id);
            var total = att?.Total ?? 0;
            return new StaffPerformanceViewModel
            {
                UserId             = u.Id,
                Name               = u.Name,
                Phone              = u.Phone,
                Roles              = new List<string>(),    // Could be fetched from Identity
                StagesCompleted    = completionMap.GetValueOrDefault(u.Id, 0),
                TotalKarigarEarnings= earningsMap.GetValueOrDefault(u.Id, 0),
                PresentDays        = att?.Present ?? 0,
                AbsentDays         = att?.Absent ?? 0,
                AttendanceRate     = total > 0 ? Math.Round((decimal)(att?.Present ?? 0) / total * 100, 1) : 0
            };
        })
        .OrderByDescending(s => s.StagesCompleted)
        .ToList();

        return ApiResponse<List<StaffPerformanceViewModel>>.Ok(result);
    }

    public async Task<ApiResponse<OrderStatusReportViewModel>> GetOrderStatusReportAsync(CancellationToken ct = default)
    {
        var statusCounts = await _db.Orders.AsNoTracking()
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int Get(OrderStatus s) => statusCounts.FirstOrDefault(x => x.Status == s)?.Count ?? 0;

        var today = DateTime.UtcNow.Date;
        var overdue = await _db.Orders.AsNoTracking()
            .CountAsync(o => o.DeliveryDate < today
                && o.Status != OrderStatus.Delivered
                && o.Status != OrderStatus.Cancelled, ct);

        var stageQueues = await GetStageQueueCountsAsync(ct);

        return ApiResponse<OrderStatusReportViewModel>.Ok(new OrderStatusReportViewModel
        {
            TotalActive     = statusCounts.Where(x => x.Status != OrderStatus.Delivered && x.Status != OrderStatus.Cancelled).Sum(x => x.Count),
            Pending         = Get(OrderStatus.Pending),
            Cutting         = Get(OrderStatus.Cutting),
            Stitching       = Get(OrderStatus.Stitching),
            Embroidery      = Get(OrderStatus.Embroidery),
            Finishing       = Get(OrderStatus.Finishing),
            ReadyToDeliver  = Get(OrderStatus.ReadyToDeliver),
            Delivered       = Get(OrderStatus.Delivered),
            Cancelled       = Get(OrderStatus.Cancelled),
            Overdue         = overdue,
            StageQueues     = stageQueues
        });
    }

    public async Task<ApiResponse<List<PendingBalanceViewModel>>> GetPendingBalancesAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        var orders = await _db.Orders.AsNoTracking()
            .Include(o => o.Customer)
            .Where(o => o.BalanceDue > 0 && o.Status != OrderStatus.Cancelled)
            .OrderByDescending(o => o.BalanceDue)
            .Select(o => new PendingBalanceViewModel
            {
                OrderId       = o.Id,
                OrderNumber   = o.OrderNumber,
                CustomerId    = o.CustomerId,
                CustomerName  = o.Customer != null ? o.Customer.Name : string.Empty,
                CustomerPhone = o.Customer != null ? o.Customer.Phone : string.Empty,
                GrandTotal    = o.GrandTotal,
                AmountPaid    = o.AmountPaid,
                BalanceDue    = o.BalanceDue,
                DeliveryDate  = o.DeliveryDate,
                Status        = o.Status,
                DaysOverdue   = o.DeliveryDate.Date < today
                    ? (int)(today - o.DeliveryDate.Date).TotalDays : 0
            })
            .ToListAsync(ct);

        return ApiResponse<List<PendingBalanceViewModel>>.Ok(orders);
    }

    public async Task<ApiResponse<List<DailyRevenueViewModel>>> GetDailyRevenueAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var payments = await _db.OrderPayments.AsNoTracking()
            .Where(p => p.PaidAt >= from && p.PaidAt <= to)
            .GroupBy(p => p.PaidAt.Date)
            .Select(g => new { Date = g.Key, Collected = g.Sum(p => p.Amount), Count = g.Count() })
            .ToListAsync(ct);

        var orderRevenue = await _db.Orders.AsNoTracking()
            .Where(o => o.CreatedOn >= from && o.CreatedOn <= to && o.Status != OrderStatus.Cancelled)
            .GroupBy(o => o.CreatedOn.Date)
            .Select(g => new { Date = g.Key, Revenue = g.Sum(o => o.GrandTotal), Orders = g.Count() })
            .ToListAsync(ct);

        // Merge by date
        var allDates = payments.Select(p => p.Date)
            .Union(orderRevenue.Select(r => r.Date))
            .OrderBy(d => d)
            .ToList();

        var revenueMap   = orderRevenue.ToDictionary(r => r.Date);
        var collectedMap = payments.ToDictionary(p => p.Date);

        var result = allDates.Select(date => new DailyRevenueViewModel
        {
            Date       = date,
            Revenue    = revenueMap.TryGetValue(date, out var r) ? r.Revenue : 0,
            Collected  = collectedMap.TryGetValue(date, out var p) ? p.Collected : 0,
            OrderCount = revenueMap.TryGetValue(date, out var ro) ? ro.Orders : 0
        }).ToList();

        return ApiResponse<List<DailyRevenueViewModel>>.Ok(result);
    }

    // ── Internal helper ──────────────────────────────────────────────────────
    private async Task<List<StageQueueCountViewModel>> GetStageQueueCountsAsync(CancellationToken ct = default)
    {
        var stages = Enum.GetValues<ProductionStage>();
        var result = new List<StageQueueCountViewModel>();

        foreach (var stage in stages)
        {
            var pending    = await _db.OrderItemStageAssignments.AsNoTracking()
                .CountAsync(a => a.Stage == stage
                    && a.OrderItem.Status == OrderItemStatus.Pending, ct);

            var inProgress = await _db.OrderItemStageLogs.AsNoTracking()
                .CountAsync(l => l.Stage == stage && !l.CompletedAt.HasValue, ct);

            var completed  = await _db.OrderItemStageLogs.AsNoTracking()
                .CountAsync(l => l.Stage == stage && l.CompletedAt.HasValue, ct);

            result.Add(new StageQueueCountViewModel
            {
                Stage      = stage,
                Pending    = pending,
                InProgress = inProgress,
                Completed  = completed
            });
        }

        return result;
    }
}
