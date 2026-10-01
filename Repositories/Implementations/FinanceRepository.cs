using Application.Common;
using Application.Interfaces.Repositories;
using Application.ViewModels.Finance;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Implementations;

public class FinanceRepository : IFinanceRepository
{
    private readonly ApplicationDbContext _db;
    public FinanceRepository(ApplicationDbContext db) => _db = db;

    // ── Expenses ─────────────────────────────────────────────────────────────
    public async Task<PagedResult<ShopExpenseListViewModel>> GetExpensesAsync(
        DateTime? from, DateTime? to, string? category, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var query = _db.ShopExpenses.AsNoTracking();

        if (from.HasValue) query = query.Where(e => e.ExpenseDate >= from.Value);
        if (to.HasValue)   query = query.Where(e => e.ExpenseDate <= to.Value);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(e => EF.Functions.ILike(e.Category, Like.Escape(category.Trim()), Like.EscapeChar));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.ExpenseDate).ThenByDescending(e => e.CreatedOn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new ShopExpenseListViewModel
            {
                Id            = e.Id,
                Category      = e.Category,
                Description   = e.Description,
                Amount        = e.Amount,
                ExpenseDate   = e.ExpenseDate,
                PaymentMethod = e.PaymentMethod,
                ReceiptRef    = e.ReceiptRef,
                AddedByName   = e.AddedByUser != null ? e.AddedByUser.Name : string.Empty
            })
            .ToListAsync(ct);

