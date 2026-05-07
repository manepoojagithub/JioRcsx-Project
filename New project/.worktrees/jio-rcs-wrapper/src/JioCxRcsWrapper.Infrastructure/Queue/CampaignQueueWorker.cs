using System.Text.Json;
using JioCxRcsWrapper.Application.Clients;
using JioCxRcsWrapper.Application.Common.Options;
using JioCxRcsWrapper.Application.JioCx;
using JioCxRcsWrapper.Application.Queue;
using JioCxRcsWrapper.Domain.Entities;
using JioCxRcsWrapper.Domain.Enums;
using JioCxRcsWrapper.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JioCxRcsWrapper.Infrastructure.Queue;

public sealed class CampaignQueueWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly QueueOptions _options;
    private readonly ILogger<CampaignQueueWorker> _logger;
    private readonly string _workerId = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    public CampaignQueueWorker(IServiceScopeFactory scopeFactory, IOptions<QueueOptions> options, ILogger<CampaignQueueWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_options.Enabled)
                {
                    await ProcessBatchAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Campaign queue processing failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.PollSeconds)), stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var jiocx = scope.ServiceProvider.GetRequiredService<IJioCxClient>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        var retryPolicy = scope.ServiceProvider.GetRequiredService<IQueueRetryPolicy>();
        var notifier = scope.ServiceProvider.GetRequiredService<IRealtimeNotifier>();
        var now = DateTimeOffset.UtcNow;

        var items = await db.CampaignQueueItems
            .Where(item =>
                item.Status == CampaignQueueStatus.Pending ||
                (item.Status == CampaignQueueStatus.RetryScheduled && item.NextAttemptAt <= now))
            .OrderBy(item => item.CreatedAt)
            .Take(Math.Max(1, _options.BatchSize))
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            item.Status = CampaignQueueStatus.Processing;
            item.LockedAt = now;
            item.LockedBy = _workerId;
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var item in items)
        {
            await ProcessItemAsync(db, jiocx, protector, retryPolicy, notifier, item, cancellationToken);
        }
    }

    private async Task ProcessItemAsync(
        AppDbContext db,
        IJioCxClient jiocx,
        ISecretProtector protector,
        IQueueRetryPolicy retryPolicy,
        IRealtimeNotifier notifier,
        CampaignQueueItem item,
        CancellationToken cancellationToken)
    {
        var campaign = await db.Campaigns.FindAsync([item.CampaignId], cancellationToken);
        var contact = await db.Contacts.FindAsync([item.ContactId], cancellationToken);
        if (campaign is null || contact is null)
        {
            item.Status = CampaignQueueStatus.Failed;
            item.LastError = "Campaign or contact not found.";
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var client = await db.Clients.FindAsync([campaign.ClientId], cancellationToken);
        var message = await db.CampaignMessages.FirstOrDefaultAsync(value => value.CampaignId == campaign.Id, cancellationToken);
        var creator = await db.Users.FindAsync([campaign.CreatedBy], cancellationToken);
        if (client is null || message is null || creator is null)
        {
            item.Status = CampaignQueueStatus.Failed;
            item.LastError = "Client, creator, or message payload not found.";
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var creditCost = Math.Max(1, client.CreditCostPerMessage);
        var isAdmin = creator.RoleId == 1; // 1 is Admin role ID in SeedData

        if (!isAdmin && (client.Credits < creditCost || creator.Credits < creditCost))
        {
            item.Status = CampaignQueueStatus.Failed;
            item.LastError = "No credits available, contact support.";
            contact.Status = ContactStatus.Failed;
            await AppendLogAsync(db, campaign.Id, contact.Id, "NoCredits", "NO_CREDITS", "No credits available, contact support.", cancellationToken);
            await UpdateCampaignStatusAsync(db, campaign, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        item.AttemptCount++;
        JioCxSendResult result;
        object payload;
        var messageId = Guid.NewGuid().ToString("N");
        try
        {
            var data = JsonSerializer.Deserialize<object>(message.PayloadJson) ?? new { };
            payload = new
            {
                messageID = messageId,
                agentID = client.AgentId,
                campaignID = campaign.Id.ToString(),
                contacts = new[] { contact.MobileNumber },
                data,
                data_sms = (object?)null
            };
            result = await jiocx.SendMessageAsync(
                protector.Unprotect(client.ApiKey),
                new JioCxSendRequest(messageId, client.AgentId, campaign.Id.ToString(), [contact.MobileNumber], data),
                cancellationToken);
        }
        catch (Exception ex)
        {
            payload = new { error = "Failed before request payload could be sent." };
            result = new JioCxSendResult(false, 500, ex.Message);
        }

        if (result.Succeeded)
        {
            item.Status = CampaignQueueStatus.Succeeded;
            item.ProcessedAt = DateTimeOffset.UtcNow;
            item.LastError = null;
            contact.Status = ContactStatus.Sent;

            if (!isAdmin)
            {
                var previousBalance = client.Credits;
                client.Credits = Math.Max(0, client.Credits - creditCost);
                creator.Credits = Math.Max(0, creator.Credits - creditCost);
                await db.UserCreditHistories.AddAsync(new UserCreditHistory
                {
                    UserId = creator.Id,
                    Amount = creditCost,
                    PreviousBalance = previousBalance,
                    NewBalance = client.Credits,
                    TransactionType = "Spent",
                    Reason = $"Message sent to {contact.MobileNumber} (Campaign: {campaign.Name})",
                    CreatedAt = DateTimeOffset.UtcNow
                }, cancellationToken);
            }

            await AppendLogAsync(db, campaign.Id, contact.Id, "Successfully Send", null, BuildSendDiagnostic("JioCX sendMessage accepted the request.", client, payload, result), cancellationToken);
            await UpsertReportAsync(db, campaign.Id, cancellationToken);
        }
        else
        {
            item.Status = retryPolicy.GetFailureStatus(result.StatusCode, item.AttemptCount, _options.MaxAttempts);
            item.LastError = result.ResponseJson;
            item.NextAttemptAt = item.Status == CampaignQueueStatus.RetryScheduled
                ? retryPolicy.NextAttemptAt(DateTimeOffset.UtcNow, item.AttemptCount)
                : null;
            contact.Status = item.Status == CampaignQueueStatus.Failed ? ContactStatus.Failed : contact.Status;
            await AppendLogAsync(db, campaign.Id, contact.Id, item.Status.ToString(), result.StatusCode.ToString(), BuildSendDiagnostic($"JioCX sendMessage failed (HTTP {result.StatusCode}).", client, payload, result), cancellationToken);
        }

        await UpdateCampaignStatusAsync(db, campaign, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await notifier.CampaignUpdatedAsync(campaign.Id, client.Id, new { contactId = contact.Id, status = contact.Status.ToString() }, cancellationToken);
        await notifier.DashboardUpdatedAsync(client.Id, new { campaignId = campaign.Id, status = contact.Status.ToString() }, cancellationToken);
    }

    private static async Task AppendLogAsync(AppDbContext db, int campaignId, int contactId, string status, string? errorCode, string response, CancellationToken cancellationToken)
    {
        await db.MessageLogs.AddAsync(new MessageLog
        {
            CampaignId = campaignId,
            ContactId = contactId,
            Status = status,
            ErrorCode = errorCode,
            Response = response,
            Timestamp = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    private static string BuildSendDiagnostic(string message, Client client, object requestPayload, JioCxSendResult result)
    {
        return JsonSerializer.Serialize(new
        {
            errorMessage = message,
            requestHeaders = $"x-apikey: ***MASKED***{Environment.NewLine}Content-Type: application/json",
            requestPayload = JsonSerializer.Serialize(requestPayload),
            responseStatusCode = result.StatusCode.ToString(),
            responseBody = result.ResponseJson
        });
    }

    private static async Task UpdateCampaignStatusAsync(AppDbContext db, Campaign campaign, CancellationToken cancellationToken)
    {
        var queueItems = await db.CampaignQueueItems
            .Where(item => item.CampaignId == campaign.Id)
            .ToListAsync(cancellationToken);

        if (queueItems.Count == 0)
        {
            return;
        }

        if (queueItems.All(item => item.Status == CampaignQueueStatus.Succeeded))
        {
            campaign.Status = CampaignStatus.Completed;
            return;
        }

        if (queueItems.All(item => item.Status is CampaignQueueStatus.Succeeded or CampaignQueueStatus.Failed) &&
            queueItems.Any(item => item.Status == CampaignQueueStatus.Failed))
        {
            campaign.Status = CampaignStatus.Failed;
            return;
        }

        campaign.Status = queueItems.Any(item => item.Status == CampaignQueueStatus.Processing)
            ? CampaignStatus.Processing
            : CampaignStatus.Queued;
    }

    private static async Task UpsertReportAsync(AppDbContext db, int campaignId, CancellationToken cancellationToken)
    {
        var report = await db.Reports.FirstOrDefaultAsync(value => value.CampaignId == campaignId, cancellationToken);
        if (report is null)
        {
            report = new Report { CampaignId = campaignId };
            await db.Reports.AddAsync(report, cancellationToken);
        }

        report.TotalSent = await db.Contacts.CountAsync(contact => contact.CampaignId == campaignId && contact.Status == ContactStatus.Sent, cancellationToken);
        report.Delivered = await db.Contacts.CountAsync(contact => contact.CampaignId == campaignId && contact.Status == ContactStatus.Delivered, cancellationToken);
        report.Failed = await db.Contacts.CountAsync(contact => contact.CampaignId == campaignId && contact.Status == ContactStatus.Failed, cancellationToken);
    }
}
