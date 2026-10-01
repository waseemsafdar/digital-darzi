using Application.Common;
using Application.ViewModels.Common;
using Domain.Enums;

namespace Application.ViewModels.Finance;

// ── Expense ───────────────────────────────────────────────────────────────
public class CreateShopExpenseViewModel : IBaseCrudViewModel
{
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; } = DateTime.UtcNow.Date;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? ReceiptRef { get; set; }
}

public class UpdateShopExpenseViewModel : IBaseCrudViewModel, IIdentification
{
    public Guid Id { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public decimal? Amount { get; set; }
    public DateTime? ExpenseDate { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? ReceiptRef { get; set; }
}

public class ShopExpenseSearchModel : BaseSearchModel
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Category { get; set; }
}

public class ShopExpenseListViewModel
{
    public Guid Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? ReceiptRef { get; set; }
    public string AddedByName { get; set; } = string.Empty;
}

public class ShopExpenseDetailViewModel : IBaseCrudViewModel
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? ReceiptRef { get; set; }
    public Guid? AddedByUserId { get; set; }
    public string AddedByName { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
}

// ── Salary ─────────────────────────────────────────────────────────────────
public class RecordStaffSalaryViewModel : IBaseCrudViewModel
{
    public Guid StaffUserId { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal BaseSalary { get; set; }
    public decimal Bonus { get; set; } = 0;
    public decimal Deduction { get; set; } = 0;
    /// <summary>Amount handed over now. Null = the full net salary (the previous behaviour).</summary>
    public decimal? AmountPaid { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? Notes { get; set; }
}

public class StaffSalarySearchModel : BaseSearchModel
{
    public int? Month { get; set; }
    public int? Year { get; set; }
    public Guid? StaffId { get; set; }
}

public class StaffSalaryListViewModel
{
    public Guid Id { get; set; }
    public Guid StaffUserId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal BaseSalary { get; set; }
    public decimal Bonus { get; set; }
    public decimal Deduction { get; set; }
    public decimal NetSalary { get; set; }
    public decimal AmountPaid { get; set; }
    public SalaryStatus Status { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public DateTime PaidOn { get; set; }
}

public class UpdateStaffSalaryViewModel : IBaseCrudViewModel, IIdentification
{
    public Guid Id { get; set; }
    public decimal? BaseSalary { get; set; }
    public decimal? Bonus { get; set; }
    public decimal? Deduction { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? Notes { get; set; }
}

public class StaffSalaryDetailViewModel : IBaseCrudViewModel
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public Guid StaffUserId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal BaseSalary { get; set; }
    public decimal Bonus { get; set; }
    public decimal Deduction { get; set; }
    public decimal NetSalary { get; set; }
    public decimal AmountPaid { get; set; }
    public SalaryStatus Status { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? Notes { get; set; }
    public DateTime PaidOn { get; set; }
}

// ── Attendance ─────────────────────────────────────────────────────────────
public class RecordAttendanceViewModel : IBaseCrudViewModel
{
    public Guid StaffUserId { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow.Date;
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public TimeSpan? CheckIn { get; set; }
    public TimeSpan? CheckOut { get; set; }
    public string? Notes { get; set; }
}

public class AttendanceSearchModel : BaseSearchModel
{
    public Guid? StaffId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public class AttendanceListViewModel
{
    public Guid Id { get; set; }
    public Guid StaffUserId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public AttendanceStatus Status { get; set; }
    public TimeSpan? CheckIn { get; set; }
    public TimeSpan? CheckOut { get; set; }
    public string? Notes { get; set; }
}

public class UpdateAttendanceViewModel : IBaseCrudViewModel, IIdentification
{
    public Guid Id { get; set; }
    public AttendanceStatus? Status { get; set; }
    public TimeSpan? CheckIn { get; set; }
    public TimeSpan? CheckOut { get; set; }
    public string? Notes { get; set; }
}

public class AttendanceDetailViewModel : IBaseCrudViewModel
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public Guid StaffUserId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public AttendanceStatus Status { get; set; }
    public TimeSpan? CheckIn { get; set; }
    public TimeSpan? CheckOut { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedOn { get; set; }
}

public class AttendanceSummaryViewModel
{
    public Guid StaffUserId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public int Month { get; set; }
    public int Year { get; set; }
    public int PresentDays { get; set; }
    public int AbsentDays { get; set; }
    public int HalfDays { get; set; }
    public int LeaveDays { get; set; }
    public int TotalWorkingDays { get; set; }
}

// ── Finance Summary (Dashboard) ────────────────────────────────────────────
public class FinanceSummaryViewModel
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal TotalBalanceDue { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal TotalSalaries { get; set; }
    public decimal TotalKarigarPayments { get; set; }
    public decimal TotalRefunds { get; set; }
    /// <summary>Cash basis: collected − expenses − salaries − karigar payouts. Not an accrual profit figure.</summary>
    public decimal NetProfit { get; set; }
    public int TotalOrders { get; set; }
    public int PendingOrders { get; set; }
    public List<ExpenseByCategoryViewModel> ExpenseBreakdown { get; set; } = new();
}

public class ExpenseByCategoryViewModel
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

// ── Staff Self-Service Attendance ──────────────────────────────────────────
/// <summary>Used by staff (any role) to self-record their own check-in/check-out.</summary>
public class StaffCheckInViewModel
{
    /// <summary>Leave null to check in; provide a time to update check-out.</summary>
    public TimeSpan? CheckOut { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Filter for a staff member viewing their own attendance history.</summary>
public class MyAttendanceFilter
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    /// <summary>Defaults to current month if neither From nor To supplied.</summary>
    public int? Month { get; set; }
    public int? Year { get; set; }
}

// ── Expense categories ─────────────────────────────────────────────────────
public class ExpenseCategoryViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class SaveExpenseCategoryViewModel
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

// ── Salary from attendance ─────────────────────────────────────────────────
public class SalaryRuleViewModel
{
    public Guid Id { get; set; }
    public Guid StaffUserId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public decimal MonthlySalary { get; set; }
    public int WorkingDaysPerMonth { get; set; }
    public int PaidLeaveDays { get; set; }
    public decimal HalfDayFactor { get; set; }
}

public class SaveSalaryRuleViewModel
{
    public Guid StaffUserId { get; set; }
    public decimal MonthlySalary { get; set; }
    public int WorkingDaysPerMonth { get; set; } = 26;
    public int PaidLeaveDays { get; set; }
    public decimal HalfDayFactor { get; set; } = 0.5m;
}

public class SalaryComputationViewModel
{
    public Guid StaffUserId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal MonthlySalary { get; set; }
    public int WorkingDaysPerMonth { get; set; }
    public int PresentDays { get; set; }
    public int HalfDays { get; set; }
    public int LeaveDays { get; set; }
    public int AbsentDays { get; set; }
    public int PaidLeaveDays { get; set; }
    public decimal PerDayRate { get; set; }
    public decimal UnpaidDays { get; set; }
    public decimal AttendanceDeduction { get; set; }
    public decimal AdvancesToRecover { get; set; }
    public decimal NetPayable { get; set; }
    public bool AlreadyRecorded { get; set; }
}

public class RecordComputedSalaryViewModel
{
    public Guid StaffUserId { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal Bonus { get; set; }
    /// <summary>Recover outstanding advances from this salary (default true).</summary>
    public bool RecoverAdvances { get; set; } = true;
    /// <summary>Amount handed over now. Null = the full net payable.</summary>
    public decimal? AmountPaid { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? Notes { get; set; }
}

public class RecordSalaryPaymentViewModel
{
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? Notes { get; set; }
}

public class SalaryAdvanceViewModel
{
    public Guid Id { get; set; }
    public Guid StaffUserId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime AdvanceDate { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public string? Notes { get; set; }
    public bool Recovered { get; set; }
}

public class RecordSalaryAdvanceViewModel
{
    public Guid StaffUserId { get; set; }
    public decimal Amount { get; set; }
    public DateTime? AdvanceDate { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? Notes { get; set; }
}
