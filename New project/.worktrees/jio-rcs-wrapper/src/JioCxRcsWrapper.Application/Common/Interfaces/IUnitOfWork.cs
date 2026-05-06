using JioCxRcsWrapper.Domain.Common;

namespace JioCxRcsWrapper.Application.Common.Interfaces;

public interface IUnitOfWork
{
    IRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
