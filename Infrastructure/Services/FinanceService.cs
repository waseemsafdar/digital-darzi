using Application.Common;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.ViewModels.Finance;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class FinanceService : IFinanceService
{
    private readonly IFinanceRepository _repo;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IBusinessCalendar _calendar;

    public FinanceService(IFinanceRepository repo, ApplicationDbContext db, ICurrentUserService currentUser, IBusinessCalendar calendar)
    {
        _repo = repo;
        _db = db;
        _currentUser = currentUser;
        _calendar = calendar;
    }

    // ── Expenses ─────────────────────────────────────────────────────────────

    public async Task<ApiResponse<PagedResult<ShopExpenseListViewModel>>> GetExpensesAsync(
        DateTime? from, DateTime? to, string? category, int page, int pageSize, CancellationToken ct = default)
        => ApiResponse<PagedResult<ShopExpenseListViewModel>>.Ok(await _repo.GetExpensesAsync(from, to, category, page, pageSize, ct));

    public async Task<ApiResponse<ShopExpenseListViewModel>> CreateExpenseAsync(CreateShopExpenseViewModel vm, CancellationToken ct = default)
    {
        var categoryError = await ValidateCategoryAsync(vm.Category, ct);
        if (categoryError != null) return ApiResponse<ShopExpenseListViewModel>.Fail(categoryError);

        var expense = new ShopExpense
        {
            Category      = await CanonicalCategoryAsync(vm.Category, ct),
            Description   = vm.Description,
            Amount        = vm.Amount,
            ExpenseDate   = await _calendar.ToCalendarDateAsync(vm.ExpenseDate, ct),
            PaymentMethod = vm.PaymentMethod,
            ReceiptRef    = vm.ReceiptRef,
            AddedByUserId = _currentUser.UserId,
            ActiveStatus  = ActiveStatus.Active
        };
        _db.ShopExpenses.Add(expense);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<ShopExpenseListViewModel>.Ok(ToListViewModel(expense), "Expense recorded.");
    }

    public async Task<ApiResponse<ShopExpenseListViewModel>> UpdateExpenseAsync(Guid id, UpdateShopExpenseViewModel vm, CancellationToken ct = default)
    {
        var expense = await _repo.GetExpenseByIdAsync(id, ct);
        if (expense == null) return ApiResponse<ShopExpenseListViewModel>.NotFound("Expense not found.");

        if (vm.Category != null)
        {
            var categoryError = await ValidateCategoryAsync(vm.Category, ct);
            if (categoryError != null) return ApiResponse<ShopExpenseListViewModel>.Fail(categoryError);
            expense.Category = await CanonicalCategoryAsync(vm.Category, ct);
        }
        if (vm.Description != null) expense.Description = vm.Description;
        if (vm.Amount.HasValue) expense.Amount = vm.Amount.Value;
        if (vm.ExpenseDate.HasValue) expense.ExpenseDate = await _calendar.ToCalendarDateAsync(vm.ExpenseDate.Value, ct);
        if (vm.PaymentMethod.HasValue) expense.PaymentMethod = vm.PaymentMethod.Value;
        if (vm.ReceiptRef != null) expense.ReceiptRef = vm.ReceiptRef;

        await _db.SaveChangesAsync(ct);
        return ApiResponse<ShopExpenseListViewModel>.Ok(ToListViewModel(expense), "Expense updated.");
    }

    /// <summary>Soft delete: the row stays for the audit trail and disappears from lists and totals.</summary>
    public async Task<ApiResponse<object>> DeleteExpenseAsync(Guid id, CancellationToken ct = default)
    {
        var expense = await _repo.GetExpenseByIdAsync(id, ct);
        if (expense == null) return ApiResponse<object>.NotFound("Expense not found.");
        expense.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(null, "Expense deleted.");
    }

    // ── Expense categories ───────────────────────────────────────────────────

    public async Task<ApiResponse<List<ExpenseCategoryViewModel>>> GetCategoriesAsync(CancellationToken ct = default)
        => ApiResponse<List<ExpenseCategoryViewModel>>.Ok(await _db.ExpenseCategories.AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new ExpenseCategoryViewModel { Id = c.Id, Name = c.Name, SortOrder = c.SortOrder })
            .ToListAsync(ct));

    public async Task<ApiResponse<ExpenseCategoryViewModel>> CreateCategoryAsync(SaveExpenseCategoryViewModel vm, CancellationToken ct = default)
    {
        var name = vm.Name.Trim();
        if (await _db.ExpenseCategories.AnyAsync(c => EF.Functions.ILike(c.Name, Like.Escape(name), Like.EscapeChar), ct))
            return ApiResponse<ExpenseCategoryViewModel>.Conflict($"Category '{name}' already exists.");
        var row = new ExpenseCategoryDefinition { Name = name, SortOrder = vm.SortOrder };
        _db.ExpenseCategories.Add(row);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<ExpenseCategoryViewModel>.Ok(new ExpenseCategoryViewModel { Id = row.Id, Name = row.Name, SortOrder = row.SortOrder });
    }

    public async Task<ApiResponse<ExpenseCategoryViewModel>> UpdateCategoryAsync(Guid id, SaveExpenseCategoryViewModel vm, CancellationToken ct = default)
    {
        var row = await _db.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (row == null) return ApiResponse<ExpenseCategoryViewModel>.NotFound("Category not found.");
        var name = vm.Name.Trim();
        if (await _db.ExpenseCategories.AnyAsync(c => c.Id != id && EF.Functions.ILike(c.Name, Like.Escape(name), Like.EscapeChar), ct))
            return ApiResponse<ExpenseCategoryViewModel>.Conflict($"Category '{name}' already exists.");

        // Keep historical expenses consistent with the renamed category.
        if (!string.Equals(row.Name, name, StringComparison.Ordinal))
            await _db.ShopExpenses.Where(e => e.Category == row.Name).ExecuteUpdateAsync(s => s.SetProperty(e => e.Category, name), ct);

        row.Name = name;
        row.SortOrder = vm.SortOrder;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<ExpenseCategoryViewModel>.Ok(new ExpenseCategoryViewModel { Id = row.Id, Name = row.Name, SortOrder = row.SortOrder });
    }

    public async Task<ApiResponse<object>> DeleteCategoryAsync(Guid id, CancellationToken ct = default)
    {
        var row = await _db.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (row == null) return ApiResponse<object>.NotFound("Category not found.");
        row.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<object>.Ok(null, "Category deleted. Existing expenses keep their category name.");
    }

    public async Task<string?> ValidateCategoryAsync(string category, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(category)) return "Category is required.";
        var names = await _db.ExpenseCategories.AsNoTracking().Select(c => c.Name).ToListAsync(ct);
        if (names.Count == 0) return null;   // branch has no category list configured → any text is accepted
        return names.Any(n => string.Equals(n, category.Trim(), StringComparison.OrdinalIgnoreCase))
            ? null
            : $"Unknown expense category '{category}'. Use one of: {string.Join(", ", names)} (or add it under expense categories).";
    }

    private async Task<string> CanonicalCategoryAsync(string category, CancellationToken ct)
    {
        var trimmed = category.Trim();
        var match = await _db.ExpenseCategories.AsNoTracking()
            .Where(c => EF.Functions.ILike(c.Name, Like.Escape(trimmed), Like.EscapeChar)).Select(c => c.Name).FirstOrDefaultAsync(ct);
        return match ?? trimmed;
    }

    // ── Salaries ─────────────────────────────────────────────────────────────

    public async Task<ApiResponse<PagedResult<StaffSalaryListViewModel>>> GetSalariesAsync(
        int? month, int? year, Guid? staffId, int page, int pageSize, CancellationToken ct = default)
        => ApiResponse<PagedResult<StaffSalaryListViewModel>>.Ok(await _repo.GetSalariesAsync(month, year, staffId, page, pageSize, ct));

    public async Task<ApiResponse<StaffSalaryListViewModel>> RecordSalaryAsync(RecordStaffSalaryViewModel vm, CancellationToken ct = default)
    {
        var staffName = await StaffNameAsync(vm.StaffUserId, ct);
        if (staffName == null) return ApiResponse<StaffSalaryListViewModel>.NotFound("Staff member not found.");
        if (await _db.StaffSalaries.AnyAsync(s => s.StaffUserId == vm.StaffUserId && s.Month == vm.Month && s.Year == vm.Year, ct))
            return ApiResponse<StaffSalaryListViewModel>.Conflict($"A salary for {vm.Month}/{vm.Year} is already recorded for this staff member. Add a payment to it instead.");

        var net = vm.BaseSalary + vm.Bonus - vm.Deduction;
        var paid = vm.AmountPaid ?? net;
        if (paid < 0 || paid > net) return ApiResponse<StaffSalaryListViewModel>.Fail($"Amount paid must be between 0 and the net salary ({net:N2}).");

        var salary = new StaffSalary
        {
            StaffUserId   = vm.StaffUserId,
            Month         = vm.Month,
            Year          = vm.Year,
            BaseSalary    = vm.BaseSalary,
            Bonus         = vm.Bonus,
            Deduction     = vm.Deduction,
            NetSalary     = net,
            AmountPaid    = paid,
            Status        = SalaryStatusFor(net, paid),
            PaymentMethod = vm.PaymentMethod,
            Notes         = vm.Notes,
            PaidOn        = _calendar.UtcNow,
            ActiveStatus  = ActiveStatus.Active
        };
        _db.StaffSalaries.Add(salary);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<StaffSalaryListViewModel>.Ok(ToListViewModel(salary, staffName), "Salary recorded.");
    }

    public async Task<ApiResponse<StaffSalaryListViewModel>> RecordSalaryPaymentAsync(Guid salaryId, RecordSalaryPaymentViewModel vm, CancellationToken ct = default)
    {
        var salary = await _db.StaffSalaries.Include(s => s.StaffUser).FirstOrDefaultAsync(s => s.Id == salaryId, ct);
        if (salary == null) return ApiResponse<StaffSalaryListViewModel>.NotFound("Salary record not found.");
        if (vm.Amount <= 0) return ApiResponse<StaffSalaryListViewModel>.Fail("Amount must be greater than zero.");
        var remaining = salary.NetSalary - salary.AmountPaid;
        if (vm.Amount > remaining) return ApiResponse<StaffSalaryListViewModel>.Fail($"Only {remaining:N2} remains unpaid on this salary.");

        salary.AmountPaid += vm.Amount;
        salary.Status = SalaryStatusFor(salary.NetSalary, salary.AmountPaid);
        salary.PaidOn = _calendar.UtcNow;
        salary.PaymentMethod = vm.PaymentMethod;
        if (vm.Notes != null) salary.Notes = string.IsNullOrWhiteSpace(salary.Notes) ? vm.Notes : $"{salary.Notes}; {vm.Notes}";
        await _db.SaveChangesAsync(ct);
        return ApiResponse<StaffSalaryListViewModel>.Ok(ToListViewModel(salary, salary.StaffUser?.Name ?? string.Empty), "Salary payment recorded.");
    }

    public async Task<ApiResponse<List<SalaryRuleViewModel>>> GetSalaryRulesAsync(CancellationToken ct = default)
    {
        var rules = await _db.SalaryRules.AsNoTracking().ToListAsync(ct);
        var ids = rules.Select(r => r.StaffUserId).ToList();
        var names = await _db.AppUsers.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        return ApiResponse<List<SalaryRuleViewModel>>.Ok(rules.Select(r => ToViewModel(r, names.GetValueOrDefault(r.StaffUserId) ?? string.Empty)).ToList());
    }

    public async Task<ApiResponse<SalaryRuleViewModel>> SaveSalaryRuleAsync(SaveSalaryRuleViewModel vm, CancellationToken ct = default)
    {
        var name = await StaffNameAsync(vm.StaffUserId, ct);
        if (name == null) return ApiResponse<SalaryRuleViewModel>.NotFound("Staff member not found.");

        var rule = await _db.SalaryRules.FirstOrDefaultAsync(r => r.StaffUserId == vm.StaffUserId, ct);
        if (rule == null)
        {
            rule = new SalaryRule { StaffUserId = vm.StaffUserId };
            _db.SalaryRules.Add(rule);
        }
        rule.MonthlySalary = vm.MonthlySalary;
        rule.WorkingDaysPerMonth = vm.WorkingDaysPerMonth;
        rule.PaidLeaveDays = vm.PaidLeaveDays;
        rule.HalfDayFactor = vm.HalfDayFactor;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<SalaryRuleViewModel>.Ok(ToViewModel(rule, name), "Salary rule saved.");
    }

    /// <summary>
    /// Salary from attendance for one month:
    /// per-day rate = monthly salary ÷ working days; unpaid days = absences + half days × (1 − half-day factor)
    /// + leave beyond the paid allowance; deduction = unpaid days × per-day rate (capped at the salary);
    /// advances not yet recovered are listed and, when recorded, deducted.
    /// </summary>
    public async Task<ApiResponse<SalaryComputationViewModel>> ComputeSalaryAsync(Guid staffId, int month, int year, CancellationToken ct = default)
    {
        var name = await StaffNameAsync(staffId, ct);
        if (name == null) return ApiResponse<SalaryComputationViewModel>.NotFound("Staff member not found.");
        var rule = await _db.SalaryRules.AsNoTracking().FirstOrDefaultAsync(r => r.StaffUserId == staffId, ct);
        if (rule == null) return ApiResponse<SalaryComputationViewModel>.Fail("Set a salary rule for this staff member first (monthly salary and working days).");

        var summary = await _repo.GetAttendanceSummaryAsync(staffId, month, year, ct);
        var advances = await _db.SalaryAdvances.AsNoTracking()
            .Where(a => a.StaffUserId == staffId && a.RecoveredInSalaryId == null)
            .SumAsync(a => (decimal?)a.Amount, ct) ?? 0;

        var result = SalaryCalculator.Compute(rule, summary.PresentDays, summary.HalfDays, summary.LeaveDays, summary.AbsentDays, advances);
        result.StaffUserId = staffId;
        result.StaffName = name;
        result.Month = month;
        result.Year = year;
        result.AlreadyRecorded = await _db.StaffSalaries.AnyAsync(s => s.StaffUserId == staffId && s.Month == month && s.Year == year, ct);
        return ApiResponse<SalaryComputationViewModel>.Ok(result);
    }

    public async Task<ApiResponse<StaffSalaryListViewModel>> RecordComputedSalaryAsync(RecordComputedSalaryViewModel vm, CancellationToken ct = default)
    {
        var computed = await ComputeSalaryAsync(vm.StaffUserId, vm.Month, vm.Year, ct);
        if (!computed.Success) return ApiResponse<StaffSalaryListViewModel>.Fail(computed.Errors, computed.StatusCode ?? 400);
        var c = computed.Data!;
        if (c.AlreadyRecorded)
            return ApiResponse<StaffSalaryListViewModel>.Conflict($"A salary for {vm.Month}/{vm.Year} is already recorded for this staff member.");

        // Recover only whole advances that fit within this month's room, and let the deduction be
        // exactly the sum of what gets marked recovered. Deducting a partial-advance amount while
        // leaving the advance itself fully outstanding would take money from pay that no record
        // accounts for, and charge the same advance again next month.
        var maxRecoverable = Math.Max(0, c.MonthlySalary - c.AttendanceDeduction + vm.Bonus);
        var outstandingAdvances = vm.RecoverAdvances
            ? await _db.SalaryAdvances.Where(a => a.StaffUserId == vm.StaffUserId && a.RecoveredInSalaryId == null)
                .OrderBy(a => a.AdvanceDate).ToListAsync(ct)
            : new List<SalaryAdvance>();

        var advancesToRecover = new List<SalaryAdvance>();
        var advanceDeduction = 0m;
        foreach (var advance in outstandingAdvances)
        {
            if (advanceDeduction + advance.Amount > maxRecoverable) break;
            advancesToRecover.Add(advance);
            advanceDeduction += advance.Amount;
        }

        var deduction = c.AttendanceDeduction + advanceDeduction;
        var net = c.MonthlySalary + vm.Bonus - deduction;
        var paid = vm.AmountPaid ?? net;
        if (paid < 0 || paid > net) return ApiResponse<StaffSalaryListViewModel>.Fail($"Amount paid must be between 0 and the net salary ({net:N2}).");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var salary = new StaffSalary
        {
            StaffUserId   = vm.StaffUserId,
            Month         = vm.Month,
            Year          = vm.Year,
            BaseSalary    = c.MonthlySalary,
            Bonus         = vm.Bonus,
            Deduction     = deduction,
            NetSalary     = net,
            AmountPaid    = paid,
            Status        = SalaryStatusFor(net, paid),
            PaymentMethod = vm.PaymentMethod,
            PaidOn        = _calendar.UtcNow,
            Notes         = $"From attendance: {c.PresentDays} present, {c.HalfDays} half, {c.LeaveDays} leave, {c.AbsentDays} absent" +
                            (advanceDeduction > 0 ? $"; advances recovered {advanceDeduction:N2}" : string.Empty) +
                            (string.IsNullOrWhiteSpace(vm.Notes) ? string.Empty : $"; {vm.Notes}"),
            ActiveStatus  = ActiveStatus.Active
        };
        _db.StaffSalaries.Add(salary);

        foreach (var advance in advancesToRecover)
            advance.RecoveredInSalaryId = salary.Id;

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ApiResponse<StaffSalaryListViewModel>.Ok(ToListViewModel(salary, c.StaffName), "Salary recorded from attendance.");
    }

    public async Task<ApiResponse<List<SalaryAdvanceViewModel>>> GetAdvancesAsync(Guid? staffId, bool outstandingOnly, CancellationToken ct = default)
    {
        var q = _db.SalaryAdvances.AsNoTracking();
        if (staffId.HasValue) q = q.Where(a => a.StaffUserId == staffId);
        if (outstandingOnly) q = q.Where(a => a.RecoveredInSalaryId == null);
        var rows = await q.OrderByDescending(a => a.AdvanceDate).Take(500).ToListAsync(ct);
        var ids = rows.Select(r => r.StaffUserId).Distinct().ToList();
        var names = await _db.AppUsers.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        return ApiResponse<List<SalaryAdvanceViewModel>>.Ok(rows.Select(a => new SalaryAdvanceViewModel
        {
            Id = a.Id, StaffUserId = a.StaffUserId, StaffName = names.GetValueOrDefault(a.StaffUserId) ?? string.Empty,
            Amount = a.Amount, AdvanceDate = a.AdvanceDate, PaymentMethod = a.PaymentMethod, Notes = a.Notes,
            Recovered = a.RecoveredInSalaryId.HasValue
        }).ToList());
    }

    public async Task<ApiResponse<SalaryAdvanceViewModel>> RecordAdvanceAsync(RecordSalaryAdvanceViewModel vm, CancellationToken ct = default)
    {
        var name = await StaffNameAsync(vm.StaffUserId, ct);
        if (name == null) return ApiResponse<SalaryAdvanceViewModel>.NotFound("Staff member not found.");
        if (vm.Amount <= 0) return ApiResponse<SalaryAdvanceViewModel>.Fail("Amount must be greater than zero.");

        var advance = new SalaryAdvance
        {
            StaffUserId   = vm.StaffUserId,
            Amount        = vm.Amount,
            AdvanceDate   = vm.AdvanceDate.HasValue ? await _calendar.ToCalendarDateAsync(vm.AdvanceDate.Value, ct) : await _calendar.TodayAsync(ct),
            PaymentMethod = vm.PaymentMethod,
            Notes         = vm.Notes
        };
        _db.SalaryAdvances.Add(advance);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<SalaryAdvanceViewModel>.Ok(new SalaryAdvanceViewModel
        {
            Id = advance.Id, StaffUserId = advance.StaffUserId, StaffName = name, Amount = advance.Amount,
            AdvanceDate = advance.AdvanceDate, PaymentMethod = advance.PaymentMethod, Notes = advance.Notes
        }, "Advance recorded.");
    }

    // ── Attendance ───────────────────────────────────────────────────────────

    public async Task<ApiResponse<PagedResult<AttendanceListViewModel>>> GetAttendanceAsync(
        Guid? staffId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default)
        => ApiResponse<PagedResult<AttendanceListViewModel>>.Ok(await _repo.GetAttendanceAsync(staffId, from, to, page, pageSize, ct));

    public async Task<ApiResponse<AttendanceListViewModel>> RecordAttendanceAsync(RecordAttendanceViewModel vm, CancellationToken ct = default)
    {
        var name = await StaffNameAsync(vm.StaffUserId, ct);
        if (name == null) return ApiResponse<AttendanceListViewModel>.NotFound("Staff member not found.");
        var date = await _calendar.ToCalendarDateAsync(vm.Date, ct);
        if (await _db.StaffAttendances.AnyAsync(a => a.StaffUserId == vm.StaffUserId && a.Date == date, ct))
            return ApiResponse<AttendanceListViewModel>.Conflict($"Attendance for {date:yyyy-MM-dd} is already recorded for this staff member. Update it instead.");

        var attendance = new StaffAttendance
        {
            StaffUserId  = vm.StaffUserId,
            Date         = date,
            Status       = vm.Status,
            CheckIn      = vm.CheckIn,
            CheckOut     = vm.CheckOut,
            Notes        = vm.Notes,
            ActiveStatus = ActiveStatus.Active
        };
        _db.StaffAttendances.Add(attendance);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<AttendanceListViewModel>.Ok(new AttendanceListViewModel
        {
            Id = attendance.Id, StaffUserId = attendance.StaffUserId, StaffName = name, Date = attendance.Date,
            Status = attendance.Status, CheckIn = attendance.CheckIn, CheckOut = attendance.CheckOut, Notes = attendance.Notes
        });
    }

    public async Task<ApiResponse<AttendanceSummaryViewModel>> GetAttendanceSummaryAsync(Guid staffId, int month, int year, CancellationToken ct = default)
        => ApiResponse<AttendanceSummaryViewModel>.Ok(await _repo.GetAttendanceSummaryAsync(staffId, month, year, ct));

    // ── Summary ──────────────────────────────────────────────────────────────

    public async Task<ApiResponse<FinanceSummaryViewModel>> GetSummaryAsync(DateTime from, DateTime to, CancellationToken ct = default)
        => ApiResponse<FinanceSummaryViewModel>.Ok(await _repo.GetFinanceSummaryAsync(from, to, ct));

    // ── Helpers ──────────────────────────────────────────────────────────────

    private Task<string?> StaffNameAsync(Guid staffId, CancellationToken ct)
        => _db.AppUsers.AsNoTracking().Where(u => u.Id == staffId).Select(u => (string?)u.Name).FirstOrDefaultAsync(ct);

    internal static SalaryStatus SalaryStatusFor(decimal net, decimal paid)
        => paid <= 0 ? SalaryStatus.Pending : paid >= net ? SalaryStatus.Paid : SalaryStatus.PartiallyPaid;

    private static ShopExpenseListViewModel ToListViewModel(ShopExpense e) => new()
    {
        Id = e.Id, Category = e.Category, Description = e.Description, Amount = e.Amount, ExpenseDate = e.ExpenseDate,
        PaymentMethod = e.PaymentMethod, ReceiptRef = e.ReceiptRef, AddedByName = string.Empty
    };

    private static StaffSalaryListViewModel ToListViewModel(StaffSalary s, string staffName) => new()
    {
        Id = s.Id, StaffUserId = s.StaffUserId, StaffName = staffName, Month = s.Month, Year = s.Year,
        BaseSalary = s.BaseSalary, Bonus = s.Bonus, Deduction = s.Deduction, NetSalary = s.NetSalary,
        AmountPaid = s.AmountPaid, Status = s.Status, PaymentMethod = s.PaymentMethod, PaidOn = s.PaidOn
    };

    private static SalaryRuleViewModel ToViewModel(SalaryRule r, string name) => new()
    {
        Id = r.Id, StaffUserId = r.StaffUserId, StaffName = name, MonthlySalary = r.MonthlySalary,
        WorkingDaysPerMonth = r.WorkingDaysPerMonth, PaidLeaveDays = r.PaidLeaveDays, HalfDayFactor = r.HalfDayFactor
    };
}

