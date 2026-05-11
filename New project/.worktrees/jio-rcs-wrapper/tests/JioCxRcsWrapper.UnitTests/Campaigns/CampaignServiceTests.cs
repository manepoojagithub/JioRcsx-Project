using System.Linq.Expressions;
using FluentAssertions;
using JioCxRcsWrapper.Application.Campaigns;
using JioCxRcsWrapper.Application.Clients;
using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Application.JioCx;
using JioCxRcsWrapper.Application.Security;
using JioCxRcsWrapper.Domain.Common;
using JioCxRcsWrapper.Domain.Entities;
using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.UnitTests.Campaigns;

public sealed class CampaignServiceTests
{
    [Fact]
    public async Task UploadContacts_WithMoreThan50Rows_IsAccepted()
    {
        var harness = CampaignHarness.CreateManager(clientId: 10);
        harness.AddClient(10);
        var campaign = harness.AddCampaign(10);
        var csv = "MobileNumber\r\n" + string.Join("\r\n", Enumerable.Range(1, 51).Select(index => $"+91800000{index:000}"));

        var result = await harness.Service.UploadContactsAsync(campaign.Id, csv, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        harness.Contacts.Where(contact => contact.CampaignId == campaign.Id).Should().HaveCount(51);
    }

    [Fact]
    public async Task UploadContacts_WithInvalidMobile_IsRejected()
    {
        var harness = CampaignHarness.CreateManager(clientId: 10);
        var campaign = harness.AddCampaign(10);

        var result = await harness.Service.UploadContactsAsync(campaign.Id, "MobileNumber\r\nnot-a-number", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Invalid mobile number: not-a-number");
    }

    [Fact]
    public async Task QueueCampaign_CreatesOneQueueItemPerContact()
    {
        var harness = CampaignHarness.CreateManager(clientId: 10);
        harness.AddClient(10);
        var campaign = harness.AddCampaign(10);
        var first = harness.AddContact(campaign.Id, "+918000000000");
        var second = harness.AddContact(campaign.Id, "+918000000001");

        var result = await harness.Service.QueueCampaignAsync(campaign.Id, CancellationToken.None);
        await harness.Service.QueueCampaignAsync(campaign.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        harness.QueueItems.Should().HaveCount(2);
        harness.QueueItems.Select(item => item.ContactId).Should().BeEquivalentTo([first.Id, second.Id]);
    }

    [Fact]
    public async Task UploadContacts_SkipsDuplicateNumbersAlreadyInCampaign()
    {
        var harness = CampaignHarness.CreateManager(clientId: 10);
        harness.AddClient(10);
        var campaign = harness.AddCampaign(10);
        harness.AddContact(campaign.Id, "+918000000000");

        var result = await harness.Service.UploadContactsAsync(
            campaign.Id,
            "MobileNumber\r\n+918000000000\r\n+918000000001\r\n+918000000001",
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        harness.Contacts.Where(contact => contact.CampaignId == campaign.Id)
            .Select(contact => contact.MobileNumber)
            .Should().BeEquivalentTo(["+918000000000", "+918000000001"]);
    }

    [Fact]
    public async Task QueueCampaign_RequeuesOnlyFailedQueueItems()
    {
        var harness = CampaignHarness.CreateManager(clientId: 10);
        harness.AddClient(10);
        var campaign = harness.AddCampaign(10);
        var succeededContact = harness.AddContact(campaign.Id, "+918000000000");
        var failedContact = harness.AddContact(campaign.Id, "+918000000001");
        harness.AddQueueItem(campaign.Id, succeededContact.Id, CampaignQueueStatus.Succeeded);
        var failedItem = harness.AddQueueItem(campaign.Id, failedContact.Id, CampaignQueueStatus.Failed);
        succeededContact.Status = ContactStatus.Sent;
        failedContact.Status = ContactStatus.Failed;

        var result = await harness.Service.QueueCampaignAsync(campaign.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        harness.QueueItems.Should().HaveCount(2);
        failedItem.Status.Should().Be(CampaignQueueStatus.Pending);
        failedItem.AttemptCount.Should().Be(0);
        failedContact.Status.Should().Be(ContactStatus.Pending);
        harness.QueueItems.Single(item => item.ContactId == succeededContact.Id).Status.Should().Be(CampaignQueueStatus.Succeeded);
    }

    [Fact]
    public async Task RetryContacts_QueuesOnlySelectedContacts()
    {
        var harness = CampaignHarness.CreateManager(clientId: 10);
        harness.AddClient(10);
        var campaign = harness.AddCampaign(10, isRcsEnabled: false);
        var selectedContact = harness.AddContact(campaign.Id, "+918000000000");
        var unselectedContact = harness.AddContact(campaign.Id, "+918000000001");
        var selectedFailedItem = harness.AddQueueItem(campaign.Id, selectedContact.Id, CampaignQueueStatus.Failed);
        var unselectedFailedItem = harness.AddQueueItem(campaign.Id, unselectedContact.Id, CampaignQueueStatus.Failed);
        selectedContact.Status = ContactStatus.Failed;
        unselectedContact.Status = ContactStatus.Failed;

        var result = await harness.Service.RetryContactsAsync(campaign.Id, [selectedContact.Id], CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        selectedFailedItem.Status.Should().Be(CampaignQueueStatus.Failed);
        unselectedFailedItem.Status.Should().Be(CampaignQueueStatus.Failed);
        harness.QueueItems.Should().ContainSingle(item =>
            item.Status == CampaignQueueStatus.Pending &&
            item.ContactId != selectedContact.Id &&
            item.ContactId != unselectedContact.Id);
        harness.Contacts.Where(contact => contact.MobileNumber == selectedContact.MobileNumber).Should().HaveCount(2);
        harness.Contacts.Where(contact => contact.MobileNumber == unselectedContact.MobileNumber).Should().ContainSingle();
    }

    [Fact]
    public async Task QueueCampaign_WhenCampaignIsPaused_IsRejected()
    {
        var harness = CampaignHarness.CreateManager(clientId: 10);
        harness.AddClient(10);
        var campaign = harness.AddCampaign(10);
        campaign.Status = CampaignStatus.Paused;
        harness.AddContact(campaign.Id, "+918000000000");

        var result = await harness.Service.QueueCampaignAsync(campaign.Id, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Campaign is disabled.");
        harness.QueueItems.Should().BeEmpty();
    }

    [Fact]
    public async Task UploadContacts_WhenCampaignIsPaused_IsRejected()
    {
        var harness = CampaignHarness.CreateManager(clientId: 10);
        var campaign = harness.AddCampaign(10);
        campaign.Status = CampaignStatus.Paused;

        var result = await harness.Service.UploadContactsAsync(campaign.Id, "MobileNumber\r\n+918000000000", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Campaign is disabled.");
    }

    [Fact]
    public async Task QueueCampaign_WhenIsRcsEnabledUnchecked_SkipsCapabilityVerification()
    {
        var jioCxClient = new FakeJioCxClient(new JioCxCapabilityResult(false, 403, "Forbidden"));
        var harness = CampaignHarness.CreateManager(clientId: 10, jioCxClient);
        harness.AddClient(10);
        var campaign = harness.AddCampaign(10, isRcsEnabled: false);
        var contact = harness.AddContact(campaign.Id, "+918000000000");

        var result = await harness.Service.QueueCampaignAsync(campaign.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        jioCxClient.CapabilityCallCount.Should().Be(0);
        harness.QueueItems.Should().ContainSingle(item => item.ContactId == contact.Id);
        contact.Status.Should().NotBe(ContactStatus.Failed);
    }

    [Fact]
    public async Task QueueCampaign_WhenIsRcsEnabledChecked_FailsNonCapableContact()
    {
        var jioCxClient = new FakeJioCxClient(new JioCxCapabilityResult(false, 403, "Forbidden"));
        var harness = CampaignHarness.CreateManager(clientId: 10, jioCxClient);
        harness.AddClient(10);
        var campaign = harness.AddCampaign(10, isRcsEnabled: true);
        var contact = harness.AddContact(campaign.Id, "+918000000000");

        var result = await harness.Service.QueueCampaignAsync(campaign.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        jioCxClient.CapabilityCallCount.Should().Be(1);
        harness.QueueItems.Should().BeEmpty();
        contact.Status.Should().Be(ContactStatus.Failed);
        harness.MessageLogs.Should().ContainSingle(log => log.ErrorCode == "RCS_CAPABILITY");
    }

    [Fact]
    public async Task Manager_CanOnlyCreateCampaignForOwnClient()
    {
        var harness = CampaignHarness.CreateManager(clientId: 10);

        var result = await harness.Service.CreateDraftAsync(
            new CreateCampaignRequest("Wrong Tenant", 11, CampaignType.Schedule, null, null, []),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Client is outside the current user's scope.");
    }

    [Fact]
    public async Task CreateOneTime_WithInvalidManualPhone_IsRejected()
    {
        var harness = CampaignHarness.CreateManager(clientId: 10);

        var result = await harness.Service.CreateDraftAsync(
            new CreateCampaignRequest("One Time", 10, CampaignType.OneTime, null, 1, ["not-a-number"]),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("Contacts must use the JioCX supported E.164 format, for example +919876543210.");
    }

    [Fact]
    public async Task CreateSchedule_WithMoreThan50ManualPhones_IsAccepted()
    {
        var harness = CampaignHarness.CreateManager(clientId: 10);
        var phones = Enumerable.Range(1, 51).Select(index => $"+91800000{index:000}").ToArray();

        var result = await harness.Service.CreateDraftAsync(
            new CreateCampaignRequest("Scheduled", 10, CampaignType.Schedule, null, null, phones),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task CreateCampaign_AllowsGlobalRichCardTemplateForClient()
    {
        var harness = CampaignHarness.CreateManager(clientId: 10);
        harness.AddClient(10);
        var template = harness.AddTemplate(clientId: null, MessageType.RichCard);

        var result = await harness.Service.CreateDraftAsync(
            new CreateCampaignRequest("One Time", 10, CampaignType.OneTime, null, template.Id, ["+918000000000"]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    private sealed class CampaignHarness
    {
        private readonly FakeUnitOfWork _unitOfWork = new();

        private CampaignHarness(FakeCurrentUser currentUser, FakeJioCxClient? jioCxClient = null)
        {
            _unitOfWork.AddSeed(new User
            {
                Id = currentUser.UserId,
                Name = "Manager",
                Email = "manager@example.com",
                PasswordHash = "hash",
                ClientId = currentUser.ClientId,
                Credits = 100
            });
            Service = jioCxClient is null
                ? new CampaignService(_unitOfWork, currentUser)
                : new CampaignService(_unitOfWork, currentUser, new ContactCsvParser(), jioCxClient, new FakeSecretProtector());
        }

        public CampaignService Service { get; }

        public IReadOnlyList<CampaignQueueItem> QueueItems => _unitOfWork.Set<CampaignQueueItem>();
        public IReadOnlyList<MessageLog> MessageLogs => _unitOfWork.Set<MessageLog>();
        public IReadOnlyList<Contact> Contacts => _unitOfWork.Set<Contact>();

        public static CampaignHarness CreateManager(int clientId, FakeJioCxClient? jioCxClient = null) => new(new FakeCurrentUser(7, "Manager", clientId), jioCxClient);

        public Client AddClient(int id)
        {
            var client = new Client { Id = id, BrandName = "Brand", AgentName = "Agent", AgentId = $"agent-{id}", ApiKey = "secret", SiteName = "Site", CreatedBy = 1, Credits = 100, CreditCostPerMessage = 1, LowCreditThreshold = 10 };
            _unitOfWork.AddSeed(client);
            return client;
        }

        public Campaign AddCampaign(int clientId, bool isRcsEnabled = true)
        {
            var campaign = new Campaign { Id = _unitOfWork.NextId<Campaign>(), Name = "Campaign", ClientId = clientId, CreatedBy = 7, Type = CampaignType.Schedule, IsRCSEnabled = isRcsEnabled };
            _unitOfWork.AddSeed(campaign);
            return campaign;
        }

        public Contact AddContact(int campaignId, string mobileNumber)
        {
            var contact = new Contact { Id = _unitOfWork.NextId<Contact>(), CampaignId = campaignId, MobileNumber = mobileNumber };
            _unitOfWork.AddSeed(contact);
            return contact;
        }

        public CampaignQueueItem AddQueueItem(int campaignId, int contactId, CampaignQueueStatus status)
        {
            var item = new CampaignQueueItem
            {
                Id = _unitOfWork.NextId<CampaignQueueItem>(),
                CampaignId = campaignId,
                ContactId = contactId,
                Status = status,
                AttemptCount = status == CampaignQueueStatus.Failed ? 4 : 1,
                LastError = status == CampaignQueueStatus.Failed ? "Failed" : null,
                ProcessedAt = status is CampaignQueueStatus.Failed or CampaignQueueStatus.Succeeded ? DateTimeOffset.UtcNow : null
            };
            _unitOfWork.AddSeed(item);
            return item;
        }

        public MessageTemplate AddTemplate(int? clientId, MessageType messageType)
        {
            var template = new MessageTemplate
            {
                Id = _unitOfWork.NextId<MessageTemplate>(),
                Name = "Template",
                ClientId = clientId,
                MessageType = messageType,
                PayloadJson = "{\"content\":{\"plainText\":\"Hi\"}}"
            };
            _unitOfWork.AddSeed(template);
            return template;
        }
    }

    private sealed record FakeCurrentUser(int UserId, string Role, int? ClientId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public bool IsDeveloper => false;
    }

    private sealed class FakeSecretProtector : ISecretProtector
    {
        public string Protect(string value) => value;

        public string Unprotect(string protectedValue) => protectedValue;
    }

    private sealed class FakeJioCxClient : IJioCxClient
    {
        private readonly JioCxCapabilityResult _capabilityResult;

        public FakeJioCxClient(JioCxCapabilityResult capabilityResult)
        {
            _capabilityResult = capabilityResult;
        }

        public int CapabilityCallCount { get; private set; }

        public Task<JioCxUploadResult> UploadFileAsync(string apiKey, string agentId, Stream file, string fileName, string contentType, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<JioCxSendResult> SendMessageAsync(string apiKey, JioCxSendRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<JioCxCapabilityResult> CheckCapabilityAsync(string apiKey, string agentId, IReadOnlyList<string> phoneNumbers, CancellationToken cancellationToken)
        {
            CapabilityCallCount++;
            return Task.FromResult(_capabilityResult);
        }

        public Task<JioCxCapabilityResult> CheckCapabilityAsync(string apiKey, string agentId, string phoneNumber, CancellationToken cancellationToken)
        {
            CapabilityCallCount++;
            return Task.FromResult(_capabilityResult);
        }
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
        {
            return Task.FromResult<IReadOnlyList<TEntity>>(Items.AsQueryable().Where(predicate).ToArray());
        }

        public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            entity.Id = entity.Id == 0 ? Items.Count + 1 : entity.Id;
            Items.Add(entity);
            return Task.CompletedTask;
        }

        public void Update(TEntity entity)
        {
        }

        public void Remove(TEntity entity)
        {
            Items.Remove(entity);
        }
    }
}
