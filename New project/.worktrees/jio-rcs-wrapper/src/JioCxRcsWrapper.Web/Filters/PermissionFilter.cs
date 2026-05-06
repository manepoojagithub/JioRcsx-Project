using JioCxRcsWrapper.Application.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace JioCxRcsWrapper.Web.Filters;

public sealed class PermissionFilter : IAsyncAuthorizationFilter
{
    private readonly string _module;
    private readonly string _permission;
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionService _permissionService;

    public PermissionFilter(string module, string permission, ICurrentUser currentUser, IPermissionService permissionService)
    {
        _module = module;
        _permission = permission;
        _currentUser = currentUser;
        _permissionService = permissionService;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (!_currentUser.IsAuthenticated)
        {
            context.Result = new RedirectToActionResult("Login", "Account", null);
            return;
        }

        if (!await _permissionService.HasPermissionAsync(_currentUser.UserId, _module, _permission, context.HttpContext.RequestAborted))
        {
            context.Result = new ForbidResult();
        }
    }
}
