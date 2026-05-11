using System.Linq.Expressions;
using FluentAssertions;
using JioCxRcsWrapper.Application.Clients;
using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Application.Security;
using JioCxRcsWrapper.Domain.Common;
using JioCxRcsWrapper.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace JioCxRcsWrapper.UnitTests.Clients;

public sealed class ClientOnboardingServiceTests
{
    [Fact]
    public async Task CreateClient_EncryptsApiKey()
    {
        var unitOfWork = ClientTestUnitOfWork.Create();
        var service = ClientOnboardingServiceFactory.Create(unitOfWork);

        await service.CreateAsync(ValidRequest(), adminUserId: 1);

        unitOfWork.Clients.Single().ApiKey.Should().Be("protected:secret-key");
    }

    [Fact]
    public async Task CreateClient_AutoCreatesManagerUser()
    {
        var unitOfWork = ClientTestUnitOfWork.Create();
        var service = ClientOnboardingServiceFactory.Create(unitOfWork);

        await service.CreateAsync(ValidRequest(), adminUserId: 1);

        var manager = unitOfWork.Users.Single(x => x.Email == "manager@example.com");
        manager.RoleId.Should().Be(2);
        manager.ClientId.Should().Be(unitOfWork.Clients.Single().Id);
        manager.PasswordHash.Should().NotBe("Password@123");
    }

    [Fact]
    public async Task CreateClient_AllowsDuplicateAgentId()
    {
        var unitOfWork = ClientTestUnitOfWork.Create();
        unitOfWork.Clients.Add(new Client { Id = 99, AgentId = "agent-1", ApiKey = "x", BrandName = "Existing", AgentName = "Existing", SiteName = "Existing" });
        var service = ClientOnboardingServiceFactory.Create(unitOfWork);

        await service.CreateAsync(ValidRequest(), adminUserId: 1);

        unitOfWork.Clients.Should().HaveCount(2);
        unitOfWork.Clients.Select(x => x.AgentId).Should().OnlyContain(x => x == "agent-1");
    }

    [Fact]
    public async Task UpdateClient_UpdatesClientDetails()
    {
        var unitOfWork = ClientTestUnitOfWork.Create();
        unitOfWork.Clients.Add(new Client
        {
            Id = 7,
            BrandName = "Old Brand",
            AgentName = "Old Agent",
            AgentId = "old-agent",
            ApiKey = "protected:old-key",
            SiteName = "Old Site",
            LogoPath = "/old.png"
        });
        var service = ClientOnboardingServiceFactory.Create(unitOfWork);

        await service.UpdateAsync(new UpdateClientRequest(7, "New Brand", "New Agent", "new-agent", "new-key", "New Site", "/new.png"), CancellationToken.None);

        var client = unitOfWork.Clients.Single();
        client.BrandName.Should().Be("New Brand");
        client.AgentName.Should().Be("New Agent");
        client.AgentId.Should().Be("new-agent");
        client.ApiKey.Should().Be("protected:new-key");
        client.SiteName.Should().Be("New Site");
        client.LogoPath.Should().Be("/new.png");
    }

    [Fact]
    public async Task DeleteClient_RemovesClient()
    {
        var unitOfWork = ClientTestUnitOfWork.Create();
        unitOfWork.Clients.Add(new Client { Id = 7, BrandName = "Brand", AgentName = "Agent", AgentId = "agent-1", ApiKey = "protected:key", SiteName = "Site" });
        var service = ClientOnboardingServiceFactory.Create(unitOfWork);

        await service.DeleteAsync(7, CancellationToken.None);

        unitOfWork.Clients.Should().BeEmpty();
    }

