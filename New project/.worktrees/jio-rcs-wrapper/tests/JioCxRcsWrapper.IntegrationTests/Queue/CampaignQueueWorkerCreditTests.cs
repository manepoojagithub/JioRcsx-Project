using FluentAssertions;
using JioCxRcsWrapper.Application.Clients;
using JioCxRcsWrapper.Application.Common.Options;
using JioCxRcsWrapper.Application.JioCx;
using JioCxRcsWrapper.Application.Queue;
using JioCxRcsWrapper.Domain.Entities;
using JioCxRcsWrapper.Domain.Enums;
using JioCxRcsWrapper.Infrastructure.Data;
using JioCxRcsWrapper.Infrastructure.Queue;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JioCxRcsWrapper.IntegrationTests.Queue;

public sealed class CampaignQueueWorkerCreditTests
{
    [Fact]
    public async Task SuccessfulAdminCreatedCampaignDebitsClientCredits()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        services.AddLogging();
        services.AddSingleton<IJioCxClient, SuccessfulJioCxClient>();
        services.AddSingleton<IQueueRetryPolicy, QueueRetryPolicy>();
        services.AddSingleton<IRealtimeNotifier, NoopRealtimeNotifier>();
        services.AddSingleton<ISecretProtector, PassThroughSecretProtector>();

        await using var provider = services.BuildServiceProvider();
        await SeedPendingCampaignAsync(provider);

        var worker = new CampaignQueueWorker(
            provider,
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<CampaignQueueWorker>>(),
            Options.Create(new QueueOptions { BatchSize = 1, MaxAttempts = 1, PollSeconds = 1 }));

        await using var processingScope = provider.CreateAsyncScope();
        var processingDb = processingScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var queueItem = await processingDb.CampaignQueueItems.SingleAsync();
        var processItem = typeof(CampaignQueueWorker)
            .GetMethod("ProcessItemAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        processItem.Should().NotBeNull();

        var processTask = (Task)processItem!.Invoke(worker, [
            processingDb,
            processingScope.ServiceProvider.GetRequiredService<IJioCxClient>(),
            processingScope.ServiceProvider.GetRequiredService<ISecretProtector>(),
            processingScope.ServiceProvider.GetRequiredService<IQueueRetryPolicy>(),
            processingScope.ServiceProvider.GetRequiredService<IRealtimeNotifier>(),
            queueItem,
            CancellationToken.None
        ])!;
        await processTask;

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = await db.Clients.AsNoTracking().SingleAsync(value => value.BrandName == "Credit Test Brand");

        client.Credits.Should().Be(3);
        var manager = await db.Users.AsNoTracking().SingleAsync(value => value.Email == "manager-credit-test@example.com");
        var spentHistoryCount = await db.UserCreditHistories.AsNoTracking()
            .CountAsync(value => value.UserId == manager.Id && value.TransactionType == "Spent");
        spentHistoryCount.Should().Be(1);
    }

    [Fact]
    public async Task AdminCreatedCampaignDoesNotFailWhenClientCreditsAreBelowMessageCost()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        services.AddLogging();
        services.AddSingleton<IJioCxClient, SuccessfulJioCxClient>();
        services.AddSingleton<IQueueRetryPolicy, QueueRetryPolicy>();
        services.AddSingleton<IRealtimeNotifier, NoopRealtimeNotifier>();
        services.AddSingleton<ISecretProtector, PassThroughSecretProtector>();

        await using var provider = services.BuildServiceProvider();
        await SeedPendingCampaignAsync(provider, credits: 0, creditCostPerMessage: 2);

        var worker = new CampaignQueueWorker(
            provider,
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<CampaignQueueWorker>>(),
            Options.Create(new QueueOptions { BatchSize = 1, MaxAttempts = 1, PollSeconds = 1 }));

        await ProcessOnlyQueueItemAsync(provider, worker);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var queueItem = await db.CampaignQueueItems.AsNoTracking().SingleAsync();
        var contact = await db.Contacts.AsNoTracking().SingleAsync();

