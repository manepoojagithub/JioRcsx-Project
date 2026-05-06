using System.Linq.Expressions;
using FluentAssertions;
using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Application.Reports;
using JioCxRcsWrapper.Application.Security;
using JioCxRcsWrapper.Domain.Common;
using JioCxRcsWrapper.Domain.Entities;
using JioCxRcsWrapper.Domain.Enums;
using JioCxRcsWrapper.Infrastructure.Exports;

namespace JioCxRcsWrapper.UnitTests.Reports;

public sealed class ReportServiceTests
{
    [Fact]
    public async Task AdminReport_IncludesAllClients()
    {
        var harness = ReportHarness.Create(role: "Admin", clientId: null);
        harness.AddCampaign(1, clientId: 10);
        harness.AddCampaign(2, clientId: 20);

        var result = await harness.Service.GetCampaignReportsAsync(CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ManagerReport_IncludesOnlyOwnClient()
    {
        var harness = ReportHarness.Create(role: "Manager", clientId: 10);
        harness.AddClient(10);
        harness.AddCampaign(1, clientId: 10);
        harness.AddCampaign(2, clientId: 20);

        var result = await harness.Service.GetCampaignReportsAsync(CancellationToken.None);

        result.Should().ContainSingle();
        result[0].ClientId.Should().Be(10);
        result[0].ClientName.Should().Be("Brand");
    }

    [Fact]
    public async Task Reports_AreAvailableForClientScope()
    {
        var harness = ReportHarness.Create(role: "Manager", clientId: 10);
        harness.AddClient(10);
        var campaign = harness.AddCampaign(1, clientId: 10);
        harness.AddContact(campaign.Id, "+918000000000", ContactStatus.Delivered);

        var result = await harness.Service.GetContactReportAsync(campaign.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Rows.Should().ContainSingle(row => row.Status == ContactStatus.Delivered);
    }

    [Fact]
    public async Task DeveloperDetails_IncludeApiDiagnosticsFromLastMessageLog()
    {
        var harness = ReportHarness.Create(role: "Manager", clientId: 10, isDeveloper: true);
        var campaign = harness.AddCampaign(1, clientId: 10);
        var contact = harness.AddContact(campaign.Id, "+919209095968", ContactStatus.Failed);
        harness.AddLog(campaign.Id, contact.Id, "RCS_CAPABILITY", """
        {
          "errorMessage": "JioCX capability response did not mark this number as RCS capable.",
          "requestHeaders": "x-apikey: ***MASKED***\nagentid: agent-1",
          "requestPayload": "{\"PhoneNumbers\":[\"+919209095968\"]}",
          "responseStatusCode": "200",
          "responseBody": "{\"capable\":false}"
        }
        """);

        var result = await harness.Service.GetContactReportAsync(campaign.Id, CancellationToken.None);

        var row = result.Rows.Single();
        row.LastError.Should().Be("RCS_CAPABILITY");
        row.ErrorMessage.Should().Contain("not mark this number");
        row.RequestHeaders.Should().Contain("x-apikey");
        row.RequestPayload.Should().Contain("+919209095968");
        row.ResponseStatusCode.Should().Be("200");
        row.ResponseBody.Should().Contain("false");
    }

    [Fact]
    public async Task NonDeveloperDetails_HideApiDiagnosticsFromLastMessageLog()
    {
        var harness = ReportHarness.Create(role: "Manager", clientId: 10, isDeveloper: false);
        var campaign = harness.AddCampaign(1, clientId: 10);
        var contact = harness.AddContact(campaign.Id, "+919209095968", ContactStatus.Failed);
        harness.AddLog(campaign.Id, contact.Id, "RCS_CAPABILITY", """
        {
          "errorMessage": "Exact error",
          "requestHeaders": "headers",
          "requestPayload": "payload",
          "responseStatusCode": "400",
          "responseBody": "response"
        }
        """);

        var result = await harness.Service.GetContactReportAsync(campaign.Id, CancellationToken.None);

        var row = result.Rows.Single();
        row.LastError.Should().BeNull();
        row.ErrorMessage.Should().BeNull();
        row.RequestHeaders.Should().BeNull();
        row.RequestPayload.Should().BeNull();
        row.ResponseStatusCode.Should().BeNull();
        row.ResponseBody.Should().BeNull();
    }

    [Fact]
    public void CsvExport_IncludesApiDiagnostics()
    {
        var exporter = new CsvReportExporter();
        var rows = new[]
        {
            new ContactReportRow("Campaign", "+918000000000", ContactStatus.Failed, false, false, "RCS_CAPABILITY", DateTimeOffset.Parse("2026-05-03T10:00:00Z"), "Exact error", "headers", "payload", "403", "response")
        };

        var csv = exporter.Export(rows, includeDeveloperDiagnostics: true);

        csv.Should().Contain("ErrorMessage,RequestHeaders,RequestPayload,ResponseStatusCode,ResponseBody");
        csv.Should().Contain("Exact error");
        csv.Should().Contain("payload");
        csv.Should().Contain("response");
    }

    [Fact]
    public void CsvExport_ForNonDeveloper_HidesApiDiagnostics()
    {
        var exporter = new CsvReportExporter();
        var rows = new[]
        {
            new ContactReportRow("Campaign", "+918000000000", ContactStatus.Failed, false, false, "RCS_CAPABILITY", DateTimeOffset.Parse("2026-05-03T10:00:00Z"), "Exact error", "headers", "payload", "403", "response")
        };

        var csv = exporter.Export(rows, includeDeveloperDiagnostics: false);

        csv.Should().NotContain("LastError");
        csv.Should().NotContain("ErrorMessage");
        csv.Should().NotContain("RequestHeaders");
        csv.Should().NotContain("Exact error");
        csv.Should().NotContain("payload");
    }

    [Fact]
    public void CsvExport_IncludesMobileStatusAndActions()
    {
        var exporter = new CsvReportExporter();
        var rows = new[]
        {
            new ContactReportRow("Campaign", "+918000000000", ContactStatus.Clicked, true, true, null, DateTimeOffset.Parse("2026-05-03T10:00:00Z"))
        };

        var csv = exporter.Export(rows);

        csv.Should().Contain("Campaign,MobileNumber,Status,Opened,Clicked,LastUpdated");
        csv.Should().Contain("+918000000000");
        csv.Should().Contain("Clicked");
    }

    private sealed class ReportHarness
    {
        private readonly FakeUnitOfWork _unitOfWork = new();

        private ReportHarness(FakeCurrentUser currentUser)
        {
            Service = new ReportService(_unitOfWork, currentUser);
        }

        public ReportService Service { get; }

        public static ReportHarness Create(string role, int? clientId, bool isDeveloper = false) => new(new FakeCurrentUser(7, role, clientId, isDeveloper));

        public Client AddClient(int id)
        {
            var client = new Client { Id = id, BrandName = "Brand", AgentName = "Agent", AgentId = $"agent-{id}", ApiKey = "secret", SiteName = "Site", CreatedBy = 1 };
            _unitOfWork.AddSeed(client);
            return client;
        }

        public Campaign AddCampaign(int id, int clientId)
        {
            var campaign = new Campaign { Id = id, ClientId = clientId, Name = $"Campaign {id}", Type = CampaignType.Schedule, CreatedBy = 1 };
            _unitOfWork.AddSeed(campaign);
            return campaign;
        }

        public Contact AddContact(int campaignId, string mobileNumber, ContactStatus status)
        {
            var contact = new Contact { Id = _unitOfWork.NextId<Contact>(), CampaignId = campaignId, MobileNumber = mobileNumber, Status = status };
            _unitOfWork.AddSeed(contact);
            return contact;
        }

        public void AddLog(int campaignId, int contactId, string errorCode, string response)
        {
            _unitOfWork.AddSeed(new MessageLog
            {
                Id = _unitOfWork.NextId<MessageLog>(),
                CampaignId = campaignId,
                ContactId = contactId,
                Status = errorCode,
                ErrorCode = errorCode,
                Response = response,
                Timestamp = DateTimeOffset.Parse("2026-05-03T10:00:00Z")
            });
        }
    }

    private sealed record FakeCurrentUser(int UserId, string Role, int? ClientId, bool IsDeveloper) : ICurrentUser
    {
        public bool IsAuthenticated => true;
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

        public int NextId<TEntity>() where TEntity : BaseEntity => ((FakeRepository<TEntity>)Repository<TEntity>()).Items.Count + 1;
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
            Items.Add(entity);
            return Task.CompletedTask;
        }

        public void Update(TEntity entity)
        {
        }

        public void Remove(TEntity entity) => Items.Remove(entity);
    }
}