    [Fact]
    public async Task Branding_UsesClientLogoAndSiteNameWhenClientIsAssigned()
    {
        var unitOfWork = ClientTestUnitOfWork.Create();
        unitOfWork.Clients.Add(new Client { Id = 7, BrandName = "Brand", AgentName = "Agent", AgentId = "agent-1", ApiKey = "protected:key", SiteName = "Client Portal", LogoPath = "/uploads/client-logos/client.png" });
        var service = new BrandingService(unitOfWork);

        var result = await service.GetBrandingAsync(7, CancellationToken.None);

        result.SiteName.Should().Be("Client Portal");
        result.LogoPath.Should().Be("/uploads/client-logos/client.png");
    }

    [Fact]
    public async Task Branding_DefaultsToAdvaitServices()
    {
        var unitOfWork = ClientTestUnitOfWork.Create();
        var service = new BrandingService(unitOfWork);

        var result = await service.GetBrandingAsync(null, CancellationToken.None);

        result.SiteName.Should().Be("Advait Services");
        result.LogoPath.Should().BeNull();
    }

    private static CreateClientRequest ValidRequest() =>
        new("Brand", "Agent", "agent-1", "secret-key", "Site", "Manager", "manager@example.com", "Password@123", null);
}

internal static class ClientOnboardingServiceFactory
{
    public static ClientOnboardingService Create(ClientTestUnitOfWork unitOfWork) =>
        new(unitOfWork, new FakeSecretProtector(), new PasswordHasher<User>(), new NoopAuditService());
}

internal sealed class FakeSecretProtector : ISecretProtector
{
    public string Protect(string value) => $"protected:{value}";
    public string Unprotect(string protectedValue) => protectedValue.Replace("protected:", string.Empty, StringComparison.Ordinal);
}

internal sealed class NoopAuditService : IAuditService
{
    public Task LogAsync(int userId, string action, string module, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task LogAsync(int userId, string action, string module, string? requestPayload, string? responseJson, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class ClientTestUnitOfWork : IUnitOfWork
{
    public List<Client> Clients { get; } = [];
    public List<ClientBrandingSetting> BrandingSettings { get; } = [];
    public List<User> Users { get; } = [];
    public List<UserCreditHistory> UserCreditHistories { get; } = [];
    public List<Role> Roles { get; } = [new Role { Id = 1, Name = "Admin" }, new Role { Id = 2, Name = "Manager" }];

    public static ClientTestUnitOfWork Create() => new();

    public IRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity
    {
        if (typeof(TEntity) == typeof(Client)) return (IRepository<TEntity>)(object)new MutableListRepository<Client>(Clients);
        if (typeof(TEntity) == typeof(ClientBrandingSetting)) return (IRepository<TEntity>)(object)new MutableListRepository<ClientBrandingSetting>(BrandingSettings);
        if (typeof(TEntity) == typeof(User)) return (IRepository<TEntity>)(object)new MutableListRepository<User>(Users);
        if (typeof(TEntity) == typeof(UserCreditHistory)) return (IRepository<TEntity>)(object)new MutableListRepository<UserCreditHistory>(UserCreditHistories);
        if (typeof(TEntity) == typeof(Role)) return (IRepository<TEntity>)(object)new MutableListRepository<Role>(Roles);
        throw new NotSupportedException(typeof(TEntity).Name);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var id = 1;
        foreach (var client in Clients.Where(x => x.Id == 0)) client.Id = id++;
        id = 1;
        foreach (var user in Users.Where(x => x.Id == 0)) user.Id = id++;
        return Task.FromResult(1);
    }
}

internal sealed class MutableListRepository<TEntity> : IRepository<TEntity>
    where TEntity : BaseEntity
{
    private readonly List<TEntity> _items;
    public MutableListRepository(List<TEntity> items) => _items = items;
    public IQueryable<TEntity> Query() => _items.AsQueryable();
    public Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));
    public Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TEntity>>(_items.AsQueryable().Where(predicate).ToList());
    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) { _items.Add(entity); return Task.CompletedTask; }
    public void Update(TEntity entity) { }
    public void Remove(TEntity entity) => _items.Remove(entity);
}
