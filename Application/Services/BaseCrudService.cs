using Application.Common;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using AutoMapper;
using Domain.Entities;

namespace Application.Services;

/// <summary>
/// Generic base CRUD service — uses AutoMapper for entity↔ViewModel conversions.
/// TUpdate implements IHasId so Id comes from the body, not the route.
/// Mirrors MobilePosApi.BaseCrudService pattern.
/// </summary>
public class BaseCrudService<TEntity, TCreate, TUpdate, TDetail>
    : IBaseCrudService<TCreate, TUpdate, TDetail>
    where TEntity : BaseDBModel
    where TCreate : class
    where TUpdate : class, IHasId
    where TDetail : class
{
    protected readonly IBaseRepository<TEntity> _repo;
    protected readonly IMapper _mapper;

    public BaseCrudService(IBaseRepository<TEntity> repo, IMapper mapper)
    {
        _repo   = repo;
        _mapper = mapper;
    }

    public virtual async Task<ApiResponse<TDetail>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity == null)
            return new ApiResponse<TDetail>("Record not found.", 404);
        return new ApiResponse<TDetail>(_mapper.Map<TDetail>(entity));
    }

    public virtual async Task<ApiResponse<PagedResult<TDetail>>> GetAllAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var paged  = await _repo.GetPagedAsync(page, pageSize, ct);
        var items  = _mapper.Map<List<TDetail>>(paged.Items);
        var result = PagedResult<TDetail>.From(items, paged.TotalCount, paged.Page, paged.PageSize);
        return new ApiResponse<PagedResult<TDetail>>(result);
    }

    public virtual async Task<ApiResponse<TDetail>> CreateAsync(TCreate vm, CancellationToken ct = default)
    {
        var entity = _mapper.Map<TEntity>(vm);
        await _repo.AddAsync(entity, ct);
        return new ApiResponse<TDetail>(_mapper.Map<TDetail>(entity), "Created successfully.");
    }

    public virtual async Task<ApiResponse<TDetail>> UpdateAsync(TUpdate vm, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(vm.Id, ct);
        if (entity == null)
            return new ApiResponse<TDetail>("Record not found.", 404);
        _mapper.Map(vm, entity);
        await _repo.UpdateAsync(entity, ct);
        return new ApiResponse<TDetail>(_mapper.Map<TDetail>(entity), "Updated successfully.");
    }

    public virtual async Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(id, ct);
        if (entity == null)
            return new ApiResponse<object>("Record not found.", 404);
        await _repo.DeleteAsync(id, ct);
        return new ApiResponse<object>(null, "Deleted successfully.");
    }
}
