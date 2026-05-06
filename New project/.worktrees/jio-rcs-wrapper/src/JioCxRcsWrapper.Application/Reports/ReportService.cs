using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Application.Security;
using JioCxRcsWrapper.Domain.Entities;
using JioCxRcsWrapper.Domain.Enums;
using System.Text.Json;

namespace JioCxRcsWrapper.Application.Reports;

public sealed class ReportService : IReportService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public ReportService(IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public Task<IReadOnlyList<CampaignReportSummary>> GetCampaignReportsAsync(ReportFilter? filter = null, CancellationToken cancellationToken = default)
    {
        var campaigns = ScopedCampaigns().ToArray();
        var contacts = _unitOfWork.Repository<Contact>().Query().ToArray();
        var clientNames = _unitOfWork.Repository<Client>().Query()
            .ToDictionary(client => client.Id, client => client.BrandName);

        var result = campaigns.Select(campaign =>
        {
            var campaignContacts = contacts.Where(contact => contact.CampaignId == campaign.Id).ToArray();
            return new CampaignReportSummary(
                campaign.Id,
                campaign.Name,
                campaign.ClientId,
                clientNames.TryGetValue(campaign.ClientId, out var clientName) ? clientName : "-",
                campaignContacts.Count(contact => contact.Status is ContactStatus.Sent or ContactStatus.Delivered or ContactStatus.Opened or ContactStatus.Clicked),
                campaignContacts.Count(contact => contact.Status == ContactStatus.Delivered),
                campaignContacts.Count(contact => contact.Status == ContactStatus.Failed));
        }).AsEnumerable();

        if (filter != null)
        {
            if (!string.IsNullOrWhiteSpace(filter.CampaignName))
                result = result.Where(x => x.CampaignName.Contains(filter.CampaignName, StringComparison.OrdinalIgnoreCase));
            
            if (!string.IsNullOrWhiteSpace(filter.ClientName))
                result = result.Where(x => x.ClientName.Contains(filter.ClientName, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult<IReadOnlyList<CampaignReportSummary>>(result.ToArray());
    }

    public Task<ContactReportResult> GetContactReportAsync(int campaignId, ContactReportFilter? filter = null, CancellationToken cancellationToken = default)
    {
        var campaign = ScopedCampaigns().SingleOrDefault(value => value.Id == campaignId);
        if (campaign is null)
        {
            return Task.FromResult(ContactReportResult.Failed("Campaign not found."));
        }

        var logs = _unitOfWork.Repository<MessageLog>().Query()
            .Where(log => log.CampaignId == campaignId)
            .ToArray();

        var result = _unitOfWork.Repository<Contact>().Query()
            .Where(contact => contact.CampaignId == campaignId)
            .ToArray()
            .Select(contact =>
            {
                var contactLogs = logs.Where(log => log.ContactId == contact.Id).ToArray();
                var lastLog = contactLogs.OrderByDescending(log => log.Timestamp).FirstOrDefault();
                var diagnostic = _currentUser.IsDeveloper
                    ? ApiLogDiagnostic.FromLog(lastLog)
                    : new ApiLogDiagnostic(null, null, null, null, null);
                return new ContactReportRow(
                    campaign.Name,
                    contact.MobileNumber,
                    contact.Status,
                    contact.Status == ContactStatus.Opened || contactLogs.Any(log => log.Status.Equals("Opened", StringComparison.OrdinalIgnoreCase)),
                    contact.Status == ContactStatus.Clicked || contactLogs.Any(log => log.Status.Equals("Clicked", StringComparison.OrdinalIgnoreCase)),
                    _currentUser.IsDeveloper ? lastLog?.ErrorCode : null,
                    lastLog?.Timestamp,
                    diagnostic.ErrorMessage,
                    diagnostic.RequestHeaders,
                    diagnostic.RequestPayload,
                    diagnostic.ResponseStatusCode,
                    diagnostic.ResponseBody);
            })
            .AsEnumerable();

        if (filter != null)
        {
            if (!string.IsNullOrWhiteSpace(filter.MobileNumber))
                result = result.Where(x => x.MobileNumber.Contains(filter.MobileNumber, StringComparison.OrdinalIgnoreCase));

            if (filter.Status.HasValue)
                result = result.Where(x => x.Status == filter.Status.Value);
        }

        return Task.FromResult(ContactReportResult.Success(result.ToArray()));
    }

    private IQueryable<Campaign> ScopedCampaigns()
    {
        var campaigns = _unitOfWork.Repository<Campaign>().Query();
        if (!string.Equals(_currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            campaigns = campaigns.Where(campaign => campaign.ClientId == _currentUser.ClientId);
        }

        return campaigns;
    }

    private sealed record ApiLogDiagnostic(
        string? ErrorMessage,
        string? RequestHeaders,
        string? RequestPayload,
        string? ResponseStatusCode,
        string? ResponseBody)
    {
        public static ApiLogDiagnostic FromLog(MessageLog? log)
        {
            if (log is null || string.IsNullOrWhiteSpace(log.Response))
            {
                return new(null, null, null, null, null);
            }

            try
            {
                using var document = JsonDocument.Parse(log.Response);
                var root = document.RootElement;
                return new(
                    Read(root, "errorMessage") ?? log.Response,
                    Read(root, "requestHeaders"),
                    Read(root, "requestPayload"),
                    Read(root, "responseStatusCode"),
                    Read(root, "responseBody"));
            }
            catch (JsonException)
            {
                return new(log.Response, null, null, null, log.Response);
            }
        }

        private static string? Read(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out var value)
                ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText()
                : null;
        }
    }
}
