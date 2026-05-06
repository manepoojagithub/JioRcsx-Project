using JioCxRcsWrapper.Domain.Common;

namespace JioCxRcsWrapper.Domain.Entities;

public sealed class Role : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public bool IsDeveloper { get; set; }
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
