using System.Linq.Expressions;
using Application.Common;
using Application.ViewModels.Common;
using Domain.Entities;

namespace Application.Interfaces.Repositories;

/// <summary>
/// Generic base repository interface — all entity repositories should extend this.
/// Matches MobilePosApi base pattern.
/// </summary>
public interface IBaseRepository<TEntity> where TEntity : BaseDBModel
{
    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken ct = default);
    Task<(IEnumerable<TEntity> Items, int TotalCount)> GetPagedAsync(IBaseSearchModel search, CancellationToken ct = default);
    Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default);
    Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default);
    Task UpdateAsync(TEntity entity, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
