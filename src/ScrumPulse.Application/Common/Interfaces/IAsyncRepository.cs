namespace ScrumPulse.Application.Common.Interfaces;

using ScrumPulse.Application.Specifications;
using ScrumPulse.Domain.Common;

public interface IAsyncRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAsync(ISpecification<T> spec, CancellationToken ct = default);
    Task<T?> FirstOrDefaultAsync(ISpecification<T> spec, CancellationToken ct = default);
    Task<int> CountAsync(ISpecification<T>? spec = null, CancellationToken ct = default);
    Task<T> AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(T entity, CancellationToken ct = default);
}

public interface IUnitOfWork : IDisposable
{
    IAsyncRepository<T> Repository<T>() where T : BaseEntity;
    Task<int> CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