        return PagedResult<ShopExpenseListViewModel>.From(items, total, page, pageSize);
    }

    public async Task<ShopExpense?> GetExpenseByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.ShopExpenses.FirstOrDefaultAsync(e => e.Id == id, ct);

    // ── Salaries ─────────────────────────────────────────────────────────────
    public async Task<PagedResult<StaffSalaryListViewModel>> GetSalariesAsync(
        int? month, int? year, Guid? staffId, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var query = _db.StaffSalaries.AsNoTracking();

        if (month.HasValue)   query = query.Where(s => s.Month == month.Value);
        if (year.HasValue)    query = query.Where(s => s.Year == year.Value);
        if (staffId.HasValue) query = query.Where(s => s.StaffUserId == staffId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(s => s.Year).ThenByDescending(s => s.Month).ThenBy(s => s.StaffUserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new StaffSalaryListViewModel
            {
                Id            = s.Id,
                StaffUserId   = s.StaffUserId,
                StaffName     = s.StaffUser != null ? s.StaffUser.Name : string.Empty,
                Month         = s.Month,
                Year          = s.Year,
                BaseSalary    = s.BaseSalary,
                Bonus         = s.Bonus,
                Deduction     = s.Deduction,
                NetSalary     = s.NetSalary,
                AmountPaid    = s.AmountPaid,
                Status        = s.Status,
                PaymentMethod = s.PaymentMethod,
                PaidOn        = s.PaidOn
            })
            .ToListAsync(ct);

        return PagedResult<StaffSalaryListViewModel>.From(items, total, page, pageSize);
    }

    // ── Attendance ─────────────────────────────────────────────────────────────
    public async Task<PagedResult<AttendanceListViewModel>> GetAttendanceAsync(
        Guid? staffId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize, 50);
        var query = _db.StaffAttendances.AsNoTracking();

        if (staffId.HasValue) query = query.Where(a => a.StaffUserId == staffId.Value);
        if (from.HasValue)    query = query.Where(a => a.Date >= from.Value);
        if (to.HasValue)      query = query.Where(a => a.Date <= to.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.Date).ThenBy(a => a.StaffUserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AttendanceListViewModel
            {
                Id          = a.Id,
                StaffUserId = a.StaffUserId,
                StaffName   = a.StaffUser != null ? a.StaffUser.Name : string.Empty,
                Date        = a.Date,
                Status      = a.Status,
                CheckIn     = a.CheckIn,
                CheckOut    = a.CheckOut,
                Notes       = a.Notes
            })
            .ToListAsync(ct);

        return PagedResult<AttendanceListViewModel>.From(items, total, page, pageSize);
    }

    public async Task<AttendanceSummaryViewModel> GetAttendanceSummaryAsync(Guid staffId, int month, int year, CancellationToken ct = default)
    {
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);

        var counts = await _db.StaffAttendances.AsNoTracking()
            .Where(a => a.StaffUserId == staffId && a.Date >= start && a.Date < end)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int Count(AttendanceStatus s) => counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0;
        var staffName = await _db.AppUsers.AsNoTracking().Where(u => u.Id == staffId).Select(u => u.Name).FirstOrDefaultAsync(ct);

        return new AttendanceSummaryViewModel
        {
            StaffUserId      = staffId,
            StaffName        = staffName ?? string.Empty,
            Month            = month,
            Year             = year,
            PresentDays      = Count(AttendanceStatus.Present),
            AbsentDays       = Count(AttendanceStatus.Absent),
            HalfDays         = Count(AttendanceStatus.HalfDay),
            LeaveDays        = Count(AttendanceStatus.Leave),
            TotalWorkingDays = counts.Sum(c => c.Count)
        };
    }

    // ── Finance Summary ────────────────────────────────────────────────────────
    /// <summary>
    /// Cash-basis summary for [from, to]. Revenue = orders booked in the window. Collected = payment
    /// lines received in the window, net of refunds. Costs = expenses, salaries actually paid, and
    /// karigar payouts/advances in the window.
    /// </summary>
    public async Task<FinanceSummaryViewModel> GetFinanceSummaryAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var orders = _db.Orders.AsNoTracking().Where(o => o.CreatedOn >= from && o.CreatedOn <= to);

        var orderTotals = await orders
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Revenue = g.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.GrandTotal),
                Due     = g.Where(o => o.Status != OrderStatus.Cancelled).Sum(o => o.BalanceDue),
                Count   = g.Count(),
                Pending = g.Count(o => o.Status != OrderStatus.Delivered && o.Status != OrderStatus.Cancelled)
            })
            .FirstOrDefaultAsync(ct);

        var payments = await _db.OrderPayments.AsNoTracking()
            .Where(p => p.PaidAt >= from && p.PaidAt <= to)
            .GroupBy(p => p.IsRefund)
            .Select(g => new { IsRefund = g.Key, Amount = g.Sum(p => p.Amount) })
            .ToListAsync(ct);
        var collected = payments.Where(p => !p.IsRefund).Sum(p => p.Amount);
        var refunds = -payments.Where(p => p.IsRefund).Sum(p => p.Amount);

        var expenses = _db.ShopExpenses.AsNoTracking().Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to);
        var totalExpenses = await expenses.SumAsync(e => (decimal?)e.Amount, ct) ?? 0;
        var expenseBreakdown = await expenses
            .GroupBy(e => e.Category)
            .Select(g => new ExpenseByCategoryViewModel { Category = g.Key, Amount = g.Sum(e => e.Amount), Count = g.Count() })
            .OrderByDescending(x => x.Amount)
            .ToListAsync(ct);

        var totalSalaries = await _db.StaffSalaries.AsNoTracking()
            .Where(s => s.PaidOn >= from && s.PaidOn <= to)
            .SumAsync(s => (decimal?)s.AmountPaid, ct) ?? 0;

        var karigarPayments = await _db.KarigarLedgerEntries.AsNoTracking()
            .Where(e => e.EntryDate >= from && e.EntryDate <= to
                && (e.EntryType == KarigarLedgerEntryType.Payment || e.EntryType == KarigarLedgerEntryType.Advance))
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0;

        var netCollected = collected - refunds;
        return new FinanceSummaryViewModel
        {
            TotalRevenue         = orderTotals?.Revenue ?? 0,
            TotalCollected       = netCollected,
            TotalRefunds         = refunds,
            TotalBalanceDue      = orderTotals?.Due ?? 0,
            TotalExpenses        = totalExpenses,
            TotalSalaries        = totalSalaries,
            TotalKarigarPayments = karigarPayments,
            NetProfit            = netCollected - totalExpenses - totalSalaries - karigarPayments,
            TotalOrders          = orderTotals?.Count ?? 0,
            PendingOrders        = orderTotals?.Pending ?? 0,
            ExpenseBreakdown     = expenseBreakdown
        };
    }
}
