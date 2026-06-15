using Application.Common;
using Domain.Enums;

namespace Application.ViewModels.Finance;

// ── Expense ───────────────────────────────────────────────────────────────
public class CreateShopExpenseViewModel
{
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; } = DateTime.Today;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? ReceiptRef { get; set; }
}

public class UpdateShopExpenseViewModel : IHasId
{
    public Guid Id { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public decimal? Amount { get; set; }
    public DateTime? ExpenseDate { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? ReceiptRef { get; set; }
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

public class ShopExpenseDetailViewModel
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
public class RecordStaffSalaryViewModel
{
    public Guid StaffUserId { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
    public decimal BaseSalary { get; set; }
    public decimal Bonus { get; set; } = 0;
    public decimal Deduction { get; set; } = 0;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? Notes { get; set; }
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
    public PaymentMethod PaymentMethod { get; set; }
    public DateTime PaidOn { get; set; }
}

public class UpdateStaffSalaryViewModel : IHasId
{
    public Guid Id { get; set; }
    public decimal? BaseSalary { get; set; }
    public decimal? Bonus { get; set; }
    public decimal? Deduction { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? Notes { get; set; }
}

public class StaffSalaryDetailViewModel
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
    public PaymentMethod PaymentMethod { get; set; }
    public string? Notes { get; set; }
    public DateTime PaidOn { get; set; }
}

// ── Attendance ─────────────────────────────────────────────────────────────
public class RecordAttendanceViewModel
{
    public Guid StaffUserId { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public TimeSpan? CheckIn { get; set; }
    public TimeSpan? CheckOut { get; set; }
    public string? Notes { get; set; }
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

public class UpdateAttendanceViewModel : IHasId
{
    public Guid Id { get; set; }
    public AttendanceStatus? Status { get; set; }
    public TimeSpan? CheckIn { get; set; }
    public TimeSpan? CheckOut { get; set; }
    public string? Notes { get; set; }
}

public class AttendanceDetailViewModel
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
