using Application.Common;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Domain.Entities;

namespace Application.Services;

/// <summary>
/// Generic base CRUD service — domain-specific services inherit from this.
/// Mirrors MobilePosApi.BaseCrudService pattern.
/// </summary>
public class BaseCrudService<TEntity, TCreate, TUpdate, TDetail>
    : IBaseCrudService<TCreate, TUpdate, TDetail>
    where TEntity : BaseDBModel
    where TCreate : class
    where TUpdate : class
    where TDetail : class
{
    protected readonly IBaseRepository<TEntity> _repo;

    public BaseCrudService(IBaseRepository<TEntity> repo)
    {
        _repo = repo;
    }

    public virtual async Task<ApiResponse<TDetail>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity == null)
            return ApiResponse<TDetail>.Fail($"Record not found.");
        return ApiResponse<TDetail>.Ok(MapToDetail(entity));
    }

    public virtual async Task<ApiResponse<PagedResult<TDetail>>> GetAllAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var paged = await _repo.GetPagedAsync(page, pageSize, ct);
        var mappedItems = paged.Items.Select(MapToDetail).ToList();
        var result = PagedResult<TDetail>.From(mappedItems, paged.TotalCount, paged.Page, paged.PageSize);
        return ApiResponse<PagedResult<TDetail>>.Ok(result);
    }

    public virtual async Task<ApiResponse<TDetail>> CreateAsync(TCreate vm, CancellationToken ct = default)
    {
        var entity = MapFromCreate(vm);
        await _repo.AddAsync(entity, ct);
        return ApiResponse<TDetail>.Ok(MapToDetail(entity), "Created successfully.");
    }

    public virtual async Task<ApiResponse<TDetail>> UpdateAsync(Guid id, TUpdate vm, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity == null)
            return ApiResponse<TDetail>.Fail("Record not found.");
        ApplyUpdate(entity, vm);
        await _repo.UpdateAsync(entity, ct);
        return ApiResponse<TDetail>.Ok(MapToDetail(entity), "Updated successfully.");
    }

    public virtual async Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity == null)
            return ApiResponse<object>.Fail("Record not found.");
        await _repo.DeleteAsync(id, ct);
        return ApiResponse<object>.Ok(null, "Deleted successfully.");
    }

    // Override these in derived services to do the actual mapping
    protected virtual TDetail MapToDetail(TEntity entity)
        => throw new NotImplementedException($"{GetType().Name} must override MapToDetail().");

    protected virtual TEntity MapFromCreate(TCreate vm)
        => throw new NotImplementedException($"{GetType().Name} must override MapFromCreate().");

    protected virtual void ApplyUpdate(TEntity entity, TUpdate vm)
        => throw new NotImplementedException($"{GetType().Name} must override ApplyUpdate().");
}
