using FluentAssertions;
using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Application.Security;
using JioCxRcsWrapper.Domain.Common;
using JioCxRcsWrapper.Domain.Entities;
using System.Linq.Expressions;

namespace JioCxRcsWrapper.UnitTests.Security;

public sealed class PermissionServiceTests
{
    [Fact]
    public async Task Admin_CanAccessAllClientScopedModules()
    {
        var service = new PermissionService(SecurityUnitOfWork.Create("Admin"));

        var allowed = await service.HasPermissionAsync(1, "Users", "Delete");

        allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Manager_CannotAccessUserManagement()
    {
        var service = new PermissionService(SecurityUnitOfWork.Create("Manager"));

        var allowed = await service.HasPermissionAsync(2, "Users", "View");

        allowed.Should().BeFalse();
    }

    [Fact]
    public async Task Viewer_CannotAddCampaign()
    {
        var service = new PermissionService(SecurityUnitOfWork.Create("Viewer"));

        var allowed = await service.HasPermissionAsync(3, "Campaigns", "Add");

        allowed.Should().BeFalse();
    }
}

internal sealed class SecurityUnitOfWork : IUnitOfWork
{
    private readonly List<User> _users;
    private readonly List<Role> _roles;
    private readonly List<Permission> _permissions;
    private readonly List<RolePermission> _rolePermissions;

    private SecurityUnitOfWork(string roleName)
    {
        var roleId = roleName switch { "Admin" => 1, "Manager" => 2, _ => 3 };
        _roles =
        [
            new Role { Id = 1, Name = "Admin" },
            new Role { Id = 2, Name = "Manager" },
            new Role { Id = 3, Name = "Viewer" }
        ];
        _permissions =
        [
            new Permission { Id = 1, Name = "View" },
            new Permission { Id = 2, Name = "Add" },
            new Permission { Id = 3, Name = "Update" },
            new Permission { Id = 4, Name = "Delete" },
            new Permission { Id = 5, Name = "Download" }
        ];
        _users = [new User { Id = roleId, Email = $"{roleName}@test.local", Name = roleName, RoleId = roleId, Role = _roles.Single(x => x.Id == roleId), ClientId = roleId == 1 ? null : 10 }];
        _rolePermissions =
        [
            new RolePermission { Id = 1, RoleId = 2, PermissionId = 1, Module = "Campaigns" },
            new RolePermission { Id = 2, RoleId = 2, PermissionId = 2, Module = "Campaigns" },
            new RolePermission { Id = 3, RoleId = 3, PermissionId = 1, Module = "Campaigns" },
            new RolePermission { Id = 4, RoleId = 3, PermissionId = 5, Module = "Reports" }
        ];
        foreach (var rp in _rolePermissions)
        {
            rp.Role = _roles.Single(x => x.Id == rp.RoleId);
            rp.Permission = _permissions.Single(x => x.Id == rp.PermissionId);
        }
    }

    public static SecurityUnitOfWork Create(string roleName) => new(roleName);

    public IRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity
    {
        if (typeof(TEntity) == typeof(User)) return (IRepository<TEntity>)(object)new ListRepository<User>(_users);
        if (typeof(TEntity) == typeof(Role)) return (IRepository<TEntity>)(object)new ListRepository<Role>(_roles);
        if (typeof(TEntity) == typeof(Permission)) return (IRepository<TEntity>)(object)new ListRepository<Permission>(_permissions);
        if (typeof(TEntity) == typeof(RolePermission)) return (IRepository<TEntity>)(object)new ListRepository<RolePermission>(_rolePermissions);
        throw new NotSupportedException(typeof(TEntity).Name);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
}

internal sealed class ListRepository<TEntity> : IRepository<TEntity>
    where TEntity : BaseEntity
{
    private readonly List<TEntity> _items;
    public ListRepository(IEnumerable<TEntity> items) => _items = items.ToList();
    public IQueryable<TEntity> Query() => _items.AsQueryable();
    public Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));
    public Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TEntity>>(_items.AsQueryable().Where(predicate).ToList());
    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) { _items.Add(entity); return Task.CompletedTask; }
    public void Update(TEntity entity) { }
    public void Remove(TEntity entity) => _items.Remove(entity);
}
