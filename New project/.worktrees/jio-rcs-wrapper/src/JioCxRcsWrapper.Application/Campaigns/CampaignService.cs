using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Application.JioCx;
using JioCxRcsWrapper.Application.Clients;
using JioCxRcsWrapper.Application.Security;
using JioCxRcsWrapper.Domain.Common;
using JioCxRcsWrapper.Domain.Entities;
using JioCxRcsWrapper.Domain.Enums;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace JioCxRcsWrapper.Application.Campaigns;

public sealed class CampaignService : ICampaignService
{
    private static readonly Regex SupportedPhoneNumber = new(@"^\+[1-9][0-9]{7,14}$", RegexOptions.Compiled);
    private static readonly ContactStatus[] CompletedContactStatuses =
    [
        ContactStatus.Sent,
        ContactStatus.Delivered,
        ContactStatus.Opened,
        ContactStatus.Clicked
    ];

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IContactCsvParser _csvParser;
    private readonly IJioCxClient? _jioCxClient;
    private readonly ISecretProtector? _secretProtector;

    public CampaignService(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        : this(unitOfWork, currentUser, new ContactCsvParser())
    {
    }

    public CampaignService(IUnitOfWork unitOfWork, ICurrentUser currentUser, IContactCsvParser csvParser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _csvParser = csvParser;
    }

    public CampaignService(IUnitOfWork unitOfWork, ICurrentUser currentUser, IContactCsvParser csvParser, IJioCxClient jioCxClient, ISecretProtector secretProtector)
        : this(unitOfWork, currentUser, csvParser)
    {
        _jioCxClient = jioCxClient;
        _secretProtector = secretProtector;
    }

    public Task<IReadOnlyList<CampaignSummary>> ListAsync(CampaignFilter? filter = null, CancellationToken cancellationToken = default)
    {
        var campaigns = _unitOfWork.Repository<Campaign>().Query();
        if (!string.Equals(_currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            campaigns = campaigns.Where(campaign => campaign.ClientId == _currentUser.ClientId);
        }

        var contactCounts = _unitOfWork.Repository<Contact>().Query()
            .GroupBy(contact => contact.CampaignId)
            .ToDictionary(group => group.Key, group => group.Count());
        var completedCounts = _unitOfWork.Repository<Contact>().Query()
            .Where(contact => CompletedContactStatuses.Contains(contact.Status))
            .GroupBy(contact => contact.CampaignId)
            .ToDictionary(group => group.Key, group => group.Count());
        var failedCounts = _unitOfWork.Repository<Contact>().Query()
            .Where(contact => contact.Status == ContactStatus.Failed)
            .GroupBy(contact => contact.CampaignId)
            .ToDictionary(group => group.Key, group => group.Count());
        var clientNames = _unitOfWork.Repository<Client>().Query()
            .ToDictionary(client => client.Id, client => client.BrandName);

        var campaignList = campaigns
            .OrderByDescending(campaign => campaign.CreatedAt)
            .ToArray();

        var result = campaignList
            .Select(campaign => new CampaignSummary(
                campaign.Id,
                campaign.Name,
                campaign.ClientId,
                clientNames.TryGetValue(campaign.ClientId, out var clientName) ? clientName : "-",
                campaign.Type,
                campaign.Status,
                campaign.ScheduledAt,
                contactCounts.TryGetValue(campaign.Id, out var count) ? count : 0,
                completedCounts.TryGetValue(campaign.Id, out var completed) ? completed : 0,
                failedCounts.TryGetValue(campaign.Id, out var failed) ? failed : 0,
                campaign.Status == CampaignStatus.Paused))
            .AsEnumerable();

        if (filter != null)
        {
            if (!string.IsNullOrWhiteSpace(filter.Name))
                result = result.Where(x => x.Name.Contains(filter.Name, StringComparison.OrdinalIgnoreCase));
            
            if (!string.IsNullOrWhiteSpace(filter.ClientName))
                result = result.Where(x => x.ClientName.Contains(filter.ClientName, StringComparison.OrdinalIgnoreCase));

            if (filter.Type.HasValue)
                result = result.Where(x => x.Type == filter.Type.Value);

            if (filter.Status.HasValue)
                result = result.Where(x => x.Status == filter.Status.Value);
        }

        return Task.FromResult<IReadOnlyList<CampaignSummary>>(result.ToArray());
    }

    public Task<IReadOnlyList<ContactSummary>> GetContactsAsync(int campaignId, CancellationToken cancellationToken = default)
    {
        var logs = _unitOfWork.Repository<MessageLog>().Query()
            .Where(l => l.CampaignId == campaignId)
            .OrderByDescending(l => l.Timestamp)
            .AsEnumerable()
            .GroupBy(l => l.ContactId)
            .ToDictionary(g => g.Key, g => g.First().ErrorCode);

        var contacts = _unitOfWork.Repository<Contact>().Query()
            .Where(contact => contact.CampaignId == campaignId)
            .Select(contact => new ContactSummary(contact.Id, contact.MobileNumber, contact.Status, null))
            .ToArray();

        var result = contacts.Select(c => c with { ErrorCode = logs.TryGetValue(c.Id, out var error) ? error : null }).ToArray();
        return Task.FromResult<IReadOnlyList<ContactSummary>>(result);
    }

    public async Task<CampaignOperationResult> CreateDraftAsync(CreateCampaignRequest request, CancellationToken cancellationToken)
    {
        var scopeError = ValidateClientScope(request.ClientId);
        if (scopeError is not null)
        {
            return CampaignOperationResult.Failed(scopeError);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return CampaignOperationResult.Failed("Campaign name is required.");
        }

        var manualPhoneNumbers = NormalizeManualPhoneNumbers(request.ManualPhoneNumbers);
        var invalidPhoneNumbers = manualPhoneNumbers.Where(phoneNumber => !SupportedPhoneNumber.IsMatch(phoneNumber)).ToArray();
        if (invalidPhoneNumbers.Length > 0)
        {
            return CampaignOperationResult.Failed("Contacts must use the JioCX supported E.164 format, for example +919876543210.");
        }
        if (request.Type == CampaignType.OneTime && request.TemplateId is null)
        {
            return CampaignOperationResult.Failed("Message template is required for one-time campaigns.");
        }

        if (request.Type == CampaignType.OneTime && manualPhoneNumbers.Count == 0)
        {
            return CampaignOperationResult.Failed("At least one phone number is required for one-time campaigns.");
        }

        var campaign = new Campaign
        {
            Name = request.Name.Trim(),
            ClientId = request.ClientId,
            Type = request.Type,
            ScheduledAt = request.ScheduledAt,
            IsRCSEnabled = request.IsRCSEnabled,
            CreatedBy = _currentUser.UserId,
            Status = CampaignStatus.Draft
        };

        await _unitOfWork.Repository<Campaign>().AddAsync(campaign, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (request.TemplateId is not null)
        {
            var template = _unitOfWork.Repository<MessageTemplate>().Query().FirstOrDefault(value => value.Id == request.TemplateId.Value);
            if (template is null || (template.ClientId is not null && template.ClientId != request.ClientId))
            {
                return CampaignOperationResult.Failed("Message template not found for the selected client.");
            }

            await _unitOfWork.Repository<CampaignMessage>().AddAsync(new CampaignMessage
            {
                CampaignId = campaign.Id,
                TemplateId = template.Id,
                MessageType = template.MessageType,
                PayloadJson = template.PayloadJson
            }, cancellationToken);
        }

        foreach (var phoneNumber in manualPhoneNumbers)
        {
            await _unitOfWork.Repository<Contact>().AddAsync(new Contact
            {
                CampaignId = campaign.Id,
                MobileNumber = phoneNumber,
                Status = ContactStatus.Pending
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (request.Type == CampaignType.OneTime && manualPhoneNumbers.Count > 0)
        {
            return await QueueCampaignAsync(campaign.Id, cancellationToken);
        }

        return CampaignOperationResult.Success(campaign.Id);
    }

    public async Task<CampaignOperationResult> UploadContactsAsync(int campaignId, string csv, CancellationToken cancellationToken)
    {
        var campaign = await _unitOfWork.Repository<Campaign>().GetByIdAsync(campaignId, cancellationToken);
        if (campaign is null) return CampaignOperationResult.Failed("Campaign not found.");
        if (campaign.Status == CampaignStatus.Paused) return CampaignOperationResult.Failed("Campaign is disabled.");

        var scopeError = ValidateClientScope(campaign.ClientId);
        if (scopeError is not null) return CampaignOperationResult.Failed(scopeError);

        var parsed = _csvParser.Parse(csv);
        if (!parsed.IsValid) return CampaignOperationResult.Failed(parsed.Errors);

        var contactRepository = _unitOfWork.Repository<Contact>();
        var existingMobileNumbers = contactRepository.Query()
            .Where(contact => contact.CampaignId == campaign.Id)
            .Select(contact => contact.MobileNumber)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var addedCount = 0;
        foreach (var mobileNumber in parsed.MobileNumbers.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!existingMobileNumbers.Add(mobileNumber)) continue;

            await contactRepository.AddAsync(new Contact
            {
                CampaignId = campaign.Id,
                MobileNumber = mobileNumber,
                Status = ContactStatus.Pending
            }, cancellationToken);
            addedCount++;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (addedCount > 0 && campaign.Status != CampaignStatus.Paused)
        {
            return await QueueCampaignAsync(campaign.Id, cancellationToken);
        }

        return CampaignOperationResult.Success(campaign.Id);
    }

    public async Task<CampaignOperationResult> QueueCampaignAsync(int campaignId, CancellationToken cancellationToken)
        => await QueueCampaignAsync(campaignId, cancellationToken, null);

    private async Task<CampaignOperationResult> QueueCampaignAsync(int campaignId, CancellationToken cancellationToken, IReadOnlySet<int>? eligibleContactIds)
    {
        var campaign = await _unitOfWork.Repository<Campaign>().GetByIdAsync(campaignId, cancellationToken);
        if (campaign is null) return CampaignOperationResult.Failed("Campaign not found.");
        if (campaign.Status == CampaignStatus.Paused) return CampaignOperationResult.Failed("Campaign is disabled.");

        var scopeError = ValidateClientScope(campaign.ClientId);
        if (scopeError is not null) return CampaignOperationResult.Failed(scopeError);

        var client = await _unitOfWork.Repository<Client>().GetByIdAsync(campaign.ClientId, cancellationToken);
        if (client is null) client = _unitOfWork.Repository<Client>().Query().FirstOrDefault(c => c.Id == campaign.ClientId);
        if (client is null) return CampaignOperationResult.Failed($"Client not found (ID: {campaign.ClientId}).");

        var queueRepository = _unitOfWork.Repository<CampaignQueueItem>();
        var existingQueueItems = queueRepository.Query()
            .Where(item => item.CampaignId == campaign.Id)
            .ToDictionary(item => item.ContactId);
        var failedQueueContactIds = existingQueueItems.Values
            .Where(item => item.Status == CampaignQueueStatus.Failed &&
                (eligibleContactIds == null || eligibleContactIds.Contains(item.ContactId)))
            .Select(item => item.ContactId)
            .ToHashSet();

        var contacts = _unitOfWork.Repository<Contact>().Query()
            .Where(c => c.CampaignId == campaign.Id &&
                (eligibleContactIds == null || eligibleContactIds.Contains(c.Id)) &&
                (c.Status == ContactStatus.Pending || failedQueueContactIds.Contains(c.Id)))
            .ToArray();

        if (contacts.Length == 0) return CampaignOperationResult.Failed("No pending contacts found.");

        var creator = await _unitOfWork.Repository<User>().GetByIdAsync(campaign.CreatedBy, cancellationToken);
        if (creator is null) return CampaignOperationResult.Failed("Campaign creator not found.");

        var isAdmin = creator.RoleId == 1;
        var creditCost = Math.Max(1, client.CreditCostPerMessage);
        
        var availableQueueSlots = int.MaxValue;
        if (!isAdmin)
        {
            var reservedCreditCount = queueRepository.Query()
                .Where(item => item.CampaignId == campaign.Id &&
                    (item.Status == CampaignQueueStatus.Pending ||
                     item.Status == CampaignQueueStatus.Processing ||
                     item.Status == CampaignQueueStatus.RetryScheduled))
                .Count() * creditCost;
            var availableCredits = Math.Min(client.Credits, creator.Credits) - reservedCreditCount;
            availableQueueSlots = Math.Max(0, (int)(availableCredits / creditCost));
        }

        foreach (var failedItem in existingQueueItems.Values.Where(item =>
            item.Status == CampaignQueueStatus.Failed &&
            (eligibleContactIds == null || eligibleContactIds.Contains(item.ContactId))))
        {
            var failedContact = contacts.SingleOrDefault(contact => contact.Id == failedItem.ContactId);
            if (failedContact is null)
            {
                continue;
            }

            failedItem.Status = CampaignQueueStatus.Pending;
            failedItem.AttemptCount = 0;
            failedItem.LastError = null;
            failedItem.NextAttemptAt = DateTimeOffset.UtcNow;
            failedItem.ProcessedAt = null;
            failedContact.Status = ContactStatus.Pending;
            queueRepository.Update(failedItem);
        }

        var contactsToProcess = contacts.Where(contact => !existingQueueItems.ContainsKey(contact.Id)).ToArray();
        
        foreach (var contact in contactsToProcess)
        {
            if (availableQueueSlots <= 0)
            {
                contact.Status = ContactStatus.Failed;
                await _unitOfWork.Repository<MessageLog>().AddAsync(new MessageLog
                {
                    CampaignId = campaign.Id,
                    ContactId = contact.Id,
                    Status = "NoCredits",
                    ErrorCode = "NO_CREDITS",
                    Response = "No credits available, contact support.",
                    Timestamp = DateTimeOffset.UtcNow
                }, cancellationToken);
                continue;
            }

            if (campaign.IsRCSEnabled)
            {
                var capability = await CheckRcsCapabilityAsync(client, contact.MobileNumber, cancellationToken);
                if (!capability.IsCapable)
                {
                    contact.Status = ContactStatus.Failed;
                    await _unitOfWork.Repository<MessageLog>().AddAsync(new MessageLog
                    {
                        CampaignId = campaign.Id,
                        ContactId = contact.Id,
                        Status = "NotRcsCapable",
                        ErrorCode = "RCS_CAPABILITY",
                        Response = capability.DiagnosticJson,
                        Timestamp = DateTimeOffset.UtcNow
                    }, cancellationToken);
                    continue;
                }
            }

            await queueRepository.AddAsync(new CampaignQueueItem
            {
                CampaignId = campaign.Id,
                ContactId = contact.Id,
                Status = CampaignQueueStatus.Pending,
                NextAttemptAt = DateTimeOffset.UtcNow
            }, cancellationToken);
            
            contact.Status = ContactStatus.Pending;
            if (!isAdmin) availableQueueSlots--;
        }

        campaign.Status = CampaignStatus.Queued;
        _unitOfWork.Repository<Campaign>().Update(campaign);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return CampaignOperationResult.Success(campaign.Id);
    }

    public async Task<CampaignOperationResult> RetryFailedAsync(int campaignId, CancellationToken cancellationToken)
    {
        var campaign = await _unitOfWork.Repository<Campaign>().GetByIdAsync(campaignId, cancellationToken);
        if (campaign is null) return CampaignOperationResult.Failed("Campaign not found.");
        if (campaign.Status == CampaignStatus.Paused) return CampaignOperationResult.Failed("Campaign is disabled.");

        var failedContactIds = _unitOfWork.Repository<Contact>().Query()
            .Where(c => c.CampaignId == campaignId && c.Status == ContactStatus.Failed)
            .Select(c => c.Id)
            .ToArray();

        if (failedContactIds.Length == 0)
        {
            failedContactIds = _unitOfWork.Repository<CampaignQueueItem>().Query()
                .Where(item => item.CampaignId == campaignId && item.Status == CampaignQueueStatus.Failed)
                .Select(item => item.ContactId)
                .ToArray();
        }

        if (failedContactIds.Length == 0) return CampaignOperationResult.Failed("No failed contacts found to retry.");

        return await RetryContactsAsync(campaignId, failedContactIds, cancellationToken);
    }

    public async Task<CampaignOperationResult> RetryContactsAsync(int campaignId, int[] contactIds, CancellationToken cancellationToken)
    {
        var campaign = await _unitOfWork.Repository<Campaign>().GetByIdAsync(campaignId, cancellationToken);
        if (campaign is null) return CampaignOperationResult.Failed("Campaign not found.");

        var contactRepository = _unitOfWork.Repository<Contact>();
        var contactsToRetry = contactRepository.Query()
            .Where(c => c.CampaignId == campaignId && contactIds.Contains(c.Id))
            .ToArray();

        if (contactsToRetry.Length == 0) return CampaignOperationResult.Failed("No contacts found to retry.");

        var retryContacts = new List<Contact>();
        foreach (var originalContact in contactsToRetry)
        {
            var retryContact = new Contact
            {
                CampaignId = campaignId,
                MobileNumber = originalContact.MobileNumber,
                Status = ContactStatus.Pending
            };
            await contactRepository.AddAsync(retryContact, cancellationToken);
            retryContacts.Add(retryContact);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return await QueueCampaignAsync(campaignId, cancellationToken, retryContacts.Select(contact => contact.Id).ToHashSet());
    }

    public async Task<CampaignOperationResult> DeleteContactsAsync(int campaignId, int[] contactIds, CancellationToken cancellationToken)
    {
        var campaign = await _unitOfWork.Repository<Campaign>().GetByIdAsync(campaignId, cancellationToken);
        if (campaign is null) return CampaignOperationResult.Failed("Campaign not found.");

        var contactRepository = _unitOfWork.Repository<Contact>();
        var queueRepository = _unitOfWork.Repository<CampaignQueueItem>();
        var logRepository = _unitOfWork.Repository<MessageLog>();

        foreach (var id in contactIds)
        {
            var contact = await contactRepository.GetByIdAsync(id, cancellationToken);
            if (contact == null || contact.CampaignId != campaignId) continue;

            var queueItem = queueRepository.Query().FirstOrDefault(q => q.ContactId == id);
            if (queueItem != null) queueRepository.Remove(queueItem);

            var logs = logRepository.Query().Where(l => l.ContactId == id).ToArray();
            foreach (var log in logs) logRepository.Remove(log);

            contactRepository.Remove(contact);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return CampaignOperationResult.Success(campaign.Id);
    }

    public async Task<CampaignOperationResult> DisableCampaignAsync(int campaignId, CancellationToken cancellationToken)
    {
        var campaign = await _unitOfWork.Repository<Campaign>().GetByIdAsync(campaignId, cancellationToken);
        if (campaign is null) return CampaignOperationResult.Failed("Campaign not found.");

        campaign.Status = CampaignStatus.Paused;
        _unitOfWork.Repository<Campaign>().Update(campaign);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return CampaignOperationResult.Success(campaign.Id);
    }

    public async Task<CampaignOperationResult> DeleteCampaignAsync(int campaignId, CancellationToken cancellationToken)
    {
        var campaign = await _unitOfWork.Repository<Campaign>().GetByIdAsync(campaignId, cancellationToken);
        if (campaign is null) return CampaignOperationResult.Failed("Campaign not found.");

        RemoveRelated<CampaignQueueItem>(item => item.CampaignId == campaignId);
        RemoveRelated<MessageLog>(log => log.CampaignId == campaignId);
        RemoveRelated<Report>(report => report.CampaignId == campaignId);
        RemoveRelated<Contact>(contact => contact.CampaignId == campaignId);
        RemoveRelated<CampaignMessage>(message => message.CampaignId == campaignId);
        _unitOfWork.Repository<Campaign>().Remove(campaign);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return CampaignOperationResult.Success(campaignId);
    }

    private void RemoveRelated<TEntity>(Func<TEntity, bool> predicate) where TEntity : BaseEntity
    {
        var repository = _unitOfWork.Repository<TEntity>();
        var items = repository.Query().Where(predicate).ToArray();
        foreach (var item in items) repository.Remove(item);
    }

    private string? ValidateClientScope(int clientId)
    {
        if (string.Equals(_currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase)) return null;
        return _currentUser.ClientId == clientId ? null : "Client is outside the current user's scope.";
    }

    private bool IsAdmin() => string.Equals(_currentUser.Role, "Admin", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> NormalizeManualPhoneNumbers(IReadOnlyList<string> phoneNumbers)
    {
        return phoneNumbers
            .SelectMany(value => value.Split([',', '\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.StartsWith('+') ? value : $"+{value}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<CapabilityCheckOutcome> CheckRcsCapabilityAsync(Client client, string phoneNumber, CancellationToken cancellationToken)
    {
        if (_jioCxClient is null || _secretProtector is null) return new(true, string.Empty);
        var apiKey = _secretProtector.Unprotect(client.ApiKey);
        var result = await _jioCxClient.CheckCapabilityAsync(apiKey, client.AgentId, phoneNumber, cancellationToken);
        if (result.Succeeded) return new(true, string.Empty);
        return new(false, "Not capable");
    }

    private sealed record CapabilityCheckOutcome(bool IsCapable, string DiagnosticJson);
}
