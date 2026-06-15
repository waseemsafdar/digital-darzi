using Application.Common;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Services;
using Application.ViewModels.Finance;
using AutoMapper;
using Domain.Entities;

namespace Infrastructure.Services;

public class ShopExpenseService
    : BaseCrudService<ShopExpense, CreateShopExpenseViewModel, UpdateShopExpenseViewModel, ShopExpenseDetailViewModel>,
      IShopExpenseService
{
    private readonly IShopExpenseRepository _expenseRepo;
    private readonly ICurrentUserService _currentUser;

    public ShopExpenseService(IShopExpenseRepository repo, ICurrentUserService currentUser, IMapper mapper)
        : base(repo, mapper)
    {
        _expenseRepo = repo;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<PagedResult<ShopExpenseDetailViewModel>>> GetFilteredAsync(
        DateTime? from, DateTime? to, string? category,
        int page, int pageSize, CancellationToken ct = default)
    {
        var result = await _expenseRepo.GetPagedDetailAsync(from, to, category, page, pageSize, ct);
        return new ApiResponse<PagedResult<ShopExpenseDetailViewModel>>(result);
    }

    public override async Task<ApiResponse<PagedResult<ShopExpenseDetailViewModel>>> GetAllAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var result = await _expenseRepo.GetPagedDetailAsync(null, null, null, page, pageSize, ct);
        return new ApiResponse<PagedResult<ShopExpenseDetailViewModel>>(result);
    }

    public override async Task<ApiResponse<ShopExpenseDetailViewModel>> CreateAsync(
        CreateShopExpenseViewModel vm, CancellationToken ct = default)
    {
        var entity = _mapper.Map<ShopExpense>(vm);
        entity.ShopId        = _currentUser.ShopId;
        entity.AddedByUserId = _currentUser.UserId;
        entity.CreatedBy     = _currentUser.UserId;
        entity.CreatedOn     = DateTime.UtcNow;
        await _repo.AddAsync(entity, ct);
        return new ApiResponse<ShopExpenseDetailViewModel>(_mapper.Map<ShopExpenseDetailViewModel>(entity), "Created successfully.");
    }

    public override async Task<ApiResponse<ShopExpenseDetailViewModel>> UpdateAsync(
        UpdateShopExpenseViewModel vm, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(vm.Id, ct);
        if (entity == null) return new ApiResponse<ShopExpenseDetailViewModel>("Expense not found.", 404);
        _mapper.Map(vm, entity);
        entity.UpdatedBy = _currentUser.UserId;
        entity.UpdatedOn = DateTime.UtcNow;
        await _repo.UpdateAsync(entity, ct);
        return new ApiResponse<ShopExpenseDetailViewModel>(_mapper.Map<ShopExpenseDetailViewModel>(entity), "Updated successfully.");
    }
}
