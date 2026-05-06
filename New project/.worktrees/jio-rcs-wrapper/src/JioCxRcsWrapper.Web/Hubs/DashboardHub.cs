using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace JioCxRcsWrapper.Web.Hubs;

[Authorize]
public sealed class DashboardHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole("Admin") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "admin");
        }

        var clientId = Context.User?.FindFirstValue("client_id");
        if (!string.IsNullOrWhiteSpace(clientId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"client-{clientId}");
        }

        await base.OnConnectedAsync();
    }
}
