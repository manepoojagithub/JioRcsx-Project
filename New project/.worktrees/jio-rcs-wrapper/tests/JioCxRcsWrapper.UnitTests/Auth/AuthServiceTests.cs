using System.Linq.Expressions;
using FluentAssertions;
using JioCxRcsWrapper.Application.Auth;
using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Domain.Common;
using JioCxRcsWrapper.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace JioCxRcsWrapper.UnitTests.Auth;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task Login_WithInactiveUser_IsRejected()
    {
        var service = AuthServiceTestFactory.CreateWithUser(
            email: "manager@example.com",
            password: "Password@123",
            isActive: false,
            roleName: "Manager",
            clientId: 10);

        var result = await service.LoginAsync(new LoginRequest("manager@example.com", "Password@123"));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Invalid email or password.");
        result.JwtToken.Should().BeNull();
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsJwtAndUserClaims()
    {
        var service = AuthServiceTestFactory.CreateWithUser(
            email: "manager@example.com",
            password: "Password@123",
            isActive: true,
            roleName: "Manager",
            clientId: 10);

        var result = await service.LoginAsync(new LoginRequest("manager@example.com", "Password@123"));

        result.Succeeded.Should().BeTrue();
        result.JwtToken.Should().Be("token");
        result.Role.Should().Be("Manager");
        result.ClientId.Should().Be(10);
    }

    [Fact]
    public async Task Login_UsesRoleDeveloperAccessForDiagnosticsClaim()
    {
        var service = AuthServiceTestFactory.CreateWithUser(
            email: "developer@example.com",
            password: "Password@123",
            isActive: true,
            roleName: "Manager",
            clientId: 10,
            roleIsDeveloper: true);

        var result = await service.LoginAsync(new LoginRequest("developer@example.com", "Password@123"));

        result.IsDeveloper.Should().BeTrue();
    }
}

internal static class AuthServiceTestFactory
{
    public static AuthService CreateWithUser(string email, string password, bool isActive, string roleName, int? clientId, bool roleIsDeveloper = false)
    {
        var role = new Role { Id = 1, Name = roleName, IsDeveloper = roleIsDeveloper };
        var user = new User
        {
            Id = 5,
            Name = "Test User",
            Email = email,
            IsActive = isActive,
            RoleId = role.Id,
            Role = role,
            ClientId = clientId
        };

        var hasher = new PasswordHasher<User>();
        user.PasswordHash = hasher.HashPassword(user, password);

        return new AuthService(
            new FakeUnitOfWork([user], [role]),
            hasher,
            new StubJwtTokenService());
    }
}

internal sealed class StubJwtTokenService : IJwtTokenService
{
    public string CreateToken(int userId, string email, string name, string role, int? clientId, bool isDeveloper) => "token";
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    private readonly IReadOnlyList<User> _users;
    private readonly IReadOnlyList<Role> _roles;

    public FakeUnitOfWork(IReadOnlyList<User> users, IReadOnlyList<Role> roles)
    {
        _users = users;
        _roles = roles;
    }

    public IRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity
    {
        if (typeof(TEntity) == typeof(User))
        {
            return (IRepository<TEntity>)(object)new FakeRepository<User>(_users);
        }

        if (typeof(TEntity) == typeof(Role))
        {
            return (IRepository<TEntity>)(object)new FakeRepository<Role>(_roles);
        }

        throw new NotSupportedException(typeof(TEntity).Name);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
}

internal sealed class FakeRepository<TEntity> : IRepository<TEntity>
    where TEntity : BaseEntity
{
    private readonly List<TEntity> _items;

    public FakeRepository(IEnumerable<TEntity> items)
    {
        _items = items.ToList();
    }

    public IQueryable<TEntity> Query() => _items.AsQueryable();

    public Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_items.FirstOrDefault(x => x.Id == id));

    public Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TEntity>>(_items.AsQueryable().Where(predicate).ToList());

    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        _items.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(TEntity entity)
    {
    }

    public void Remove(TEntity entity)
    {
        _items.Remove(entity);
    }
}
