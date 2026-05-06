using JioCxRcsWrapper.Domain.Common;

namespace JioCxRcsWrapper.Domain.Entities;

public sealed class User : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public int? ClientId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeveloper { get; set; }
    public int Credits { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Role Role { get; set; } = null!;
    public Client? Client { get; set; }
}
