using JioCxRcsWrapper.Domain.Common;
using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.Domain.Entities;

public sealed class Contact : BaseEntity
{
    public int CampaignId { get; set; }
    public string MobileNumber { get; set; } = string.Empty;
    public ContactStatus Status { get; set; } = ContactStatus.Pending;
}
