using FluentAssertions;
using JioCxRcsWrapper.Domain.Entities;
using JioCxRcsWrapper.Domain.Enums;
using JioCxRcsWrapper.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JioCxRcsWrapper.IntegrationTests.Data;

public sealed class AppDbContextTests
{
    [Fact]
    public async Task CanCreateClientCampaignContactAndQueueItem()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var client = new Client
        {
            BrandName = "Brand",
            AgentName = "Agent",
            AgentId = "agent-1",
            ApiKey = "encrypted",
            SiteName = "Site",
            CreatedBy = 1
        };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var campaign = new Campaign
        {
            Name = "Test",
            ClientId = client.Id,
            CreatedBy = 1,
            Type = CampaignType.Schedule
        };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();

        var contact = new Contact { CampaignId = campaign.Id, MobileNumber = "+918000000000" };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        db.CampaignQueueItems.Add(new CampaignQueueItem { CampaignId = campaign.Id, ContactId = contact.Id });
        await db.SaveChangesAsync();

        (await db.CampaignQueueItems.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AllowsMultipleClientsWithSameAgentId()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.Clients.AddRange(
            new Client
            {
                BrandName = "Brand One",
                AgentName = "Agent One",
                AgentId = "agent-1",
                ApiKey = "encrypted",
                SiteName = "Site One",
                CreatedBy = 1
            },
            new Client
            {
                BrandName = "Brand Two",
                AgentName = "Agent Two",
                AgentId = "agent-1",
                ApiKey = "encrypted",
                SiteName = "Site Two",
                CreatedBy = 1
            });

        await db.SaveChangesAsync();

        (await db.Clients.CountAsync(x => x.AgentId == "agent-1")).Should().Be(2);
    }
}
