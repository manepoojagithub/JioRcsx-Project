using JioCxRcsWrapper.Application.Queue;
using JioCxRcsWrapper.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace JioCxRcsWrapper.Web.Services;

public sealed class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<CampaignHub> _campaignHub;
    private readonly IHubContext<DashboardHub> _dashboardHub;

    public SignalRRealtimeNotifier(IHubContext<CampaignHub> campaignHub, IHubContext<DashboardHub> dashboardHub)
    {
        _campaignHub = campaignHub;
        _dashboardHub = dashboardHub;
    }

    public async Task CampaignUpdatedAsync(int campaignId, int clientId, object payload, CancellationToken cancellationToken)
    {
        await _campaignHub.Clients.Group($"campaign-{campaignId}").SendAsync("campaignUpdated", payload, cancellationToken);
        await _dashboardHub.Clients.Group($"client-{clientId}").SendAsync("dashboardUpdated", payload, cancellationToken);
        await _dashboardHub.Clients.Group("admin").SendAsync("dashboardUpdated", payload, cancellationToken);
    }

    public async Task DashboardUpdatedAsync(int clientId, object payload, CancellationToken cancellationToken)
    {
        await _dashboardHub.Clients.Group($"client-{clientId}").SendAsync("dashboardUpdated", payload, cancellationToken);
        await _dashboardHub.Clients.Group("admin").SendAsync("dashboardUpdated", payload, cancellationToken);
    }
}
