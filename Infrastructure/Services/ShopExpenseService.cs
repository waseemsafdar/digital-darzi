using Application.Common;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Mappings;
using Application.Services;
using Application.ViewModels.Common;
using Application.ViewModels.Finance;
using Domain.Entities;

namespace Infrastructure.Services;

/// <summary>
/// Generic CRUD for <c>/api/expenses</c>. Tenant and branch are stamped by the DbContext
/// (previously this service set only ShopId, so created expenses were invisible afterwards).
/// </summary>
public class ShopExpenseService<TCreate, TUpdate, TDetail>
    : BaseCrudService<ShopExpense, TCreate, TUpdate, TDetail>,
      IShopExpenseService<TCreate, TUpdate, TDetail>
    where TCreate : class, IBaseCrudViewModel, new()
    where TUpdate : class, IBaseCrudViewModel, IIdentification, new()
    where TDetail : class, IBaseCrudViewModel, new()
{
    private readonly ICurrentUserService _currentUser;
    private readonly IBusinessCalendar _calendar;
    private readonly IFinanceService _finance;

    public ShopExpenseService(IShopExpenseRepository repo, ICurrentUserService currentUser, IObjectMapper mapper,
        IBusinessCalendar calendar, IFinanceService finance)
        : base(repo, mapper)
    {
        _currentUser = currentUser;
        _calendar = calendar;
        _finance = finance;
    }

    protected override string EntityName => "Expense";

    public override async Task<ApiResponse<Guid>> CreateAsync(TCreate vm, CancellationToken ct = default)
    {
        if (vm is CreateShopExpenseViewModel c)
        {
            var error = await _finance.ValidateCategoryAsync(c.Category, ct);
            if (error != null) return ApiResponse<Guid>.Fail(error);
        }
        return await base.CreateAsync(vm, ct);
    }

    public override async Task<ApiResponse<Guid>> UpdateAsync(TUpdate vm, CancellationToken ct = default)
    {
        if (vm is UpdateShopExpenseViewModel u && u.Category != null)
        {
            var error = await _finance.ValidateCategoryAsync(u.Category, ct);
            if (error != null) return ApiResponse<Guid>.Fail(error);
        }
        return await base.UpdateAsync(vm, ct);
    }

    protected override async Task BeforeCreateAsync(ShopExpense entity, TCreate vm, CancellationToken ct)
    {
        entity.AddedByUserId = _currentUser.UserId;
        entity.ExpenseDate = await _calendar.ToCalendarDateAsync(entity.ExpenseDate, ct);
    }

    protected override async Task BeforeUpdateAsync(ShopExpense entity, TUpdate vm, CancellationToken ct)
    {
        if (vm is UpdateShopExpenseViewModel { ExpenseDate: not null })
            entity.ExpenseDate = await _calendar.ToCalendarDateAsync(entity.ExpenseDate, ct);
    }
}
