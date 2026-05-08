using JioCxRcsWrapper.Domain.Common;

namespace JioCxRcsWrapper.Domain.Entities;

public sealed class MessageLog : BaseEntity
{
    public int CampaignId { get; set; }
    public int ContactId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public string? RequestPayload { get; set; }
    public string? ResponseJson { get; set; }
    public string Response { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
