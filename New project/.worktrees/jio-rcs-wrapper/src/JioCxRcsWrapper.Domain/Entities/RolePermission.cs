using JioCxRcsWrapper.Domain.Common;

namespace JioCxRcsWrapper.Domain.Entities;

public sealed class RolePermission : BaseEntity
{
    public int RoleId { get; set; }
    public int PermissionId { get; set; }
    public string Module { get; set; } = string.Empty;
    public Role Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}
