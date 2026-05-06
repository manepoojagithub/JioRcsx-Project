using System.Security.Claims;
using JioCxRcsWrapper.Application.Security;

namespace JioCxRcsWrapper.Web.Services;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public int UserId => int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    public string Role => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    public int? ClientId => int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("client_id"), out var id) ? id : null;

    public bool IsDeveloper => bool.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("is_developer"), out var value) && value;
}
