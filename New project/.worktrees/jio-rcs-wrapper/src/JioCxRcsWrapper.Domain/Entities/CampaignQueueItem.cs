using JioCxRcsWrapper.Domain.Common;
using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.Domain.Entities;

public sealed class CampaignQueueItem : BaseEntity
{
    public int CampaignId { get; set; }
    public int ContactId { get; set; }
    public CampaignQueueStatus Status { get; set; } = CampaignQueueStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? LockedAt { get; set; }
    public string? LockedBy { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
}
