using JioCxRcsWrapper.Domain.Common;
using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.Domain.Entities;

public sealed class Campaign : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int ClientId { get; set; }
    public CampaignType Type { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;
    public int CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ScheduledAt { get; set; }
    public bool IsRCSEnabled { get; set; } = true;
}
