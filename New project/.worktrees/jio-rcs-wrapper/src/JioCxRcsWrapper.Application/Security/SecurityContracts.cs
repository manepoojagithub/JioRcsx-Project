namespace JioCxRcsWrapper.Application.Security;
public interface ICurrentUser
{
    int UserId { get; }
    string Role { get; }
    int? ClientId { get; }
    bool IsAuthenticated { get; }
    bool IsDeveloper { get; }
}

public sealed record UserCreditInfo(int Credits, int LowCreditThreshold);

public interface IUserCreditService
{
    Task<UserCreditInfo?> GetCurrentUserCreditsAsync(CancellationToken cancellationToken = default);
}

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(int userId, string module, string permission, CancellationToken cancellationToken = default);
    Task EnsureClientScopeAsync(int userId, int clientId, CancellationToken cancellationToken = default);
}

public interface IAuditService
{
    Task LogAsync(int userId, string action, string module, CancellationToken cancellationToken = default);
    Task LogAsync(int userId, string action, string module, string? requestPayload, string? responseJson, CancellationToken cancellationToken = default);
}
