namespace ScrumPulse.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Specifications;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Domain.Common;
using ScrumPulse.Infrastructure.Persistence;

public class EfRepository<T>(AppDbContext db) : IAsyncRepository<T> where T : BaseEntity
{
    private readonly DbSet<T> _dbSet = db.Set<T>();

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _dbSet.FirstOrDefaultAsync(entity => entity.Id == id, ct);

    public async Task<IReadOnlyList<T>> ListAllAsync(CancellationToken ct = default) =>
        await _dbSet.ToListAsync(ct);

    public async Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec, CancellationToken ct = default) =>
        await ApplySpecification(spec).ToListAsync(ct);

    public async Task<T?> FirstOrDefaultAsync(ISpecification<T> spec, CancellationToken ct = default) =>
        await ApplySpecification(spec).FirstOrDefaultAsync(ct);

    public async Task<int> CountAsync(ISpecification<T>? spec = null, CancellationToken ct = default) =>
        spec != null ? await ApplySpecification(spec).CountAsync(ct) : await _dbSet.CountAsync(ct);

    public async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        await _dbSet.AddAsync(entity, ct);
        return entity;
    }

    public Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(T entity, CancellationToken ct = default)
    {
        _dbSet.Remove(entity);
        return Task.CompletedTask;
    }

    private IQueryable<T> ApplySpecification(ISpecification<T> spec)
    {
        var query = _dbSet.AsQueryable();

        if (spec.Criteria != null)
        {
            query = query.Where(spec.Criteria);
        }

        query = spec.Includes.Aggregate(query, (current, include) => current.Include(include));
        query = spec.IncludeStrings.Aggregate(query, (current, include) => current.Include(include));

        if (spec.OrderBy != null)
        {
            query = query.OrderBy(spec.OrderBy);
        }
        else if (spec.OrderByDescending != null)
        {
            query = query.OrderByDescending(spec.OrderByDescending);
        }

        if (spec.IsPagingEnabled)
        {
            query = query.Skip(spec.Skip).Take(spec.Take);
        }

        return query;
    }
}
