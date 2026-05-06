using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Application.Security;
using JioCxRcsWrapper.Domain.Entities;

namespace JioCxRcsWrapper.Application.Permissions;

public sealed class PermissionManagementService : IPermissionManagementService
{
    private static readonly PermissionMenu[] Menus =
    [
        new("Dashboard", "Dashboard", "Realtime campaign and delivery overview.", "Dashboard", "Index"),
        new("Clients", "Client Onboarding", "Agent/client onboarding, API key setup, logos, and branding.", "Clients", "Index"),
        new("Users", "User Management", "Panel users, roles, and role permission setup.", "Users", "Index"),
        new("Campaigns", "Campaigns", "One-time, scheduled, recurring campaigns, contacts, and CSV upload.", "Campaigns", "Index"),
        new("MessageBuilder", "Message Builder", "RCSX template builder, media upload, CTA buttons, and previews.", "MessageBuilder", "Index"),
        new("Reports", "Reports", "Message delivery reports and CSV/PDF downloads.", "Reports", "Index"),
        new("AuditLogs", "Audit Logs", "Administrative and operational audit trail.", "AuditLogs", "Index"),
        new("Media", "Media Library", "Uploaded media files used by RCSX templates.", "Media", "Index")
    ];

    private static readonly string[] Modules = Menus.Select(menu => menu.Key).ToArray();
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public PermissionManagementService(IUnitOfWork unitOfWork, IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public Task<IReadOnlyList<RoleOption>> ListRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = _unitOfWork.Repository<Role>().Query()
            .OrderBy(role => role.Name)
            .Select(role => new RoleOption(role.Id, role.Name))
            .ToArray();
        return Task.FromResult<IReadOnlyList<RoleOption>>(roles);
    }

    public Task<RolePermissionEditor> GetEditorAsync(int roleId, CancellationToken cancellationToken = default)
    {
        var role = _unitOfWork.Repository<Role>().Query().FirstOrDefault(value => value.Id == roleId)
            ?? throw new InvalidOperationException("Role not found.");
        var permissions = _unitOfWork.Repository<Permission>().Query().OrderBy(value => value.Id).ToArray();
        var selected = _unitOfWork.Repository<RolePermission>().Query()
            .Where(value => value.RoleId == roleId)
            .Select(value => new { value.Module, value.PermissionId })
            .ToHashSet();

        var modules = Menus.Select(menu => new PermissionMatrix(
            menu.Key,
            menu.Name,
            menu.Description,
            menu.Controller,
            menu.Action,
            permissions.Select(permission => new PermissionMatrixItem(
                permission.Id,
                permission.Name,
                selected.Contains(new { Module = menu.Key, PermissionId = permission.Id }))).ToArray())).ToArray();

        return Task.FromResult(new RolePermissionEditor(role.Id, role.Name, role.IsDeveloper, modules));
    }

    public async Task UpdateAsync(int roleId, IReadOnlyDictionary<string, IReadOnlyList<int>> selectedPermissions, bool isDeveloper, int adminUserId, CancellationToken cancellationToken = default)
    {
        var role = _unitOfWork.Repository<Role>().Query().FirstOrDefault(value => value.Id == roleId)
            ?? throw new InvalidOperationException("Role not found.");
        role.IsDeveloper = isDeveloper;
        var permissions = _unitOfWork.Repository<Permission>().Query().ToArray();
        var validPermissionIds = permissions.Select(value => value.Id).ToHashSet();
        var viewPermissionId = permissions.FirstOrDefault(value => value.Name == "View")?.Id;
        var repository = _unitOfWork.Repository<RolePermission>();
        var existing = repository.Query().Where(value => value.RoleId == roleId).ToArray();
        foreach (var item in existing)
        {
            repository.Remove(item);
        }

        var added = new HashSet<(string Module, int PermissionId)>();
        foreach (var module in Modules)
        {
            if (!selectedPermissions.TryGetValue(module, out var permissionIds))
            {
                continue;
            }

            foreach (var permissionId in permissionIds.Distinct().Where(validPermissionIds.Contains))
            {
                await AddRolePermissionAsync(repository, added, roleId, module, permissionId, cancellationToken);
            }

            if (viewPermissionId is not null && permissionIds.Any(permissionId => permissionId != viewPermissionId && validPermissionIds.Contains(permissionId)))
            {
                await AddRolePermissionAsync(repository, added, roleId, module, viewPermissionId.Value, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(adminUserId, $"Updated permissions for {role.Name}", "Users", cancellationToken);
    }

    private static async Task AddRolePermissionAsync(
        IRepository<RolePermission> repository,
        HashSet<(string Module, int PermissionId)> added,
        int roleId,
        string module,
        int permissionId,
        CancellationToken cancellationToken)
    {
        if (!added.Add((module, permissionId)))
        {
            return;
        }

        await repository.AddAsync(new RolePermission
        {
            RoleId = roleId,
            PermissionId = permissionId,
            Module = module
        }, cancellationToken);
    }

    private sealed record PermissionMenu(string Key, string Name, string Description, string Controller, string Action);
}
