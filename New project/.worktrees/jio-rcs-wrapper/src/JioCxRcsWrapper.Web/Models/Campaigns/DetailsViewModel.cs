using JioCxRcsWrapper.Application.Campaigns;
using JioCxRcsWrapper.Application.Templates;

namespace JioCxRcsWrapper.Web.Models.Campaigns;

public class DetailsViewModel
{
    public CampaignSummary Campaign { get; set; }
    public IReadOnlyList<ContactSummary> Contacts { get; set; }
    public MessageTemplateEditor? Template { get; set; }
}
