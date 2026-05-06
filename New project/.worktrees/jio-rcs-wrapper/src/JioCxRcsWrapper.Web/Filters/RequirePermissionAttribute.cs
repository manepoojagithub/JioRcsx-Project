using Microsoft.AspNetCore.Mvc;

namespace JioCxRcsWrapper.Web.Filters;

public sealed class RequirePermissionAttribute : TypeFilterAttribute
{
    public RequirePermissionAttribute(string module, string permission)
        : base(typeof(PermissionFilter))
    {
        Arguments = [module, permission];
    }
}
