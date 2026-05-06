using System.Linq.Expressions;
using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Domain.Common;
using JioCxRcsWrapper.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace JioCxRcsWrapper.Infrastructure.Repositories;

public sealed class Repository<TEntity> : IRepository<TEntity>
    where TEntity : BaseEntity
{
    private readonly AppDbContext _db;

    public Repository(AppDbContext db)
    {
        _db = db;
    }

    public IQueryable<TEntity> Query() => _db.Set<TEntity>().AsQueryable();

    public Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _db.Set<TEntity>().FindAsync([id], cancellationToken).AsTask();

    public async Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        await _db.Set<TEntity>().Where(predicate).ToListAsync(cancellationToken);

    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        _db.Set<TEntity>().AddAsync(entity, cancellationToken).AsTask();

    public void Update(TEntity entity) => _db.Set<TEntity>().Update(entity);

    public void Remove(TEntity entity) => _db.Set<TEntity>().Remove(entity);
}
