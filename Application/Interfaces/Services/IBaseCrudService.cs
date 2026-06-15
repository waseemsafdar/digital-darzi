using Application.Common;
using Application.Common.Models;
using Application.ViewModels.Common;

namespace Application.Interfaces.Services;

/// <summary>
/// Generic base CRUD service interface — domain-specific services extend this.
/// Matches MobilePosApi base pattern.
/// </summary>
public interface IBaseCrudService<TCreate, TUpdate, TDetail>
    where TCreate : class, IBaseCrudViewModel, new()
    where TUpdate : class, IBaseCrudViewModel, IIdentification, new()
    where TDetail : class, IBaseCrudViewModel, new()
{
    Task<ApiResponse<TDetail>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<PaginatedResultModel<TDetail>>> GetAllAsync(IBaseSearchModel search, CancellationToken ct = default);
    Task<ApiResponse<Guid>> CreateAsync(TCreate model, CancellationToken ct = default);
    Task<ApiResponse<Guid>> UpdateAsync(TUpdate model, CancellationToken ct = default);
    Task<ApiResponse<string>> DeleteAsync(Guid id, CancellationToken ct = default);
}
