using Application.Common;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.ViewModels.Finance;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Services;

public class FinanceService : IFinanceService
{
    private readonly IFinanceRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public FinanceService(IFinanceRepository repo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<PagedResult<ShopExpenseListViewModel>>> GetExpensesAsync(
        DateTime? from, DateTime? to, string? category, int page, int pageSize, CancellationToken ct = default)
        => ApiResponse<PagedResult<ShopExpenseListViewModel>>.Ok(await _repo.GetExpensesAsync(from, to, category, page, pageSize, ct));

    public async Task<ApiResponse<ShopExpenseListViewModel>> CreateExpenseAsync(CreateShopExpenseViewModel vm, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var expense = new ShopExpense
        {
            Id            = Guid.NewGuid(),
            TenantId      = _currentUser.TenantId,
            BranchId      = _currentUser.ShopId,
            ShopId        = _currentUser.ShopId,
            Category      = vm.Category,
            Description   = vm.Description,
            Amount        = vm.Amount,
            ExpenseDate   = vm.ExpenseDate,
            PaymentMethod = vm.PaymentMethod,
            ReceiptRef    = vm.ReceiptRef,
            AddedByUserId = _currentUser.UserId,
            ActiveStatus  = ActiveStatus.Active,
            CreatedBy     = _currentUser.UserId,
            CreatedOn     = now,
            UpdatedBy     = _currentUser.UserId,
            UpdatedOn     = now
        };

        await _repo.CreateExpenseAsync(expense, ct);
        return ApiResponse<ShopExpenseListViewModel>.Ok(new ShopExpenseListViewModel
        {
            Id = expense.Id, Category = expense.Category, Description = expense.Description,
            Amount = expense.Amount, ExpenseDate = expense.ExpenseDate, PaymentMethod = expense.PaymentMethod,
            ReceiptRef = expense.ReceiptRef, AddedByName = string.Empty
        });
    }

    public async Task<ApiResponse<ShopExpenseListViewModel>> UpdateExpenseAsync(Guid id, UpdateShopExpenseViewModel vm, CancellationToken ct = default)
    {
        var expense = await _repo.GetExpenseByIdAsync(id, ct);
        if (expense == null) return ApiResponse<ShopExpenseListViewModel>.Fail("Expense not found.");

        if (vm.Category != null) expense.Category = vm.Category;
        if (vm.Description != null) expense.Description = vm.Description;
        if (vm.Amount.HasValue) expense.Amount = vm.Amount.Value;
        if (vm.ExpenseDate.HasValue) expense.ExpenseDate = vm.ExpenseDate.Value;
        if (vm.PaymentMethod.HasValue) expense.PaymentMethod = vm.PaymentMethod.Value;
        if (vm.ReceiptRef != null) expense.ReceiptRef = vm.ReceiptRef;
        expense.UpdatedBy = _currentUser.UserId;
        expense.UpdatedOn = DateTime.UtcNow;

        await _repo.UpdateExpenseAsync(expense, ct);
        return ApiResponse<ShopExpenseListViewModel>.Ok(new ShopExpenseListViewModel
        {
            Id = expense.Id, Category = expense.Category, Description = expense.Description,
            Amount = expense.Amount, ExpenseDate = expense.ExpenseDate, PaymentMethod = expense.PaymentMethod,
            ReceiptRef = expense.ReceiptRef, AddedByName = string.Empty
        });
    }

    public async Task<ApiResponse<object>> DeleteExpenseAsync(Guid id, CancellationToken ct = default)
    {
        var expense = await _repo.GetExpenseByIdAsync(id, ct);
        if (expense == null) return ApiResponse<object>.Fail("Expense not found.");
        await _repo.DeleteExpenseAsync(id, ct);
        return ApiResponse<object>.Ok((object?)null, "Expense deleted.");
    }

    public async Task<ApiResponse<PagedResult<StaffSalaryListViewModel>>> GetSalariesAsync(
        int? month, int? year, Guid? staffId, int page, int pageSize, CancellationToken ct = default)
        => ApiResponse<PagedResult<StaffSalaryListViewModel>>.Ok(await _repo.GetSalariesAsync(month, year, staffId, page, pageSize, ct));

    public async Task<ApiResponse<StaffSalaryListViewModel>> RecordSalaryAsync(RecordStaffSalaryViewModel vm, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var salary = new StaffSalary
        {
            Id            = Guid.NewGuid(),
            TenantId      = _currentUser.TenantId,
            BranchId      = _currentUser.ShopId,
            ShopId        = _currentUser.ShopId,
            StaffUserId   = vm.StaffUserId,
            Month         = vm.Month,
            Year          = vm.Year,
            BaseSalary    = vm.BaseSalary,
            Bonus         = vm.Bonus,
            Deduction     = vm.Deduction,
            NetSalary     = vm.BaseSalary + vm.Bonus - vm.Deduction,
            PaymentMethod = vm.PaymentMethod,
            Notes         = vm.Notes,
            PaidOn        = now,
            ActiveStatus  = ActiveStatus.Active,
            CreatedBy     = _currentUser.UserId,
            CreatedOn     = now,
            UpdatedBy     = _currentUser.UserId,
            UpdatedOn     = now
        };

        await _repo.CreateSalaryAsync(salary, ct);
        return ApiResponse<StaffSalaryListViewModel>.Ok(new StaffSalaryListViewModel
        {
            Id = salary.Id, StaffUserId = salary.StaffUserId, Month = salary.Month, Year = salary.Year,
            BaseSalary = salary.BaseSalary, Bonus = salary.Bonus, Deduction = salary.Deduction,
            NetSalary = salary.NetSalary, PaymentMethod = salary.PaymentMethod, PaidOn = salary.PaidOn
        });
    }

    public async Task<ApiResponse<PagedResult<AttendanceListViewModel>>> GetAttendanceAsync(
        Guid? staffId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default)
        => ApiResponse<PagedResult<AttendanceListViewModel>>.Ok(await _repo.GetAttendanceAsync(staffId, from, to, page, pageSize, ct));

    public async Task<ApiResponse<AttendanceListViewModel>> RecordAttendanceAsync(RecordAttendanceViewModel vm, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var attendance = new StaffAttendance
        {
            Id          = Guid.NewGuid(),
            TenantId    = _currentUser.TenantId,
            BranchId    = _currentUser.ShopId,
            ShopId      = _currentUser.ShopId,
            StaffUserId = vm.StaffUserId,
            Date        = vm.Date,
            Status      = vm.Status,
            CheckIn     = vm.CheckIn,
            CheckOut    = vm.CheckOut,
            Notes       = vm.Notes,
            ActiveStatus= ActiveStatus.Active,
            CreatedBy   = _currentUser.UserId,
            CreatedOn   = now,
            UpdatedBy   = _currentUser.UserId,
            UpdatedOn   = now
        };

        await _repo.RecordAttendanceAsync(attendance, ct);
        return ApiResponse<AttendanceListViewModel>.Ok(new AttendanceListViewModel
        {
            Id = attendance.Id, StaffUserId = attendance.StaffUserId, Date = attendance.Date,
            Status = attendance.Status, CheckIn = attendance.CheckIn, CheckOut = attendance.CheckOut,
            Notes = attendance.Notes
        });
    }

    public async Task<ApiResponse<AttendanceSummaryViewModel>> GetAttendanceSummaryAsync(Guid staffId, int month, int year, CancellationToken ct = default)
        => ApiResponse<AttendanceSummaryViewModel>.Ok(await _repo.GetAttendanceSummaryAsync(staffId, month, year, ct));

    public async Task<ApiResponse<FinanceSummaryViewModel>> GetSummaryAsync(DateTime from, DateTime to, CancellationToken ct = default)
        => ApiResponse<FinanceSummaryViewModel>.Ok(await _repo.GetFinanceSummaryAsync(from, to, ct));
}
