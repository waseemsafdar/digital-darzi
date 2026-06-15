using Application.Common;

namespace Application.Interfaces.Services;

/// <summary>
/// Generic base CRUD service interface — domain-specific services extend this.
/// TUpdate must implement IHasId so Id travels in the body, not the route.
/// </summary>
public interface IBaseCrudService<TCreate, TUpdate, TDetail>
    where TCreate : class
    where TUpdate : class, IHasId
    where TDetail : class
{
    Task<ApiResponse<TDetail>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<PagedResult<TDetail>>> GetAllAsync(int page, int pageSize, CancellationToken ct = default);
    Task<ApiResponse<TDetail>> CreateAsync(TCreate vm, CancellationToken ct = default);
    Task<ApiResponse<TDetail>> UpdateAsync(TUpdate vm, CancellationToken ct = default);
    Task<ApiResponse<object>> DeleteAsync(Guid id, CancellationToken ct = default);
}
