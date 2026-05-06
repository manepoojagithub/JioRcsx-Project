namespace JioCxRcsWrapper.Application.Permissions;

public sealed record PermissionMatrix(
    string Module,
    string MenuName,
    string Description,
    string Controller,
    string Action,
    IReadOnlyList<PermissionMatrixItem> Items);

public sealed record PermissionMatrixItem(int PermissionId, string PermissionName, bool IsSelected);

public sealed record RolePermissionEditor(int RoleId, string RoleName, bool IsDeveloper, IReadOnlyList<PermissionMatrix> Modules);

public interface IPermissionManagementService
{
    Task<IReadOnlyList<RoleOption>> ListRolesAsync(CancellationToken cancellationToken = default);

    Task<RolePermissionEditor> GetEditorAsync(int roleId, CancellationToken cancellationToken = default);

    Task UpdateAsync(int roleId, IReadOnlyDictionary<string, IReadOnlyList<int>> selectedPermissions, bool isDeveloper, int adminUserId, CancellationToken cancellationToken = default);
}

public sealed record RoleOption(int Id, string Name);
