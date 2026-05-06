using JioCxRcsWrapper.Domain.Common;
using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.Domain.Entities;

public sealed class CampaignMessage : BaseEntity
{
    public int CampaignId { get; set; }
    public int? TemplateId { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public MessageType MessageType { get; set; }
}
