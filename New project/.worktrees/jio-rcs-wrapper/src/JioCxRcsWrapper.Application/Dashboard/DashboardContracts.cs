namespace JioCxRcsWrapper.Application.Dashboard;

public sealed record DashboardSummary(
    int TotalCampaigns,
    int Sent,
    int Delivered,
    int Failed,
    decimal DeliveryRate,
    int Pending,
    int Opened,
    int Clicked,
    IReadOnlyList<CampaignStatusPoint> CampaignStatuses,
    IReadOnlyList<MessageMetricPoint> MessageMetrics,
    IReadOnlyList<RecentCampaignActivity> RecentCampaigns);

public sealed record CampaignStatusPoint(string Status, int Count);

public sealed record MessageMetricPoint(string Label, int Value);

public sealed record RecentCampaignActivity(string Campaign, string Status, int Contacts, DateTimeOffset CreatedAt);

public interface IDashboardService
{
    Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken);
}
