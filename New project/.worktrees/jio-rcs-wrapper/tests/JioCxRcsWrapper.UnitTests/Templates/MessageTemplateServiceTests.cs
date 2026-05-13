using System.Linq.Expressions;
using FluentAssertions;
using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Application.Security;
using JioCxRcsWrapper.Application.Templates;
using JioCxRcsWrapper.Domain.Common;
using JioCxRcsWrapper.Domain.Entities;
using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.UnitTests.Templates;

public sealed class MessageTemplateServiceTests
{
    [Fact]
    public async Task List_ReturnsClientNameInsteadOfOnlyClientId()
    {
        var harness = TemplateHarness.CreateAdmin();
        harness.AddClient(10, "BATA");
        harness.AddTemplate(1, "Sale", 10);

        var result = await harness.Service.ListAsync();

        result.Should().ContainSingle(template => template.ClientName == "BATA");
    }

    [Fact]
    public async Task Update_ChangesTemplateDetails()
    {
        var harness = TemplateHarness.CreateAdmin();
        harness.AddTemplate(1, "Old", 10);

        await harness.Service.UpdateAsync(new UpdateMessageTemplateRequest(
            1,
            "New",
            MessageType.StandaloneCard,
            """{"content":{"richCardDetails":{}}}""",
            20,
            "/local.png",
            "https://cdn.example.com/local.png",
            "image/png"));

        var template = harness.Templates.Single();
        template.Name.Should().Be("New");
        template.ClientId.Should().Be(20);
        template.MessageType.Should().Be(MessageType.StandaloneCard);
        template.RcsMediaUrl.Should().Be("https://cdn.example.com/local.png");
    }

    [Fact]
    public async Task Delete_RemovesTemplate()
    {
        var harness = TemplateHarness.CreateAdmin();
        harness.AddTemplate(1, "Old", 10);

        await harness.Service.DeleteAsync(1);

        harness.Templates.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_WhenTemplateIsUsedByCampaign_IsRejected()
    {
        var harness = TemplateHarness.CreateAdmin();
        harness.AddTemplate(1, "Used", 10);
        harness.AddCampaignMessage(templateId: 1);

        var act = () => harness.Service.DeleteAsync(1);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Template is used by a campaign and cannot be deleted.");
        harness.Templates.Should().ContainSingle();
    }

    private sealed class TemplateHarness
    {
        private readonly FakeUnitOfWork _unitOfWork = new();

        private TemplateHarness(FakeCurrentUser currentUser)
        {
            Service = new MessageTemplateService(_unitOfWork, currentUser);
        }

        public MessageTemplateService Service { get; }

        public IReadOnlyList<MessageTemplate> Templates => _unitOfWork.Set<MessageTemplate>();

        public static TemplateHarness CreateAdmin() => new(new FakeCurrentUser(1, "Admin", null));

        public void AddClient(int id, string brandName) =>
            _unitOfWork.AddSeed(new Client { Id = id, BrandName = brandName, AgentName = "Agent", AgentId = $"agent-{id}", ApiKey = "key", SiteName = "Site" });

        public void AddTemplate(int id, string name, int? clientId) =>
            _unitOfWork.AddSeed(new MessageTemplate
            {
                Id = id,
                Name = name,
                ClientId = clientId,
                MessageType = MessageType.PlainText,
                PayloadJson = """{"content":{"plainText":"Hi"}}""",
                CreatedBy = 1
            });

        public void AddCampaignMessage(int templateId) =>
            _unitOfWork.AddSeed(new CampaignMessage
            {
                Id = 1,
                CampaignId = 10,
                TemplateId = templateId,
                MessageType = MessageType.PlainText,
                PayloadJson = """{"content":{"plainText":"Hi"}}"""
            });
    }

    private sealed record FakeCurrentUser(int UserId, string Role, int? ClientId) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public bool IsDeveloper => false;
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
    }

    private sealed class FakeRepository<TEntity> : IRepository<TEntity> where TEntity : BaseEntity
    {
        public List<TEntity> Items { get; } = [];

        public IQueryable<TEntity> Query() => Items.AsQueryable();

        public Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.SingleOrDefault(item => item.Id == id));

        public Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TEntity>>(Items.AsQueryable().Where(predicate).ToArray());

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
