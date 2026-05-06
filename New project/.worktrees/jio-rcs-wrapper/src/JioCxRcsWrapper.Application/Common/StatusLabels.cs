using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.Application.Common;

public static class StatusLabels
{
    public static string For(ContactStatus status) => status switch
    {
        ContactStatus.Sent => "Successfully Send",
        _ => status.ToString()
    };

    public static string For(CampaignStatus status) => status switch
    {
        CampaignStatus.Completed => "Successfully Send",
        _ => status.ToString()
    };
}
