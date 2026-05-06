using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Application.Security;
using JioCxRcsWrapper.Domain.Entities;
using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.Application.Dashboard;

public sealed class DashboardService : IDashboardService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public DashboardService(IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var campaigns = _unitOfWork.Repository<Campaign>().Query();
        var clients = _unitOfWork.Repository<Client>().Query();
        if (!string.Equals(_currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            campaigns = campaigns.Where(campaign => campaign.ClientId == _currentUser.ClientId);
            clients = clients.Where(client => client.Id == _currentUser.ClientId);
        }

        var campaignList = campaigns.ToArray();
        var clientList = clients.ToArray();
        var campaignIds = campaignList.Select(campaign => campaign.Id).ToHashSet();
        var contacts = _unitOfWork.Repository<Contact>().Query()
            .Where(contact => campaignIds.Contains(contact.CampaignId))
            .ToArray();
        var contactCounts = contacts.GroupBy(contact => contact.CampaignId).ToDictionary(group => group.Key, group => group.Count());
        var sent = contacts.Count(contact => contact.Status is ContactStatus.Sent or ContactStatus.Delivered or ContactStatus.Opened or ContactStatus.Clicked);
        var delivered = contacts.Count(contact => contact.Status == ContactStatus.Delivered);
        var failed = contacts.Count(contact => contact.Status == ContactStatus.Failed);
        var pending = contacts.Count(contact => contact.Status == ContactStatus.Pending);
        var opened = contacts.Count(contact => contact.Status == ContactStatus.Opened);
        var clicked = contacts.Count(contact => contact.Status == ContactStatus.Clicked);
        var deliveryRate = sent == 0 ? 0 : Math.Round(delivered * 100m / sent, 2);

        return Task.FromResult(new DashboardSummary(
            campaignIds.Count,
            sent,
            delivered,
            failed,
            deliveryRate,
            pending,
            opened,
            clicked,
            campaignList.GroupBy(campaign => campaign.Status.ToString()).Select(group => new CampaignStatusPoint(group.Key, group.Count())).ToArray(),
            [
                new MessageMetricPoint("Pending", pending),
                new MessageMetricPoint("Sent", sent),
                new MessageMetricPoint("Delivered", delivered),
                new MessageMetricPoint("Failed", failed),
                new MessageMetricPoint("Opened", opened),
                new MessageMetricPoint("Clicked", clicked)
            ],
            campaignList.OrderByDescending(campaign => campaign.CreatedAt)
                .Take(8)
                .Select(campaign => new RecentCampaignActivity(
                    campaign.Name,
                    campaign.Status.ToString(),
                    contactCounts.TryGetValue(campaign.Id, out var count) ? count : 0,
                    campaign.CreatedAt))
                .ToArray()));
    }
}
