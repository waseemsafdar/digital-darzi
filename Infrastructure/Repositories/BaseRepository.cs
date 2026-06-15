using System.Linq.Expressions;
using Application.Common;
using Application.Interfaces.Repositories;
using Application.ViewModels.Common;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// Generic base repository — all concrete repositories should inherit from this.
/// Provides standard EF Core CRUD with soft-delete support and typed filters.
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

    protected virtual IQueryable<TEntity> GetBaseQuery()
    {
        return _dbSet.AsNoTracking().Where(x => !x.IsDeleted);
    }

    protected virtual Task<IQueryable<TEntity>> ApplyFiltersAsync(IQueryable<TEntity> query, IBaseSearchModel search)
    {
        return Task.FromResult(query);
    }

    public virtual async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await GetBaseQuery().FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public virtual async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken ct = default)
    {
        return await GetBaseQuery().ToListAsync(ct);
    }

    public virtual async Task<(IEnumerable<TEntity> Items, int TotalCount)> GetPagedAsync(IBaseSearchModel search, CancellationToken ct = default)
    {
        var query = GetBaseQuery();
        query = await ApplyFiltersAsync(query, search);

        var total = await query.CountAsync(ct);

        if (!search.DisablePagination)
        {
            query = query
                .Skip((search.PageNumber - 1) * search.PageSize)
                .Take(search.PageSize);
        }

        var items = await query.ToListAsync(ct);
        return (items, total);
    }

    public virtual async Task<IEnumerable<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
    {
        return await _dbSet.Where(predicate).ToListAsync(ct);
    }

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

    public virtual async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _dbSet.FindAsync(new object[] { id }, ct);
        if (entity != null)
        {
            entity.IsDeleted = true;
            _dbSet.Update(entity);
            await _db.SaveChangesAsync(ct);
        }
    }

    public virtual async Task SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);
}
