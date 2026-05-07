using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Application.Security;
using JioCxRcsWrapper.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace JioCxRcsWrapper.Application.Users;

public sealed class UserManagementService : IUserManagementService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IAuditService _auditService;

    public UserManagementService(IUnitOfWork unitOfWork, IPasswordHasher<User> passwordHasher, IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _auditService = auditService;
    }

    public Task<IReadOnlyList<UserSummary>> ListAsync(UserFilter? filter = null, CancellationToken cancellationToken = default)
    {
        var roles = _unitOfWork.Repository<Role>().Query().ToDictionary(role => role.Id, role => role.Name);
        var clients = _unitOfWork.Repository<Client>().Query().ToDictionary(client => client.Id, client => client.BrandName);

        var result = _unitOfWork.Repository<User>().Query()
            .OrderBy(user => user.Name)
            .ToArray()
            .Select(user => ToSummary(user, roles, clients))
            .AsEnumerable();

        if (filter != null)
        {
            if (!string.IsNullOrWhiteSpace(filter.Name))
                result = result.Where(x => x.Name.Contains(filter.Name, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(filter.Email))
                result = result.Where(x => x.Email.Contains(filter.Email, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(filter.Role))
                result = result.Where(x => x.Role.Contains(filter.Role, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(filter.Client))
                result = result.Where(x => (x.Client ?? "-").Contains(filter.Client, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult<IReadOnlyList<UserSummary>>(result.ToArray());
    }

    public Task<IReadOnlyList<UserCreditHistorySummary>> GetCreditHistoryAsync(int userId, CancellationToken cancellationToken = default)
    {
        var history = _unitOfWork.Repository<UserCreditHistory>().Query()
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.CreatedAt)
            .ToArray();

        var result = history.Select(h => new UserCreditHistorySummary(
            h.Id,
            h.TransactionType,
            h.Amount,
            h.PreviousBalance,
            h.NewBalance,
            h.Reason,
            h.CreatedAt)).ToArray();

        return Task.FromResult<IReadOnlyList<UserCreditHistorySummary>>(result);
    }

    public Task<IReadOnlyList<RoleOption>> ListRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = _unitOfWork.Repository<Role>().Query()
            .OrderBy(role => role.Name)
            .Select(role => new RoleOption(role.Id, role.Name))
            .ToArray();

        return Task.FromResult<IReadOnlyList<RoleOption>>(roles);
    }

    public Task<IReadOnlyList<ClientOption>> ListClientsAsync(CancellationToken cancellationToken = default)
    {
        var clients = _unitOfWork.Repository<Client>().Query()
            .OrderBy(client => client.BrandName)
            .Select(client => new ClientOption(client.Id, client.BrandName))
            .ToArray();

        return Task.FromResult<IReadOnlyList<ClientOption>>(clients);
    }

    public async Task<int> CreateAsync(CreateUserRequest request, int adminUserId, CancellationToken cancellationToken = default)
    {
        ValidateCreate(request);
        if (_unitOfWork.Repository<User>().Query().Any(user => user.Email == request.Email.Trim()))
        {
            throw new InvalidOperationException("Email already exists.");
        }

        EnsureRoleAndClientAreValid(request.RoleId, request.ClientId);
var user = new User
{
    Name = request.Name.Trim(),
    Email = request.Email.Trim(),
    RoleId = request.RoleId,
    ClientId = request.ClientId,
    IsActive = request.IsActive,
    IsDeveloper = request.IsDeveloper,
    Credits = 100, // Default initial credits
    CreatedAt = DateTimeOffset.UtcNow,
    PlainTextPassword = request.Password
    };
    user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        await _unitOfWork.Repository<User>().AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(adminUserId, $"Created user {user.Email}", "Users", cancellationToken);
        return user.Id;
    }

    public async Task<UserEditor?> GetForEditAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Repository<User>().GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        return new UserEditor(user.Id, user.Name, user.Email, user.RoleId, user.ClientId, user.IsActive, user.IsDeveloper);
    }

    public async Task UpdateAsync(UpdateUserRequest request, int adminUserId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Repository<User>().GetByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Name is required.");
        }

        EnsureRoleAndClientAreValid(request.RoleId, request.ClientId);
        user.Name = request.Name.Trim();
        user.RoleId = request.RoleId;
        user.ClientId = request.ClientId;
        user.IsActive = request.IsActive;
        user.IsDeveloper = request.IsDeveloper;

        _unitOfWork.Repository<User>().Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(adminUserId, $"Updated user {user.Email}", "Users", cancellationToken);
    }

    public async Task DeleteAsync(int id, int adminUserId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Repository<User>().GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        _unitOfWork.Repository<User>().Remove(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(adminUserId, $"Deleted user {user.Email}", "Users", cancellationToken);
    }

    public async Task DisableAsync(int id, int adminUserId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Repository<User>().GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        user.IsActive = false;
        _unitOfWork.Repository<User>().Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(adminUserId, $"Disabled user {user.Email}", "Users", cancellationToken);
    }

    private void EnsureRoleAndClientAreValid(int roleId, int? clientId)
    {
        var role = _unitOfWork.Repository<Role>().Query().FirstOrDefault(value => value.Id == roleId)
            ?? throw new InvalidOperationException("Role not found.");

        if (role.Name != "Admin" && clientId is null)
        {
            throw new ArgumentException("Client is required for Manager and Viewer users.");
        }

        if (clientId is not null && !_unitOfWork.Repository<Client>().Query().Any(client => client.Id == clientId))
        {
            throw new InvalidOperationException("Client not found.");
        }
    }

    private static void ValidateCreate(CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("Name, email, and password are required.");
        }
    }

    private static UserSummary ToSummary(User user, IReadOnlyDictionary<int, string> roles, IReadOnlyDictionary<int, string> clients)
    {
        roles.TryGetValue(user.RoleId, out var role);
        var client = user.ClientId is null || !clients.TryGetValue(user.ClientId.Value, out var clientName) ? null : clientName;
        return new UserSummary(user.Id, user.Name, user.Email, user.PlainTextPassword, role ?? "-", client, user.IsActive, user.IsDeveloper, user.CreatedAt);
    }
}
