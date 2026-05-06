using System.Linq.Expressions;
using FluentAssertions;
using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Application.Permissions;
using JioCxRcsWrapper.Application.Security;
using JioCxRcsWrapper.Domain.Common;
using JioCxRcsWrapper.Domain.Entities;

namespace JioCxRcsWrapper.UnitTests.Permissions;

public sealed class PermissionManagementServiceTests
{
    [Fact]
    public async Task GetEditor_ReturnsMenuWiseMetadata()
    {
        var unitOfWork = PermissionManagementUnitOfWork.Create();
        var service = new PermissionManagementService(unitOfWork, new NoopAuditService());

        var editor = await service.GetEditorAsync(2);

        var usersMenu = editor.Modules.Single(value => value.Module == "Users");
        usersMenu.MenuName.Should().Be("User Management");
        usersMenu.Controller.Should().Be("Users");
        usersMenu.Action.Should().Be("Index");
        editor.IsDeveloper.Should().BeFalse();
    }

    [Fact]
    public async Task Update_AddsViewPermissionWhenActionPermissionIsSelected()
    {
        var unitOfWork = PermissionManagementUnitOfWork.Create();
        var service = new PermissionManagementService(unitOfWork, new NoopAuditService());

        await service.UpdateAsync(
            2,
            new Dictionary<string, IReadOnlyList<int>> { ["Campaigns"] = [2] },
            isDeveloper: false,
            adminUserId: 1);

        unitOfWork.RolePermissions.Should().Contain(value => value.RoleId == 2 && value.Module == "Campaigns" && value.PermissionId == 1);
        unitOfWork.RolePermissions.Should().Contain(value => value.RoleId == 2 && value.Module == "Campaigns" && value.PermissionId == 2);
    }

    [Fact]
    public async Task Update_SavesDeveloperAccessOnRole()
    {
        var unitOfWork = PermissionManagementUnitOfWork.Create();
        var service = new PermissionManagementService(unitOfWork, new NoopAuditService());

        await service.UpdateAsync(
            2,
            new Dictionary<string, IReadOnlyList<int>>(),
            isDeveloper: true,
            adminUserId: 1);

        unitOfWork.Roles.Single(role => role.Id == 2).IsDeveloper.Should().BeTrue();
    }

    private sealed class NoopAuditService : IAuditService
    {
        public Task LogAsync(int userId, string action, string module, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class PermissionManagementUnitOfWork : IUnitOfWork
    {
        public List<Role> Roles { get; } =
        [
            new() { Id = 1, Name = "Admin" },
            new() { Id = 2, Name = "Manager" }
        ];

        public List<Permission> Permissions { get; } =
        [
            new() { Id = 1, Name = "View" },
            new() { Id = 2, Name = "Add" },
            new() { Id = 3, Name = "Update" },
            new() { Id = 4, Name = "Delete" },
            new() { Id = 5, Name = "Download" }
        ];

        public List<RolePermission> RolePermissions { get; } =
        [
            new() { Id = 1, RoleId = 2, PermissionId = 1, Module = "Dashboard" }
        ];

        public static PermissionManagementUnitOfWork Create() => new();

        public IRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity
        {
            if (typeof(TEntity) == typeof(Role)) return (IRepository<TEntity>)(object)new MutableRepository<Role>(Roles);
            if (typeof(TEntity) == typeof(Permission)) return (IRepository<TEntity>)(object)new MutableRepository<Permission>(Permissions);
            if (typeof(TEntity) == typeof(RolePermission)) return (IRepository<TEntity>)(object)new MutableRepository<RolePermission>(RolePermissions);
            throw new NotSupportedException(typeof(TEntity).Name);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class MutableRepository<TEntity> : IRepository<TEntity>
        where TEntity : BaseEntity
    {
        private readonly List<TEntity> _items;

        public MutableRepository(List<TEntity> items) => _items = items;

        public IQueryable<TEntity> Query() => _items.AsQueryable();

        public Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_items.FirstOrDefault(value => value.Id == id));

        public Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TEntity>>(_items.AsQueryable().Where(predicate).ToArray());

        public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            entity.Id = entity.Id == 0 ? _items.Count + 1 : entity.Id;
            _items.Add(entity);
            return Task.CompletedTask;
        }

        public void Update(TEntity entity)
        {
        }

        public void Remove(TEntity entity) => _items.Remove(entity);
    }
}