/// <summary>Pure salary arithmetic (unit-tested directly).</summary>
public static class SalaryCalculator
{
    public static SalaryComputationViewModel Compute(SalaryRule rule, int present, int halfDays, int leave, int absent, decimal outstandingAdvances)
    {
        var workingDays = Math.Max(1, rule.WorkingDaysPerMonth);
        var perDay = Math.Round(rule.MonthlySalary / workingDays, 2);
        var unpaidLeave = Math.Max(0, leave - rule.PaidLeaveDays);
        var unpaidDays = absent + halfDays * (1 - rule.HalfDayFactor) + unpaidLeave;
        var deduction = Math.Min(rule.MonthlySalary, Math.Round(unpaidDays * perDay, 2));

        return new SalaryComputationViewModel
        {
            MonthlySalary       = rule.MonthlySalary,
            WorkingDaysPerMonth = workingDays,
            PresentDays         = present,
            HalfDays            = halfDays,
            LeaveDays           = leave,
            AbsentDays          = absent,
            PaidLeaveDays       = rule.PaidLeaveDays,
            PerDayRate          = perDay,
            UnpaidDays          = unpaidDays,
            AttendanceDeduction = deduction,
            AdvancesToRecover   = outstandingAdvances,
            NetPayable          = Math.Max(0, rule.MonthlySalary - deduction - outstandingAdvances)
        };
    }
}
