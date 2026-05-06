using JioCxRcsWrapper.Domain.Common;
using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.Domain.Entities;

public sealed class MessageTemplate : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int? ClientId { get; set; }
    public MessageType MessageType { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public string? LocalMediaPath { get; set; }
    public string? RcsMediaUrl { get; set; }
    public string? MediaContentType { get; set; }
    public int CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
