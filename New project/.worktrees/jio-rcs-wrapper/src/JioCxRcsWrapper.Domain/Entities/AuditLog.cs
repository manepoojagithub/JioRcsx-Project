using JioCxRcsWrapper.Domain.Common;

namespace JioCxRcsWrapper.Domain.Entities;

public sealed class AuditLog : BaseEntity
{
    public int UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string? RequestPayload { get; set; }
    public string? ResponseJson { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