        queueItem.Status.Should().Be(CampaignQueueStatus.Succeeded);
        contact.Status.Should().Be(ContactStatus.Sent);
    }

    private static async Task ProcessOnlyQueueItemAsync(ServiceProvider provider, CampaignQueueWorker worker)
    {
        await using var processingScope = provider.CreateAsyncScope();
        var processingDb = processingScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var queueItem = await processingDb.CampaignQueueItems.SingleAsync();
        var processItem = typeof(CampaignQueueWorker)
            .GetMethod("ProcessItemAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        processItem.Should().NotBeNull();

        var processTask = (Task)processItem!.Invoke(worker, [
            processingDb,
            processingScope.ServiceProvider.GetRequiredService<IJioCxClient>(),
            processingScope.ServiceProvider.GetRequiredService<ISecretProtector>(),
            processingScope.ServiceProvider.GetRequiredService<IQueueRetryPolicy>(),
            processingScope.ServiceProvider.GetRequiredService<IRealtimeNotifier>(),
            queueItem,
            CancellationToken.None
        ])!;
        await processTask;
    }

    private static async Task SeedPendingCampaignAsync(ServiceProvider provider, int credits = 5, int creditCostPerMessage = 2)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var client = new Client
        {
            BrandName = "Credit Test Brand",
            AgentName = "Credit Test Agent",
            AgentId = "agent-credit-test",
            ApiKey = "plain-api-key",
            SiteName = "Credit Test Site",
            Credits = credits,
            CreditCostPerMessage = creditCostPerMessage,
            CreatedBy = 1
        };

        db.Clients.Add(client);
        await db.SaveChangesAsync();

        db.Users.Add(new User
        {
            Name = "Credit Test Manager",
            Email = "manager-credit-test@example.com",
            PasswordHash = "hash",
            RoleId = 2,
            ClientId = client.Id,
            IsActive = true,
            Credits = client.Credits
        });
        await db.SaveChangesAsync();

        var campaign = new Campaign
        {
            Name = "Credit debit campaign",
            ClientId = client.Id,
            CreatedBy = 1,
            Type = CampaignType.OneTime,
            Status = CampaignStatus.Queued
        };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();

        var contact = new Contact
        {
            CampaignId = campaign.Id,
            MobileNumber = "+918000000001",
            Status = ContactStatus.Pending
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        db.CampaignMessages.Add(new CampaignMessage
        {
            CampaignId = campaign.Id,
            MessageType = MessageType.PlainText,
            PayloadJson = """{"content":{"plainText":"Hello"}}"""
        });

        db.CampaignQueueItems.Add(new CampaignQueueItem
        {
            CampaignId = campaign.Id,
            ContactId = contact.Id,
            Status = CampaignQueueStatus.Pending
        });

        await db.SaveChangesAsync();
    }

    private sealed class SuccessfulJioCxClient : IJioCxClient
    {
        public Task<JioCxUploadResult> UploadFileAsync(string apiKey, string agentId, Stream file, string fileName, string contentType, CancellationToken cancellationToken)
            => Task.FromResult(new JioCxUploadResult(true, 200, "{}", "https://example.test/file"));

        public Task<JioCxSendResult> SendMessageAsync(string apiKey, JioCxSendRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new JioCxSendResult(true, 200, """{"status":"accepted"}"""));

        public Task<JioCxCapabilityResult> CheckCapabilityAsync(string apiKey, string agentId, IReadOnlyList<string> phoneNumbers, CancellationToken cancellationToken)
            => Task.FromResult(new JioCxCapabilityResult(true, 200, "{}"));

        public Task<JioCxCapabilityResult> CheckCapabilityAsync(string apiKey, string agentId, string phoneNumber, CancellationToken cancellationToken)
            => Task.FromResult(new JioCxCapabilityResult(true, 200, "{}"));
    }

    private sealed class NoopRealtimeNotifier : IRealtimeNotifier
    {
        public Task CampaignUpdatedAsync(int campaignId, int clientId, object payload, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task DashboardUpdatedAsync(int clientId, object payload, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class PassThroughSecretProtector : ISecretProtector
    {
        public string Protect(string value) => value;

        public string Unprotect(string protectedValue) => protectedValue;
    }
}
