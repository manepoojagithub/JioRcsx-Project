using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Domain.Common;
using JioCxRcsWrapper.Infrastructure.Data;

namespace JioCxRcsWrapper.Infrastructure.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;
    private readonly Dictionary<Type, object> _repositories = [];

    public UnitOfWork(AppDbContext db)
    {
        _db = db;
    }

    public IRepository<TEntity> Repository<TEntity>()
        where TEntity : BaseEntity
    {
        var type = typeof(TEntity);
        if (!_repositories.TryGetValue(type, out var repository))
        {
            repository = new Repository<TEntity>(_db);
            _repositories[type] = repository;
        }

        return (IRepository<TEntity>)repository;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
