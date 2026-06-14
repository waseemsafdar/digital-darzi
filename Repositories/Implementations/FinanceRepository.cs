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
        var query = _db.ShopExpenses.AsNoTracking()
            .Include(e => e.AddedByUser)
            .AsQueryable();

        if (from.HasValue) query = query.Where(e => e.ExpenseDate >= from.Value);
        if (to.HasValue)   query = query.Where(e => e.ExpenseDate <= to.Value);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(e => e.Category == category);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.ExpenseDate)
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

    public async Task<ShopExpense> CreateExpenseAsync(ShopExpense expense, CancellationToken ct = default)
    {
        _db.ShopExpenses.Add(expense);
        await _db.SaveChangesAsync(ct);
        return expense;
    }

    public async Task UpdateExpenseAsync(ShopExpense expense, CancellationToken ct = default)
    {
        _db.ShopExpenses.Update(expense);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteExpenseAsync(Guid id, CancellationToken ct = default)
    {
        var e = await GetExpenseByIdAsync(id, ct);
        if (e == null) return;
        _db.ShopExpenses.Remove(e);
        await _db.SaveChangesAsync(ct);
    }

    // ── Salaries ─────────────────────────────────────────────────────────────
    public async Task<PagedResult<StaffSalaryListViewModel>> GetSalariesAsync(
        int? month, int? year, Guid? staffId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.StaffSalaries.AsNoTracking()
            .Include(s => s.StaffUser)
            .AsQueryable();

        if (month.HasValue) query = query.Where(s => s.Month == month.Value);
        if (year.HasValue)  query = query.Where(s => s.Year == year.Value);
        if (staffId.HasValue) query = query.Where(s => s.StaffUserId == staffId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(s => s.Year).ThenByDescending(s => s.Month)
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
                PaymentMethod = s.PaymentMethod,
                PaidOn        = s.PaidOn
            })
            .ToListAsync(ct);

        return PagedResult<StaffSalaryListViewModel>.From(items, total, page, pageSize);
    }

    public async Task<StaffSalary?> GetSalaryByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.StaffSalaries.FindAsync(new object[] { id }, ct);

    public async Task<StaffSalary> CreateSalaryAsync(StaffSalary salary, CancellationToken ct = default)
    {
        _db.StaffSalaries.Add(salary);
        await _db.SaveChangesAsync(ct);
        return salary;
    }

    // ── Attendance ─────────────────────────────────────────────────────────────
    public async Task<PagedResult<AttendanceListViewModel>> GetAttendanceAsync(
        Guid? staffId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _db.StaffAttendances.AsNoTracking()
            .Include(a => a.StaffUser)
            .AsQueryable();

        if (staffId.HasValue) query = query.Where(a => a.StaffUserId == staffId.Value);
        if (from.HasValue) query = query.Where(a => a.Date >= from.Value);
        if (to.HasValue)   query = query.Where(a => a.Date <= to.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.Date)
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

    public async Task<StaffAttendance?> GetAttendanceByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.StaffAttendances.FindAsync(new object[] { id }, ct);

    public async Task<StaffAttendance> RecordAttendanceAsync(StaffAttendance attendance, CancellationToken ct = default)
    {
        _db.StaffAttendances.Add(attendance);
        await _db.SaveChangesAsync(ct);
        return attendance;
    }

    public async Task UpdateAttendanceAsync(StaffAttendance attendance, CancellationToken ct = default)
    {
        _db.StaffAttendances.Update(attendance);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<AttendanceSummaryViewModel> GetAttendanceSummaryAsync(
        Guid staffId, int month, int year, CancellationToken ct = default)
    {
        var records = await _db.StaffAttendances
            .Include(a => a.StaffUser)
            .Where(a => a.StaffUserId == staffId && a.Date.Month == month && a.Date.Year == year)
            .ToListAsync(ct);

        var staffName = records.FirstOrDefault()?.StaffUser?.Name
            ?? await _db.AppUsers.IgnoreQueryFilters().Where(u => u.Id == staffId).Select(u => u.Name).FirstOrDefaultAsync(ct)
            ?? string.Empty;

        return new AttendanceSummaryViewModel
        {
            StaffUserId     = staffId,
            StaffName       = staffName,
            Month           = month,
            Year            = year,
            PresentDays     = records.Count(r => r.Status == AttendanceStatus.Present),
            AbsentDays      = records.Count(r => r.Status == AttendanceStatus.Absent),
            HalfDays        = records.Count(r => r.Status == AttendanceStatus.HalfDay),
            LeaveDays       = records.Count(r => r.Status == AttendanceStatus.Leave),
            TotalWorkingDays= records.Count
        };
    }

    // ── Finance Summary ────────────────────────────────────────────────────────
    public async Task<FinanceSummaryViewModel> GetFinanceSummaryAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var totalRevenue = await _db.Orders
            .Where(o => o.CreatedOn >= from && o.CreatedOn <= to && o.Status != Domain.Enums.OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.GrandTotal, ct) ?? 0;

        var totalCollected = await _db.Orders
            .Where(o => o.CreatedOn >= from && o.CreatedOn <= to && o.Status != Domain.Enums.OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.AmountPaid, ct) ?? 0;

        var totalDue = await _db.Orders
            .Where(o => o.CreatedOn >= from && o.CreatedOn <= to && o.Status != Domain.Enums.OrderStatus.Cancelled)
            .SumAsync(o => (decimal?)o.BalanceDue, ct) ?? 0;

        var totalExpenses = await _db.ShopExpenses
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0;

        var totalSalaries = await _db.StaffSalaries
            .Where(s => s.PaidOn >= from && s.PaidOn <= to)
            .SumAsync(s => (decimal?)s.NetSalary, ct) ?? 0;

        var totalOrders = await _db.Orders.CountAsync(o => o.CreatedOn >= from && o.CreatedOn <= to, ct);
        var pendingOrders = await _db.Orders.CountAsync(
            o => o.CreatedOn >= from && o.CreatedOn <= to
              && o.Status != Domain.Enums.OrderStatus.Delivered
              && o.Status != Domain.Enums.OrderStatus.Cancelled, ct);

        var expenseBreakdown = await _db.ShopExpenses
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to)
            .GroupBy(e => e.Category)
            .Select(g => new ExpenseByCategoryViewModel
            {
                Category = g.Key,
                Amount   = g.Sum(e => e.Amount),
                Count    = g.Count()
            })
            .OrderByDescending(x => x.Amount)
            .ToListAsync(ct);

        return new FinanceSummaryViewModel
        {
            TotalRevenue     = totalRevenue,
            TotalCollected   = totalCollected,
            TotalBalanceDue  = totalDue,
            TotalExpenses    = totalExpenses,
            TotalSalaries    = totalSalaries,
            NetProfit        = totalCollected - totalExpenses - totalSalaries,
            TotalOrders      = totalOrders,
            PendingOrders    = pendingOrders,
            ExpenseBreakdown = expenseBreakdown
        };
    }
}
