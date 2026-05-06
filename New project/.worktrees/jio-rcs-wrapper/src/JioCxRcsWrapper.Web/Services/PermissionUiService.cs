using JioCxRcsWrapper.Application.Security;

namespace JioCxRcsWrapper.Web.Services;

public sealed class PermissionUiService
{
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionService _permissionService;

    public PermissionUiService(ICurrentUser currentUser, IPermissionService permissionService)
    {
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    public bool IsDeveloper => _currentUser.IsDeveloper;

    public Task<bool> CanViewAsync(string module) =>
        _currentUser.IsAuthenticated
            ? _permissionService.HasPermissionAsync(_currentUser.UserId, module, "View")
            : Task.FromResult(false);

    public Task<bool> CanAsync(string module, string permission) =>
        _currentUser.IsAuthenticated
            ? _permissionService.HasPermissionAsync(_currentUser.UserId, module, permission)
            : Task.FromResult(false);
}
