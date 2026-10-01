using System.Linq.Expressions;
using Application.Interfaces.Repositories;
using Application.ViewModels.Common;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// Generic base repository with soft-delete support and typed filters.
/// Every read goes through the DbSet, so the tenant/branch global query filters always apply.
/// Tenant, branch and audit columns are stamped by <see cref="ApplicationDbContext"/> on save.
/// </summary>
public class BaseRepository<TEntity> : IBaseRepository<TEntity> where TEntity : BaseDBModel
{
    protected readonly ApplicationDbContext _db;
    protected readonly DbSet<TEntity> _dbSet;

    public BaseRepository(ApplicationDbContext db)
    {
        _db = db;
        _dbSet = db.Set<TEntity>();
    }

    protected virtual IQueryable<TEntity> GetBaseQuery() => _dbSet.AsNoTracking().Where(x => !x.IsDeleted);

    /// <summary>Tracked query for single-entity fetches so the returned entity can be mutated and saved.</summary>
    protected virtual IQueryable<TEntity> GetTrackedQuery() => _dbSet.Where(x => !x.IsDeleted);

    protected virtual Task<IQueryable<TEntity>> ApplyFiltersAsync(IQueryable<TEntity> query, IBaseSearchModel search)
        => Task.FromResult(query);

    public virtual async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await GetTrackedQuery().FirstOrDefaultAsync(x => x.Id == id, ct);

    public virtual async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken ct = default)
        => await GetBaseQuery().ToListAsync(ct);

    public virtual async Task<(IEnumerable<TEntity> Items, int TotalCount)> GetPagedAsync(IBaseSearchModel search, CancellationToken ct = default)
    {
        var query = await ApplyFiltersAsync(GetBaseQuery(), search);
        var total = await query.CountAsync(ct);

        if (!search.DisablePagination)
        {
            // A stable order is required for paging; filters may already have ordered the query.
            if (query.Expression.Type != typeof(IOrderedQueryable<TEntity>))
                query = query.OrderByDescending(x => x.CreatedOn).ThenBy(x => x.Id);
            query = query.Skip((search.PageNumber - 1) * search.PageSize).Take(search.PageSize);
        }

        return (await query.ToListAsync(ct), total);
    }

    public virtual async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
        => await _dbSet.Where(predicate).ToListAsync(ct);

    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default)
    {
        await _dbSet.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public virtual async Task UpdateAsync(TEntity entity, CancellationToken ct = default)
    {
        if (_db.Entry(entity).State == EntityState.Detached)
            _dbSet.Update(entity);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Soft delete through the filtered query — a row outside the caller's tenant/branch is never touched.</summary>
    public virtual async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetTrackedQuery().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity == null) return;
        entity.IsDeleted = true;
        await _db.SaveChangesAsync(ct);
    }

    public virtual async Task SaveChangesAsync(CancellationToken ct = default) => await _db.SaveChangesAsync(ct);

    public void Detach(TEntity entity) => _db.Entry(entity).State = EntityState.Detached;
}
