using JioCxRcsWrapper.Domain.Common;

namespace JioCxRcsWrapper.Domain.Entities;

public sealed class Report : BaseEntity
{
    public int CampaignId { get; set; }
    public int TotalSent { get; set; }
    public int Delivered { get; set; }
    public int Failed { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
