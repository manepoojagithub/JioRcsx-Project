namespace JioCxRcsWrapper.Application.Users;

public sealed record UserSummary(
    int Id,
    string Name,
    string Email,
    string? Password,
    string Role,
    string? Client,
    bool IsActive,
    bool IsDeveloper,
    DateTimeOffset CreatedAt);

public sealed record UserEditor(int Id, string Name, string Email, int RoleId, int? ClientId, bool IsActive, bool IsDeveloper);

public sealed record RoleOption(int Id, string Name);

public sealed record ClientOption(int Id, string BrandName);

public sealed record CreateUserRequest(string Name, string Email, string Password, int RoleId, int? ClientId, bool IsActive, bool IsDeveloper);

public sealed record UpdateUserRequest(int Id, string Name, int RoleId, int? ClientId, bool IsActive, bool IsDeveloper);

public sealed record UserFilter(string? Name = null, string? Email = null, string? Role = null, string? Client = null);

public sealed record UserCreditHistorySummary(
    int Id,
    string TransactionType,
    int Amount,
    int PreviousBalance,
    int NewBalance,
    string Reason,
    DateTimeOffset CreatedAt);

public interface IUserManagementService
{
    Task<IReadOnlyList<UserSummary>> ListAsync(UserFilter? filter = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserCreditHistorySummary>> GetCreditHistoryAsync(int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleOption>> ListRolesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClientOption>> ListClientsAsync(CancellationToken cancellationToken = default);

    Task<int> CreateAsync(CreateUserRequest request, int adminUserId, CancellationToken cancellationToken = default);

    Task<UserEditor?> GetForEditAsync(int id, CancellationToken cancellationToken = default);

    Task UpdateAsync(UpdateUserRequest request, int adminUserId, CancellationToken cancellationToken = default);

    Task DisableAsync(int id, int adminUserId, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, int adminUserId, CancellationToken cancellationToken = default);
}
