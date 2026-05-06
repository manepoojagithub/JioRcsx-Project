using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace JioCxRcsWrapper.Web.Hubs;

[Authorize]
public sealed class CampaignHub : Hub
{
    public async Task JoinCampaign(int campaignId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"campaign-{campaignId}");
    }
}
