using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Domain.Entities;

namespace JioCxRcsWrapper.Application.Security;

public sealed class PermissionService : IPermissionService
{
    private readonly IUnitOfWork _unitOfWork;

    public PermissionService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public Task<bool> HasPermissionAsync(int userId, string module, string permission, CancellationToken cancellationToken = default)
    {
        var user = _unitOfWork.Repository<User>().Query().FirstOrDefault(x => x.Id == userId);
        if (user is null || !user.IsActive)
        {
            return Task.FromResult(false);
        }

        var roleName = user.Role?.Name ?? _unitOfWork.Repository<Role>().Query().FirstOrDefault(x => x.Id == user.RoleId)?.Name;
        if (string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(true);
        }

        var permissionId = _unitOfWork.Repository<Permission>()
            .Query()
            .FirstOrDefault(x => x.Name == permission)
            ?.Id;

        if (!permissionId.HasValue)
        {
            return Task.FromResult(false);
        }

        var allowed = _unitOfWork.Repository<RolePermission>()
            .Query()
            .Any(x => x.RoleId == user.RoleId && x.PermissionId == permissionId.Value && x.Module == module);

        return Task.FromResult(allowed);
    }

    public Task EnsureClientScopeAsync(int userId, int clientId, CancellationToken cancellationToken = default)
    {
        var user = _unitOfWork.Repository<User>().Query().FirstOrDefault(x => x.Id == userId)
            ?? throw new UnauthorizedAccessException("User not found.");
        var roleName = user.Role?.Name ?? _unitOfWork.Repository<Role>().Query().FirstOrDefault(x => x.Id == user.RoleId)?.Name;
        if (string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        if (user.ClientId != clientId)
        {
            throw new UnauthorizedAccessException("Client scope violation.");
        }

        return Task.CompletedTask;
    }
}
