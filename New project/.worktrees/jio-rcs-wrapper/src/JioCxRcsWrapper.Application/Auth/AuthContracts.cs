namespace JioCxRcsWrapper.Application.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResult(
    bool Succeeded,
    string? Error,
    int? UserId,
    string? Name,
    string? Role,
    int? ClientId,
    bool IsDeveloper,
    string? JwtToken);

public sealed record UserDetails(int Id, string Name, string Email, string Role, int? ClientId);

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<UserDetails?> GetUserByIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<AuthOperationResult> ChangePasswordAsync(int userId, string newPassword, CancellationToken cancellationToken = default);
}

public sealed record AuthOperationResult(bool Succeeded, string? Error);

public interface IJwtTokenService
{
    string CreateToken(int userId, string email, string name, string role, int? clientId, bool isDeveloper);
}
