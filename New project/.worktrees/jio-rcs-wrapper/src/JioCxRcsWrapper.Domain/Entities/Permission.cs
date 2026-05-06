using JioCxRcsWrapper.Domain.Common;

namespace JioCxRcsWrapper.Domain.Entities;

public sealed class Permission : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
