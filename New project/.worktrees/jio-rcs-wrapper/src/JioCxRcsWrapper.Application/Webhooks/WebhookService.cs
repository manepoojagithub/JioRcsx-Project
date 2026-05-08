using System.Text.Json;
using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Application.Queue;
using JioCxRcsWrapper.Domain.Entities;
using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.Application.Webhooks;

public sealed class WebhookService : IWebhookService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRealtimeNotifier _notifier;

    public WebhookService(IUnitOfWork unitOfWork, IRealtimeNotifier notifier)
    {
        _unitOfWork = unitOfWork;
        _notifier = notifier;
    }

    public async Task ProcessAsync(string rawJson, CancellationToken cancellationToken)
    {
        var mapped = TryMap(rawJson);
        
        if (mapped.CampaignId.HasValue)
        {
            var campaign = await _unitOfWork.Repository<Campaign>().GetByIdAsync(mapped.CampaignId.Value, cancellationToken);
            if (campaign != null)
            {
                var client = await _unitOfWork.Repository<Client>().GetByIdAsync(campaign.ClientId, cancellationToken);
                if (client != null && client.WebhookAuditEnabled)
                {
                    var webhookEvent = new WebhookEvent
                    {
                        CampaignId = mapped.CampaignId,
                        ContactId = mapped.ContactId,
                        MessageId = mapped.MessageId,
                        EventType = mapped.EventType ?? mapped.Status ?? "unknown",
                        PayloadJson = rawJson,
                        ReceivedAt = DateTimeOffset.UtcNow
                    };

                    await _unitOfWork.Repository<WebhookEvent>().AddAsync(webhookEvent, cancellationToken);
                }
            }
        }

        if (mapped.CampaignId is not null && mapped.ContactId is not null)
        {
            await ApplyMappedStatusAsync(mapped, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyMappedStatusAsync(WebhookMap mapped, CancellationToken cancellationToken)
    {
        var campaign = await _unitOfWork.Repository<Campaign>().GetByIdAsync(mapped.CampaignId!.Value, cancellationToken);
        var contact = await _unitOfWork.Repository<Contact>().GetByIdAsync(mapped.ContactId!.Value, cancellationToken);
        if (campaign is null || contact is null || contact.CampaignId != campaign.Id)
        {
            return;
        }

        var status = NormalizeStatus(mapped.EventType ?? mapped.Status);
        if (status is null)
        {
            return;
        }

        contact.Status = status.Value;
        _unitOfWork.Repository<Contact>().Update(contact);

        await _unitOfWork.Repository<MessageLog>().AddAsync(new MessageLog
        {
            CampaignId = campaign.Id,
            ContactId = contact.Id,
            Status = status.Value.ToString(),
            Response = mapped.RawJson,
            Timestamp = DateTimeOffset.UtcNow
        }, cancellationToken);

        await _notifier.CampaignUpdatedAsync(campaign.Id, campaign.ClientId, new { contactId = contact.Id, status = status.Value.ToString() }, cancellationToken);
        await _notifier.DashboardUpdatedAsync(campaign.ClientId, new { campaignId = campaign.Id, status = status.Value.ToString() }, cancellationToken);
    }

    private static WebhookMap TryMap(string rawJson)
    {
        try
        {
            using var document = JsonDocument.Parse(rawJson);
            var root = document.RootElement;
            
            // JioCX might wrap the event in an "event" or "data" object, or send it at root.
            // We search multiple potential containers for the IDs.
            var searchContainers = new List<JsonElement> { root };
            if (root.TryGetProperty("event", out var eventObj) && eventObj.ValueKind == JsonValueKind.Object) searchContainers.Add(eventObj);
            if (root.TryGetProperty("data", out var dataObj) && dataObj.ValueKind == JsonValueKind.Object) searchContainers.Add(dataObj);

            int? campaignId = null;
            int? contactId = null;

            foreach (var container in searchContainers)
            {
                campaignId ??= TryGetInt(container, "campaignId") ?? TryGetInt(container, "campaignID");
                contactId ??= TryGetInt(container, "contactId") ?? TryGetInt(container, "contactID");
                if (campaignId.HasValue && contactId.HasValue) break;
            }

            return new WebhookMap(
                campaignId,
                contactId,
                TryGetString(root, "messageId") ?? TryGetString(root, "messageID"),
                TryGetString(root, "eventType") ?? TryGetString(root, "event_type"),
                TryGetString(root, "status") ?? TryGetString(root, "messageStatus") ?? TryGetString(root, "status"),
                rawJson);
        }
        catch (JsonException)
        {
            return new WebhookMap(null, null, null, null, null, rawJson);
        }
    }

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? TryGetInt(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var intValue))
        {
            return intValue;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out intValue))
        {
            return intValue;
        }

        return null;
    }

    private static ContactStatus? NormalizeStatus(string? status)
    {
        return status?.Trim().ToLowerInvariant() switch
        {
            "sent" => ContactStatus.Sent,
            "delivered" => ContactStatus.Delivered,
            "failed" => ContactStatus.Failed,
            "opened" or "open" => ContactStatus.Opened,
            "clicked" or "click" => ContactStatus.Clicked,
            _ => null
        };
    }

    private sealed record WebhookMap(int? CampaignId, int? ContactId, string? MessageId, string? EventType, string? Status, string RawJson);
}
