using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Domain.Entities;

namespace JioCxRcsWrapper.Application.Security;

public sealed class AuditService : IAuditService
{
    private readonly IUnitOfWork _unitOfWork;

    public AuditService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task LogAsync(int userId, string action, string module, CancellationToken cancellationToken = default)
    {
        await _unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
        {
            UserId = userId,
            Action = action,
            Module = module,
            Timestamp = DateTimeOffset.UtcNow
        }, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
