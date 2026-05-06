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

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}

public interface IJwtTokenService
{
    string CreateToken(int userId, string email, string name, string role, int? clientId, bool isDeveloper);
}
