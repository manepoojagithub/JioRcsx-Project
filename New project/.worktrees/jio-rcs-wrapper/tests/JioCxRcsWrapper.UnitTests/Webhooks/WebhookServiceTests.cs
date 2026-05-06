using System.Linq.Expressions;
using FluentAssertions;
using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Application.Queue;
using JioCxRcsWrapper.Application.Webhooks;
using JioCxRcsWrapper.Domain.Common;
using JioCxRcsWrapper.Domain.Entities;
using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.UnitTests.Webhooks;

public sealed class WebhookServiceTests
{
    [Fact]
    public async Task Webhook_AlwaysStoresRawPayload()
    {
        var harness = new WebhookHarness();

        await harness.Service.ProcessAsync("""{"unknown":true}""", CancellationToken.None);

        harness.Events.Should().ContainSingle();
        harness.Events[0].PayloadJson.Should().Be("""{"unknown":true}""");
    }

    [Fact]
    public async Task Webhook_WithKnownContactId_UpdatesMatchingLog()
    {
        var harness = new WebhookHarness();
        var campaign = harness.AddCampaign(clientId: 10);
        var contact = harness.AddContact(campaign.Id, "+918000000000");

        await harness.Service.ProcessAsync($$"""{"campaignId":{{campaign.Id}},"contactId":{{contact.Id}},"status":"Delivered"}""", CancellationToken.None);

        contact.Status.Should().Be(ContactStatus.Delivered);
        harness.Logs.Should().ContainSingle(log => log.Status == "Delivered" && log.ContactId == contact.Id);
    }

    [Fact]
    public async Task Webhook_WithUnknownShape_DoesNotThrow()
    {
        var harness = new WebhookHarness();

        var act = () => harness.Service.ProcessAsync("""{"nested":{"value":1}}""", CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Webhook_WithClickEvent_BroadcastsWhenMapped()
    {
        var harness = new WebhookHarness();
        var campaign = harness.AddCampaign(clientId: 10);
        var contact = harness.AddContact(campaign.Id, "+918000000000");

        await harness.Service.ProcessAsync($$"""{"campaignId":{{campaign.Id}},"contactId":{{contact.Id}},"eventType":"Clicked"}""", CancellationToken.None);

        contact.Status.Should().Be(ContactStatus.Clicked);
        harness.Notifier.CampaignUpdates.Should().Be(1);
    }

    [Fact]
    public async Task Webhook_WithNestedEventShape_MapsDocumentedStatusFields()
    {
        var harness = new WebhookHarness();
        var campaign = harness.AddCampaign(clientId: 10);
        var contact = harness.AddContact(campaign.Id, "+918000000000");

        await harness.Service.ProcessAsync($$"""
            {
              "messageID": "message-1",
              "event": {
                "campaignId": {{campaign.Id}},
                "contactId": {{contact.Id}},
                "eventType": "opened"
              }
            }
            """, CancellationToken.None);

        contact.Status.Should().Be(ContactStatus.Opened);
        harness.Events.Should().ContainSingle(value => value.MessageId == "message-1" && value.EventType == "opened");
    }

    private sealed class WebhookHarness
    {
        private readonly FakeUnitOfWork _unitOfWork = new();

        public WebhookHarness()
        {
            Notifier = new FakeRealtimeNotifier();
            Service = new WebhookService(_unitOfWork, Notifier);
        }

        public WebhookService Service { get; }

        public FakeRealtimeNotifier Notifier { get; }

        public IReadOnlyList<WebhookEvent> Events => _unitOfWork.Set<WebhookEvent>();

        public IReadOnlyList<MessageLog> Logs => _unitOfWork.Set<MessageLog>();

        public Campaign AddCampaign(int clientId)
        {
            var campaign = new Campaign { Id = _unitOfWork.NextId<Campaign>(), ClientId = clientId, Name = "Campaign", Type = CampaignType.Schedule, CreatedBy = 1 };
            _unitOfWork.AddSeed(campaign);
            return campaign;
        }

        public Contact AddContact(int campaignId, string mobileNumber)
        {
            var contact = new Contact { Id = _unitOfWork.NextId<Contact>(), CampaignId = campaignId, MobileNumber = mobileNumber };
            _unitOfWork.AddSeed(contact);
            return contact;
        }
    }

    private sealed class FakeRealtimeNotifier : IRealtimeNotifier
    {
        public int CampaignUpdates { get; private set; }

        public Task CampaignUpdatedAsync(int campaignId, int clientId, object payload, CancellationToken cancellationToken)
        {
            CampaignUpdates++;
            return Task.CompletedTask;
        }

        public Task DashboardUpdatedAsync(int clientId, object payload, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        private readonly Dictionary<Type, object> _repositories = new();

        public IRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity
        {
            if (!_repositories.TryGetValue(typeof(TEntity), out var repository))
            {
                repository = new FakeRepository<TEntity>();
                _repositories[typeof(TEntity)] = repository;
            }

            return (IRepository<TEntity>)repository;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

        public void AddSeed<TEntity>(TEntity entity) where TEntity : BaseEntity => ((FakeRepository<TEntity>)Repository<TEntity>()).Items.Add(entity);

        public IReadOnlyList<TEntity> Set<TEntity>() where TEntity : BaseEntity => ((FakeRepository<TEntity>)Repository<TEntity>()).Items;

        public int NextId<TEntity>() where TEntity : BaseEntity => Set<TEntity>().Count + 1;
    }

    private sealed class FakeRepository<TEntity> : IRepository<TEntity> where TEntity : BaseEntity
    {
        public List<TEntity> Items { get; } = [];

        public IQueryable<TEntity> Query() => Items.AsQueryable();

        public Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(Items.SingleOrDefault(item => item.Id == id));

        public Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TEntity>>(Items.AsQueryable().Where(predicate).ToArray());

        public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            entity.Id = entity.Id == 0 ? Items.Count + 1 : entity.Id;
            Items.Add(entity);
            return Task.CompletedTask;
        }

        public void Update(TEntity entity)
        {
        }

        public void Remove(TEntity entity) => Items.Remove(entity);
    }
}
