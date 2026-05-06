using JioCxRcsWrapper.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JioCxRcsWrapper.Infrastructure.Data;

public static class SeedData
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Name = "Admin", IsDeveloper = true },
            new Role { Id = 2, Name = "Manager" },
            new Role { Id = 3, Name = "Viewer" });

        modelBuilder.Entity<Permission>().HasData(
            new Permission { Id = 1, Name = "View" },
            new Permission { Id = 2, Name = "Add" },
            new Permission { Id = 3, Name = "Update" },
            new Permission { Id = 4, Name = "Delete" },
            new Permission { Id = 5, Name = "Download" },
            new Permission { Id = 6, Name = "Disable" });

        modelBuilder.Entity<User>().HasData(new User
        {
            Id = 1,
            Name = "System Admin",
            Email = "admin@local.test",
            PasswordHash = "AQAAAAIAAYagAAAAECm1p9KdJkz9/8pNO5Yb/5v6s+zxD9nBzWjydYpQ3ytXUvutLz6rvpLLecJxW7iWVQ==",
            RoleId = 1,
            ClientId = null,
            IsActive = true,
            IsDeveloper = true,
            CreatedAt = new DateTimeOffset(2026, 5, 2, 0, 0, 0, TimeSpan.Zero)
        });

        var modules = new[] { "Dashboard", "Clients", "Users", "Campaigns", "Reports", "AuditLogs", "Media" };
        var rolePermissions = new List<RolePermission>();
        var id = 1;
        foreach (var module in modules)
        {
            for (var permissionId = 1; permissionId <= 5; permissionId++)
            {
                rolePermissions.Add(new RolePermission { Id = id++, RoleId = 1, PermissionId = permissionId, Module = module });
            }
        }

        foreach (var module in new[] { "Dashboard", "Campaigns", "Reports", "Media" })
        {
            rolePermissions.Add(new RolePermission { Id = id++, RoleId = 2, PermissionId = 1, Module = module });
        }

        foreach (var permissionId in new[] { 2, 3, 4 })
        {
            rolePermissions.Add(new RolePermission { Id = id++, RoleId = 2, PermissionId = permissionId, Module = "Campaigns" });
        }

        rolePermissions.Add(new RolePermission { Id = id++, RoleId = 2, PermissionId = 5, Module = "Reports" });
        rolePermissions.Add(new RolePermission { Id = id++, RoleId = 3, PermissionId = 1, Module = "Dashboard" });
        rolePermissions.Add(new RolePermission { Id = id++, RoleId = 3, PermissionId = 1, Module = "Reports" });
        rolePermissions.Add(new RolePermission { Id = id++, RoleId = 3, PermissionId = 5, Module = "Reports" });

        for (var permissionId = 1; permissionId <= 5; permissionId++)
        {
            rolePermissions.Add(new RolePermission { Id = id++, RoleId = 1, PermissionId = permissionId, Module = "MessageBuilder" });
        }

        foreach (var permissionId in new[] { 1, 2, 3, 4 })
        {
            rolePermissions.Add(new RolePermission { Id = id++, RoleId = 2, PermissionId = permissionId, Module = "MessageBuilder" });
        }

        rolePermissions.Add(new RolePermission { Id = id++, RoleId = 3, PermissionId = 1, Module = "MessageBuilder" });
        rolePermissions.Add(new RolePermission { Id = id++, RoleId = 1, PermissionId = 6, Module = "Users" });

        modelBuilder.Entity<RolePermission>().HasData(rolePermissions);
    }
}
