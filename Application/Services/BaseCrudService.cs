using Application.Common;
using Application.Common.Models;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Application.Mappings;
using Application.ViewModels.Common;
using Domain.Entities;

namespace Application.Services;

/// <summary>
/// Generic base CRUD service over a repository and the explicit <see cref="IObjectMapper"/>.
///
/// Unexpected exceptions are no longer caught here: they propagate to the global exception
/// middleware, which logs them and returns a safe message (previously the raw exception text,
/// including database detail, was copied into the response).
///
/// Tenant, branch and audit columns are stamped by the DbContext on save.
/// </summary>
public class BaseCrudService<TEntity, TCreate, TUpdate, TDetail>
    : IBaseCrudService<TCreate, TUpdate, TDetail>
    where TEntity : BaseDBModel
    where TCreate : class, IBaseCrudViewModel, new()
    where TUpdate : class, IBaseCrudViewModel, IIdentification, new()
    where TDetail : class, IBaseCrudViewModel, new()
{
    protected readonly IBaseRepository<TEntity> _repo;
    protected readonly IObjectMapper _mapper;

    /// <summary>Human-readable entity name used in "not found" messages.</summary>
    protected virtual string EntityName => "Record";

    public BaseCrudService(IBaseRepository<TEntity> repo, IObjectMapper mapper)
    {
        _repo = repo;
        _mapper = mapper;
    }

    public virtual async Task<ApiResponse<TDetail>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        return entity == null
            ? ApiResponse<TDetail>.NotFound($"{EntityName} not found.")
            : ApiResponse<TDetail>.Ok(_mapper.Map<TDetail>(entity));
    }

    public virtual async Task<ApiResponse<PaginatedResultModel<TDetail>>> GetAllAsync(IBaseSearchModel search, CancellationToken ct = default)
    {
        var (page, size) = Paging.Normalize(search.PageNumber, search.PageSize, 10);
        search.PageNumber = page;
        if (!search.DisablePagination) search.PageSize = size;

        var paged = await _repo.GetPagedAsync(search, ct);
        var items = paged.Items.Select(e => _mapper.Map<TDetail>(e)).ToList();
        var pageSize = search.DisablePagination ? paged.TotalCount : search.PageSize;
        return ApiResponse<PaginatedResultModel<TDetail>>.Ok(
            new PaginatedResultModel<TDetail>(items, search.PageNumber, pageSize, paged.TotalCount));
    }

    public virtual async Task<ApiResponse<Guid>> CreateAsync(TCreate vm, CancellationToken ct = default)
    {
        var entity = _mapper.Map<TEntity>(vm);
        await BeforeCreateAsync(entity, vm, ct);
        var created = await _repo.AddAsync(entity, ct);
        return ApiResponse<Guid>.Ok(created.Id);
    }

    public virtual async Task<ApiResponse<Guid>> UpdateAsync(TUpdate vm, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(vm.Id, ct);
        if (entity == null) return ApiResponse<Guid>.NotFound($"{EntityName} not found.");
        _mapper.Map(vm, entity);
        await BeforeUpdateAsync(entity, vm, ct);
        await _repo.UpdateAsync(entity, ct);
        return ApiResponse<Guid>.Ok(vm.Id);
    }

    public virtual async Task<ApiResponse<string>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity == null) return ApiResponse<string>.NotFound($"{EntityName} not found.");
        await _repo.DeleteAsync(id, ct);
        return ApiResponse<string>.Ok(id.ToString(), "Deleted successfully.");
    }

    /// <summary>Hook for derived services to set derived fields or run checks before insert.</summary>
    protected virtual Task BeforeCreateAsync(TEntity entity, TCreate vm, CancellationToken ct) => Task.CompletedTask;

    /// <summary>Hook for derived services to recompute derived fields before update.</summary>
    protected virtual Task BeforeUpdateAsync(TEntity entity, TUpdate vm, CancellationToken ct) => Task.CompletedTask;
}
