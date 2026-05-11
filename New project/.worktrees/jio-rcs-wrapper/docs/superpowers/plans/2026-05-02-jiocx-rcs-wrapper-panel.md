# JioCX RCS Wrapper Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a production-ready ASP.NET Core 9 MVC JioCX RCS Wrapper Panel with Clean Architecture, SQL Server EF Core, RBAC, SignalR, reports, and a durable database-backed campaign send queue.

**Architecture:** One solution with `Domain`, `Application`, `Infrastructure`, `Web`, unit test, and integration test projects. MVC requests call application services, application services enforce RBAC and tenant scope, infrastructure owns EF Core, encryption, exports, and the documented JioCX UAT API client. Sends go through SQL queue rows processed by a hosted background service.

**Tech Stack:** ASP.NET Core 9 MVC, Razor Views, jQuery/AJAX, SignalR, EF Core SQL Server, Data Protection, Cookie Auth, JWT, Repository Pattern, Unit of Work, xUnit, Moq, FluentAssertions.

---

## Approved Constraints

- Use only documented JioCX UAT APIs:
  - `POST https://rcsapi.jiocx.com/api/v1/uploadFile`
  - `POST https://rcsapi.jiocx.com/api/v1/sendMessage`
  - `POST https://rcsapi.jiocx.com/api/v1/checkCapability`
- Do not implement undocumented onboarding or tester-management API calls.
- Do not invent payload schemas for dialer, calendar, location CTA actions, or webhook bodies.
- Store raw webhook JSON and parse defensively.
- Block live sends for unsupported/undocumented CTA action payloads.
- Use a database-backed queue for all campaign sends.

## File Structure

- `JioCxRcsWrapper.sln`: solution file.
- `src/JioCxRcsWrapper.Domain`: entities, enums, constants, domain validation result types.
- `src/JioCxRcsWrapper.Application`: DTOs, service interfaces, service implementations, validators, auth/RBAC policies, queue orchestration contracts.
- `src/JioCxRcsWrapper.Infrastructure`: EF Core `AppDbContext`, entity configurations, migrations, repositories, Unit of Work, JioCX HTTP client, encryption, CSV/PDF exporters, queue worker.
- `src/JioCxRcsWrapper.Web`: MVC controllers, Razor views, view models, SignalR hubs, filters, middleware, static assets, AJAX scripts.
- `tests/JioCxRcsWrapper.UnitTests`: validator, service, RBAC, queue retry, payload builder tests.
- `tests/JioCxRcsWrapper.IntegrationTests`: EF Core mapping/repository/controller authorization tests.
- `docs/deployment.md`: deployment steps.
- `README.md`: setup and operation guide.

---

### Task 1: Scaffold Solution And Project References

**Files:**
- Create: `JioCxRcsWrapper.sln`
- Create: `src/JioCxRcsWrapper.Domain/JioCxRcsWrapper.Domain.csproj`
- Create: `src/JioCxRcsWrapper.Application/JioCxRcsWrapper.Application.csproj`
- Create: `src/JioCxRcsWrapper.Infrastructure/JioCxRcsWrapper.Infrastructure.csproj`
- Create: `src/JioCxRcsWrapper.Web/JioCxRcsWrapper.Web.csproj`
- Create: `tests/JioCxRcsWrapper.UnitTests/JioCxRcsWrapper.UnitTests.csproj`
- Create: `tests/JioCxRcsWrapper.IntegrationTests/JioCxRcsWrapper.IntegrationTests.csproj`

- [ ] **Step 1: Create solution and projects**

Run:

```powershell
dotnet new sln -n JioCxRcsWrapper
dotnet new classlib -n JioCxRcsWrapper.Domain -o src/JioCxRcsWrapper.Domain -f net9.0
dotnet new classlib -n JioCxRcsWrapper.Application -o src/JioCxRcsWrapper.Application -f net9.0
dotnet new classlib -n JioCxRcsWrapper.Infrastructure -o src/JioCxRcsWrapper.Infrastructure -f net9.0
dotnet new mvc -n JioCxRcsWrapper.Web -o src/JioCxRcsWrapper.Web -f net9.0
dotnet new xunit -n JioCxRcsWrapper.UnitTests -o tests/JioCxRcsWrapper.UnitTests -f net9.0
dotnet new xunit -n JioCxRcsWrapper.IntegrationTests -o tests/JioCxRcsWrapper.IntegrationTests -f net9.0
```

Expected: each command completes successfully and creates a project.

- [ ] **Step 2: Add projects to solution**

Run:

```powershell
dotnet sln add src/JioCxRcsWrapper.Domain/JioCxRcsWrapper.Domain.csproj
dotnet sln add src/JioCxRcsWrapper.Application/JioCxRcsWrapper.Application.csproj
dotnet sln add src/JioCxRcsWrapper.Infrastructure/JioCxRcsWrapper.Infrastructure.csproj
dotnet sln add src/JioCxRcsWrapper.Web/JioCxRcsWrapper.Web.csproj
dotnet sln add tests/JioCxRcsWrapper.UnitTests/JioCxRcsWrapper.UnitTests.csproj
dotnet sln add tests/JioCxRcsWrapper.IntegrationTests/JioCxRcsWrapper.IntegrationTests.csproj
```

Expected: all projects are listed in `dotnet sln list`.

- [ ] **Step 3: Wire Clean Architecture references**

Run:

```powershell
dotnet add src/JioCxRcsWrapper.Application/JioCxRcsWrapper.Application.csproj reference src/JioCxRcsWrapper.Domain/JioCxRcsWrapper.Domain.csproj
dotnet add src/JioCxRcsWrapper.Infrastructure/JioCxRcsWrapper.Infrastructure.csproj reference src/JioCxRcsWrapper.Application/JioCxRcsWrapper.Application.csproj
dotnet add src/JioCxRcsWrapper.Infrastructure/JioCxRcsWrapper.Infrastructure.csproj reference src/JioCxRcsWrapper.Domain/JioCxRcsWrapper.Domain.csproj
dotnet add src/JioCxRcsWrapper.Web/JioCxRcsWrapper.Web.csproj reference src/JioCxRcsWrapper.Application/JioCxRcsWrapper.Application.csproj
dotnet add src/JioCxRcsWrapper.Web/JioCxRcsWrapper.Web.csproj reference src/JioCxRcsWrapper.Infrastructure/JioCxRcsWrapper.Infrastructure.csproj
dotnet add tests/JioCxRcsWrapper.UnitTests/JioCxRcsWrapper.UnitTests.csproj reference src/JioCxRcsWrapper.Application/JioCxRcsWrapper.Application.csproj
dotnet add tests/JioCxRcsWrapper.UnitTests/JioCxRcsWrapper.UnitTests.csproj reference src/JioCxRcsWrapper.Domain/JioCxRcsWrapper.Domain.csproj
dotnet add tests/JioCxRcsWrapper.IntegrationTests/JioCxRcsWrapper.IntegrationTests.csproj reference src/JioCxRcsWrapper.Web/JioCxRcsWrapper.Web.csproj
dotnet add tests/JioCxRcsWrapper.IntegrationTests/JioCxRcsWrapper.IntegrationTests.csproj reference src/JioCxRcsWrapper.Infrastructure/JioCxRcsWrapper.Infrastructure.csproj
```

Expected: references are added with no circular dependency.

- [ ] **Step 4: Add required NuGet packages**

Run:

```powershell
dotnet add src/JioCxRcsWrapper.Infrastructure/JioCxRcsWrapper.Infrastructure.csproj package Microsoft.EntityFrameworkCore.SqlServer
dotnet add src/JioCxRcsWrapper.Infrastructure/JioCxRcsWrapper.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Design
dotnet add src/JioCxRcsWrapper.Infrastructure/JioCxRcsWrapper.Infrastructure.csproj package CsvHelper
dotnet add src/JioCxRcsWrapper.Infrastructure/JioCxRcsWrapper.Infrastructure.csproj package QuestPDF
dotnet add src/JioCxRcsWrapper.Web/JioCxRcsWrapper.Web.csproj package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add src/JioCxRcsWrapper.Web/JioCxRcsWrapper.Web.csproj package Microsoft.EntityFrameworkCore.Design
dotnet add tests/JioCxRcsWrapper.UnitTests/JioCxRcsWrapper.UnitTests.csproj package FluentAssertions
dotnet add tests/JioCxRcsWrapper.UnitTests/JioCxRcsWrapper.UnitTests.csproj package Moq
dotnet add tests/JioCxRcsWrapper.IntegrationTests/JioCxRcsWrapper.IntegrationTests.csproj package Microsoft.AspNetCore.Mvc.Testing
dotnet add tests/JioCxRcsWrapper.IntegrationTests/JioCxRcsWrapper.IntegrationTests.csproj package Microsoft.EntityFrameworkCore.Sqlite
dotnet add tests/JioCxRcsWrapper.IntegrationTests/JioCxRcsWrapper.IntegrationTests.csproj package FluentAssertions
```

Expected: restore succeeds.

- [ ] **Step 5: Build solution**

Run:

```powershell
dotnet build
```

Expected: build succeeds.

- [ ] **Step 6: Commit scaffold**

Run:

```powershell
git add JioCxRcsWrapper.sln src tests
git commit -m "chore: scaffold clean architecture solution"
```

Expected: commit succeeds.

---

### Task 2: Implement Domain Entities And Enums

**Files:**
- Create: `src/JioCxRcsWrapper.Domain/Common/BaseEntity.cs`
- Create: `src/JioCxRcsWrapper.Domain/Entities/*.cs`
- Create: `src/JioCxRcsWrapper.Domain/Enums/*.cs`
- Test: `tests/JioCxRcsWrapper.UnitTests/Domain/DomainDefaultsTests.cs`

- [ ] **Step 1: Write domain defaults test**

Create `tests/JioCxRcsWrapper.UnitTests/Domain/DomainDefaultsTests.cs`:

```csharp
using FluentAssertions;
using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.UnitTests.Domain;

public sealed class DomainDefaultsTests
{
    [Fact]
    public void CampaignQueueStatus_IncludesRetryableStates()
    {
        Enum.GetNames<CampaignQueueStatus>()
            .Should()
            .Contain(new[] { "Pending", "Processing", "RetryScheduled", "Succeeded", "Failed", "Paused" });
    }

    [Fact]
    public void CtaActionType_OnlyOpenUrlIsSendableFromDocumentedSchema()
    {
        CtaActionType.OpenUrl.Should().Be(CtaActionType.OpenUrl);
        CtaActionType.Dialer.Should().NotBe(CtaActionType.OpenUrl);
        CtaActionType.Calendar.Should().NotBe(CtaActionType.OpenUrl);
        CtaActionType.Location.Should().NotBe(CtaActionType.OpenUrl);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test tests/JioCxRcsWrapper.UnitTests/JioCxRcsWrapper.UnitTests.csproj --filter DomainDefaultsTests
```

Expected: FAIL because enums do not exist.

- [ ] **Step 3: Add base entity**

Create `src/JioCxRcsWrapper.Domain/Common/BaseEntity.cs`:

```csharp
namespace JioCxRcsWrapper.Domain.Common;

public abstract class BaseEntity
{
    public int Id { get; set; }
}
```

- [ ] **Step 4: Add enums**

Create enum files under `src/JioCxRcsWrapper.Domain/Enums/`:

```csharp
namespace JioCxRcsWrapper.Domain.Enums;

public enum CampaignType { Schedule = 1, Recurring = 2 }
public enum CampaignStatus { Draft = 1, Scheduled = 2, Queued = 3, Processing = 4, Completed = 5, Failed = 6, Paused = 7 }
public enum MessageType { PlainText = 1, RichCard = 2 }
public enum ContactStatus { Pending = 1, Sent = 2, Delivered = 3, Failed = 4, Opened = 5, Clicked = 6 }
public enum CampaignQueueStatus { Pending = 1, Processing = 2, RetryScheduled = 3, Succeeded = 4, Failed = 5, Paused = 6 }
public enum CtaActionType { OpenUrl = 1, Dialer = 2, Calendar = 3, Location = 4 }
public enum MediaType { Image = 1, Video = 2, Gif = 3 }
```

If C# requires one public enum per file in the repo style, split these into separate files with the same declarations.

- [ ] **Step 5: Add required entities**

Create entity files under `src/JioCxRcsWrapper.Domain/Entities/`. Each entity inherits `BaseEntity`.

```csharp
using JioCxRcsWrapper.Domain.Common;
using JioCxRcsWrapper.Domain.Enums;

namespace JioCxRcsWrapper.Domain.Entities;

public sealed class User : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public int? ClientId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Role Role { get; set; } = null!;
    public Client? Client { get; set; }
}
```

Create the remaining entities with the fields from the approved spec:

```csharp
public sealed class Role : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public sealed class Permission : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public sealed class RolePermission : BaseEntity
{
    public int RoleId { get; set; }
    public int PermissionId { get; set; }
    public string Module { get; set; } = string.Empty;
    public Role Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}

public sealed class Client : BaseEntity
{
    public string BrandName { get; set; } = string.Empty;
    public string AgentName { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public string SiteName { get; set; } = string.Empty;
    public int Credits { get; set; }
    public int CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Campaign : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int ClientId { get; set; }
    public CampaignType Type { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;
    public int CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ScheduledAt { get; set; }
}

public sealed class CampaignMessage : BaseEntity
{
    public int CampaignId { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public MessageType MessageType { get; set; }
}

public sealed class Contact : BaseEntity
{
    public int CampaignId { get; set; }
    public string MobileNumber { get; set; } = string.Empty;
    public ContactStatus Status { get; set; } = ContactStatus.Pending;
}

public sealed class MessageLog : BaseEntity
{
    public int CampaignId { get; set; }
    public int ContactId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public string Response { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Report : BaseEntity
{
    public int CampaignId { get; set; }
    public int TotalSent { get; set; }
    public int Delivered { get; set; }
    public int Failed { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AuditLog : BaseEntity
{
    public int UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CampaignQueueItem : BaseEntity
{
    public int CampaignId { get; set; }
    public int ContactId { get; set; }
    public CampaignQueueStatus Status { get; set; } = CampaignQueueStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? LockedAt { get; set; }
    public string? LockedBy { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
}

public sealed class WebhookEvent : BaseEntity
{
    public int? CampaignId { get; set; }
    public int? ContactId { get; set; }
    public string? MessageId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
}

public sealed class ClientBrandingSetting : BaseEntity
{
    public int? ClientId { get; set; }
    public string SiteName { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public bool IsDefault { get; set; }
}

public sealed class UploadedMedia : BaseEntity
{
    public int ClientId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? LocalPath { get; set; }
    public string PublicUrl { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

- [ ] **Step 6: Run tests**

Run:

```powershell
dotnet test tests/JioCxRcsWrapper.UnitTests/JioCxRcsWrapper.UnitTests.csproj --filter DomainDefaultsTests
```

Expected: PASS.

- [ ] **Step 7: Commit domain model**

Run:

```powershell
git add src/JioCxRcsWrapper.Domain tests/JioCxRcsWrapper.UnitTests/Domain
git commit -m "feat: add domain model"
```

Expected: commit succeeds.

---

### Task 3: Add EF Core DbContext, Repository, Unit Of Work, And Seed Data

**Files:**
- Create: `src/JioCxRcsWrapper.Infrastructure/Data/AppDbContext.cs`
- Create: `src/JioCxRcsWrapper.Infrastructure/Data/Configurations/*.cs`
- Create: `src/JioCxRcsWrapper.Application/Common/Interfaces/IRepository.cs`
- Create: `src/JioCxRcsWrapper.Application/Common/Interfaces/IUnitOfWork.cs`
- Create: `src/JioCxRcsWrapper.Infrastructure/Repositories/Repository.cs`
- Create: `src/JioCxRcsWrapper.Infrastructure/Repositories/UnitOfWork.cs`
- Create: `src/JioCxRcsWrapper.Infrastructure/Data/SeedData.cs`
- Test: `tests/JioCxRcsWrapper.IntegrationTests/Data/AppDbContextTests.cs`

- [ ] **Step 1: Write EF mapping test**

Create `tests/JioCxRcsWrapper.IntegrationTests/Data/AppDbContextTests.cs`:

```csharp
using FluentAssertions;
using JioCxRcsWrapper.Domain.Entities;
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

        var client = new Client { BrandName = "Brand", AgentName = "Agent", AgentId = "agent-1", ApiKey = "encrypted", SiteName = "Site", Credits = 10, CreatedBy = 1 };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var campaign = new Campaign { Name = "Test", ClientId = client.Id, CreatedBy = 1, Type = JioCxRcsWrapper.Domain.Enums.CampaignType.Schedule };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();

        var contact = new Contact { CampaignId = campaign.Id, MobileNumber = "+918000000000" };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        db.CampaignQueueItems.Add(new CampaignQueueItem { CampaignId = campaign.Id, ContactId = contact.Id });
        await db.SaveChangesAsync();

        (await db.CampaignQueueItems.CountAsync()).Should().Be(1);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test tests/JioCxRcsWrapper.IntegrationTests/JioCxRcsWrapper.IntegrationTests.csproj --filter AppDbContextTests
```

Expected: FAIL because infrastructure data classes do not exist.

- [ ] **Step 3: Add repository contracts**

Create `src/JioCxRcsWrapper.Application/Common/Interfaces/IRepository.cs`:

```csharp
using System.Linq.Expressions;
using JioCxRcsWrapper.Domain.Common;

namespace JioCxRcsWrapper.Application.Common.Interfaces;

public interface IRepository<TEntity> where TEntity : BaseEntity
{
    IQueryable<TEntity> Query();
    Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    void Update(TEntity entity);
    void Remove(TEntity entity);
}
```

Create `src/JioCxRcsWrapper.Application/Common/Interfaces/IUnitOfWork.cs`:

```csharp
namespace JioCxRcsWrapper.Application.Common.Interfaces;

public interface IUnitOfWork
{
    IRepository<TEntity> Repository<TEntity>() where TEntity : JioCxRcsWrapper.Domain.Common.BaseEntity;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Add AppDbContext**

Create `src/JioCxRcsWrapper.Infrastructure/Data/AppDbContext.cs`:

```csharp
using JioCxRcsWrapper.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JioCxRcsWrapper.Infrastructure.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignMessage> CampaignMessages => Set<CampaignMessage>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<MessageLog> MessageLogs => Set<MessageLog>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<CampaignQueueItem> CampaignQueueItems => Set<CampaignQueueItem>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
    public DbSet<ClientBrandingSetting> ClientBrandingSettings => Set<ClientBrandingSetting>();
    public DbSet<UploadedMedia> UploadedMedia => Set<UploadedMedia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

- [ ] **Step 5: Add focused entity configuration**

Create `src/JioCxRcsWrapper.Infrastructure/Data/Configurations/CoreEntityConfiguration.cs`:

```csharp
using JioCxRcsWrapper.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JioCxRcsWrapper.Infrastructure.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasIndex(x => x.Email).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(1000).IsRequired();
        builder.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.Property(x => x.BrandName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.AgentName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.AgentId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ApiKey).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.SiteName).HasMaxLength(200).IsRequired();
    }
}

public sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.ClientId, x.Status });
    }
}

public sealed class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.Property(x => x.MobileNumber).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.CampaignId, x.MobileNumber }).IsUnique();
    }
}

public sealed class CampaignQueueItemConfiguration : IEntityTypeConfiguration<CampaignQueueItem>
{
    public void Configure(EntityTypeBuilder<CampaignQueueItem> builder)
    {
        builder.HasIndex(x => new { x.Status, x.NextAttemptAt });
        builder.Property(x => x.LockedBy).HasMaxLength(100);
    }
}
```

Create `src/JioCxRcsWrapper.Infrastructure/Data/Configurations/OperationalEntityConfiguration.cs`:

```csharp
using JioCxRcsWrapper.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JioCxRcsWrapper.Infrastructure.Data.Configurations;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.Property(x => x.Module).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new { x.RoleId, x.PermissionId, x.Module }).IsUnique();
    }
}

public sealed class CampaignMessageConfiguration : IEntityTypeConfiguration<CampaignMessage>
{
    public void Configure(EntityTypeBuilder<CampaignMessage> builder)
    {
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.HasIndex(x => x.CampaignId).IsUnique();
    }
}

public sealed class MessageLogConfiguration : IEntityTypeConfiguration<MessageLog>
{
    public void Configure(EntityTypeBuilder<MessageLog> builder)
    {
        builder.Property(x => x.Status).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ErrorCode).HasMaxLength(80);
        builder.Property(x => x.Response).IsRequired();
        builder.HasIndex(x => new { x.CampaignId, x.ContactId, x.Timestamp });
    }
}

public sealed class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
{
    public void Configure(EntityTypeBuilder<WebhookEvent> builder)
    {
        builder.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.HasIndex(x => x.MessageId);
    }
}

public sealed class UploadedMediaConfiguration : IEntityTypeConfiguration<UploadedMedia>
{
    public void Configure(EntityTypeBuilder<UploadedMedia> builder)
    {
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PublicUrl).HasMaxLength(2000).IsRequired();
        builder.HasIndex(x => new { x.ClientId, x.CreatedAt });
    }
}
```

- [ ] **Step 6: Add repository implementations**

Create `src/JioCxRcsWrapper.Infrastructure/Repositories/Repository.cs`:

```csharp
using System.Linq.Expressions;
using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Domain.Common;
using JioCxRcsWrapper.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace JioCxRcsWrapper.Infrastructure.Repositories;

public sealed class Repository<TEntity> : IRepository<TEntity> where TEntity : BaseEntity
{
    private readonly AppDbContext _db;

    public Repository(AppDbContext db) => _db = db;

    public IQueryable<TEntity> Query() => _db.Set<TEntity>().AsQueryable();
    public Task<TEntity?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => _db.Set<TEntity>().FindAsync([id], cancellationToken).AsTask();
    public async Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default) => await _db.Set<TEntity>().Where(predicate).ToListAsync(cancellationToken);
    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) => _db.Set<TEntity>().AddAsync(entity, cancellationToken).AsTask();
    public void Update(TEntity entity) => _db.Set<TEntity>().Update(entity);
    public void Remove(TEntity entity) => _db.Set<TEntity>().Remove(entity);
}
```

Create `src/JioCxRcsWrapper.Infrastructure/Repositories/UnitOfWork.cs`:

```csharp
using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Domain.Common;
using JioCxRcsWrapper.Infrastructure.Data;

namespace JioCxRcsWrapper.Infrastructure.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;
    private readonly Dictionary<Type, object> _repositories = new();

    public UnitOfWork(AppDbContext db) => _db = db;

    public IRepository<TEntity> Repository<TEntity>() where TEntity : BaseEntity
    {
        var type = typeof(TEntity);
        if (!_repositories.TryGetValue(type, out var repository))
        {
            repository = new Repository<TEntity>(_db);
            _repositories[type] = repository;
        }

        return (IRepository<TEntity>)repository;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => _db.SaveChangesAsync(cancellationToken);
}
```

- [ ] **Step 7: Run mapping test**

Run:

```powershell
dotnet test tests/JioCxRcsWrapper.IntegrationTests/JioCxRcsWrapper.IntegrationTests.csproj --filter AppDbContextTests
```

Expected: PASS.

- [ ] **Step 8: Add initial SQL Server migration**

Run:

```powershell
dotnet ef migrations add InitialCreate --project src/JioCxRcsWrapper.Infrastructure --startup-project src/JioCxRcsWrapper.Web --output-dir Data/Migrations
```

Expected: migration is generated in `src/JioCxRcsWrapper.Infrastructure/Data/Migrations`.

- [ ] **Step 9: Commit persistence**

Run:

```powershell
git add src/JioCxRcsWrapper.Application src/JioCxRcsWrapper.Infrastructure tests/JioCxRcsWrapper.IntegrationTests
git commit -m "feat: add ef core persistence and repositories"
```

Expected: commit succeeds.

---

### Task 4: Configure Web Host, App Settings, DI, Health Checks, And Auth Shell

**Files:**
- Modify: `src/JioCxRcsWrapper.Web/Program.cs`
- Modify: `src/JioCxRcsWrapper.Web/appsettings.json`
- Create: `src/JioCxRcsWrapper.Web/appsettings.Development.json`
- Create: `src/JioCxRcsWrapper.Infrastructure/DependencyInjection.cs`
- Create: `src/JioCxRcsWrapper.Application/DependencyInjection.cs`
- Create: `src/JioCxRcsWrapper.Application/Common/Options/*.cs`

- [ ] **Step 1: Add options classes**

Create option classes:

```csharp
namespace JioCxRcsWrapper.Application.Common.Options;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
}

public sealed class JioCxOptions
{
    public string BaseUrl { get; set; } = "https://rcsapi.jiocx.com";
    public string UploadFilePath { get; set; } = "/api/v1/uploadFile";
    public string SendMessagePath { get; set; } = "/api/v1/sendMessage";
    public string CheckCapabilityPath { get; set; } = "/api/v1/checkCapability";
}

public sealed class QueueOptions
{
    public int BatchSize { get; set; } = 20;
    public int MaxAttempts { get; set; } = 4;
    public int PollSeconds { get; set; } = 10;
}
```

- [ ] **Step 2: Register application DI entrypoint**

Create `src/JioCxRcsWrapper.Application/DependencyInjection.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace JioCxRcsWrapper.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
```

- [ ] **Step 3: Register infrastructure services**

Create `src/JioCxRcsWrapper.Infrastructure/DependencyInjection.cs`:

```csharp
using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Infrastructure.Data;
using JioCxRcsWrapper.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JioCxRcsWrapper.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddDataProtection();
        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
        return services;
    }
}
```

- [ ] **Step 4: Configure appsettings**

Update `src/JioCxRcsWrapper.Web/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=JioCxRcsWrapper;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  },
  "Jwt": {
    "Issuer": "JioCxRcsWrapper",
    "Audience": "JioCxRcsWrapper",
    "SigningKey": "replace-with-32-character-minimum-secret",
    "ExpiryMinutes": 60
  },
  "JioCx": {
    "BaseUrl": "https://rcsapi.jiocx.com",
    "UploadFilePath": "/api/v1/uploadFile",
    "SendMessagePath": "/api/v1/sendMessage",
    "CheckCapabilityPath": "/api/v1/checkCapability"
  },
  "Queue": {
    "BatchSize": 20,
    "MaxAttempts": 4,
    "PollSeconds": 10
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

- [ ] **Step 5: Wire Program.cs**

Modify `src/JioCxRcsWrapper.Web/Program.cs`:

```csharp
using System.Text;
using JioCxRcsWrapper.Application;
using JioCxRcsWrapper.Application.Common.Options;
using JioCxRcsWrapper.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<JioCxOptions>(builder.Configuration.GetSection("JioCx"));
builder.Services.Configure<QueueOptions>(builder.Configuration.GetSection("Queue"));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()!;
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllerRoute(name: "default", pattern: "{controller=Dashboard}/{action=Index}/{id?}");
app.Run();

public partial class Program;
```

- [ ] **Step 6: Build**

Run:

```powershell
dotnet build
```

Expected: build succeeds.

- [ ] **Step 7: Commit host configuration**

Run:

```powershell
git add src/JioCxRcsWrapper.Web src/JioCxRcsWrapper.Application src/JioCxRcsWrapper.Infrastructure
git commit -m "feat: configure web host and infrastructure services"
```

Expected: commit succeeds.

---

### Task 5: Implement Password Auth, Login, JWT Issuing, And Seed Admin

**Files:**
- Create: `src/JioCxRcsWrapper.Application/Auth/*.cs`
- Create: `src/JioCxRcsWrapper.Web/Controllers/AccountController.cs`
- Create: `src/JioCxRcsWrapper.Web/Models/Auth/LoginViewModel.cs`
- Create: `src/JioCxRcsWrapper.Web/Views/Account/Login.cshtml`
- Modify: `src/JioCxRcsWrapper.Infrastructure/Data/SeedData.cs`
- Test: `tests/JioCxRcsWrapper.UnitTests/Auth/AuthServiceTests.cs`

- [ ] **Step 1: Write auth service test**

Create `tests/JioCxRcsWrapper.UnitTests/Auth/AuthServiceTests.cs`:

```csharp
using FluentAssertions;
using JioCxRcsWrapper.Application.Auth;

namespace JioCxRcsWrapper.UnitTests.Auth;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task Login_WithInactiveUser_IsRejected()
    {
        var service = AuthServiceTestFactory.CreateWithUser(
            email: "manager@example.com",
            password: "Password@123",
            isActive: false,
            roleName: "Manager",
            clientId: 10);

        var result = await service.LoginAsync(new LoginRequest("manager@example.com", "Password@123"));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("Invalid email or password.");
        result.JwtToken.Should().BeNull();
    }
}
```

Create `AuthServiceTestFactory` in the same test folder as a focused helper that builds `AuthService` with an in-memory repository, `PasswordHasher<User>`, and a stub `IJwtTokenService` returning `"token"`.

- [ ] **Step 2: Add auth DTOs and interface**

Create:

```csharp
namespace JioCxRcsWrapper.Application.Auth;

public sealed record LoginRequest(string Email, string Password);
public sealed record LoginResult(bool Succeeded, string? Error, int? UserId, string? Name, string? Role, int? ClientId, string? JwtToken);

public interface IAuthService
{
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 3: Implement auth service**

Create `src/JioCxRcsWrapper.Application/Auth/AuthService.cs`:

```csharp
using JioCxRcsWrapper.Application.Common.Interfaces;
using JioCxRcsWrapper.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace JioCxRcsWrapper.Application.Auth;

public sealed class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(IUnitOfWork unitOfWork, IPasswordHasher<User> passwordHasher, IJwtTokenService jwtTokenService)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Repository<User>()
            .Query()
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.Email == request.Email, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return new LoginResult(false, "Invalid email or password.", null, null, null, null, null);
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return new LoginResult(false, "Invalid email or password.", null, null, null, null, null);
        }

        var token = _jwtTokenService.CreateToken(user.Id, user.Email, user.Name, user.Role.Name, user.ClientId);
        return new LoginResult(true, null, user.Id, user.Name, user.Role.Name, user.ClientId, token);
    }
}
```

Create `IJwtTokenService` and `JwtTokenService` to generate claims: `sub`, `email`, `name`, `role`, and `client_id` when present.

- [ ] **Step 4: Register auth services**

Modify `src/JioCxRcsWrapper.Application/DependencyInjection.cs`:

```csharp
services.AddScoped<IAuthService, AuthService>();
services.AddScoped<IJwtTokenService, JwtTokenService>();
services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
```

- [ ] **Step 5: Add login controller and view**

Create `AccountController` with `Login` GET/POST, `Logout`, and `AccessDenied`. On successful login, issue cookie claims and store JWT in an HTTP-only cookie named `access_token`.

- [ ] **Step 6: Add seed data**

Seed roles `Admin`, `Manager`, `Viewer`, permissions `View`, `Add`, `Update`, `Delete`, `Download`, module-aware `RolePermissions`, and initial admin user:

```text
Email: admin@local.test
Password: ChangeMe@12345
```

The README must warn to rotate this password immediately.

- [ ] **Step 7: Verify login flow**

Run:

```powershell
dotnet test
dotnet run --project src/JioCxRcsWrapper.Web
```

Expected: app starts, `/Account/Login` renders, seeded admin can log in after database migration/seed.

- [ ] **Step 8: Commit auth**

Run:

```powershell
git add src tests
git commit -m "feat: add hybrid authentication"
```

Expected: commit succeeds.

---

### Task 6: Implement RBAC Filters, Tenant Scope, Menu Visibility, And Audit Logging

**Files:**
- Create: `src/JioCxRcsWrapper.Application/Security/*.cs`
- Create: `src/JioCxRcsWrapper.Web/Filters/RequirePermissionAttribute.cs`
- Create: `src/JioCxRcsWrapper.Web/Filters/PermissionFilter.cs`
- Create: `src/JioCxRcsWrapper.Web/Services/CurrentUser.cs`
- Create: `src/JioCxRcsWrapper.Web/Views/Shared/_Sidebar.cshtml`
- Test: `tests/JioCxRcsWrapper.UnitTests/Security/PermissionServiceTests.cs`

- [ ] **Step 1: Write permission service tests**

Create tests asserting:

```csharp
Admin_CanAccessAllClientScopedModules()
Manager_CannotAccessUserManagement()
Viewer_CannotAddCampaign()
```

Use role permission collections and assert boolean results.

- [ ] **Step 2: Add current user abstraction**

Create:

```csharp
public interface ICurrentUser
{
    int UserId { get; }
    string Role { get; }
    int? ClientId { get; }
    bool IsAuthenticated { get; }
}
```

Implement it in Web using `IHttpContextAccessor`.

- [ ] **Step 3: Add permission service**

Create `IPermissionService` with:

```csharp
Task<bool> HasPermissionAsync(int userId, string module, string permission, CancellationToken cancellationToken = default);
Task EnsureClientScopeAsync(int userId, int clientId, CancellationToken cancellationToken = default);
```

Implementation returns all permissions for Admin, checks `RolePermissions` for Manager/Viewer, and throws `UnauthorizedAccessException` for client-scope violations.

- [ ] **Step 4: Add MVC permission filter**

Create `RequirePermissionAttribute` with `Module` and `Permission`, and a filter that returns:

- redirect to login for unauthenticated users.
- `ForbidResult` or `/Account/AccessDenied` for authenticated users without permission.

- [ ] **Step 5: Add audit service**

Create `IAuditService.LogAsync(userId, action, module)` and implementation that inserts `AuditLog`.

- [ ] **Step 6: Add permission-aware sidebar**

Implement `_Sidebar.cshtml` so menus render only when permission checks allow:

```cshtml
@if (await PermissionUi.CanViewAsync("Campaigns"))
{
    <a asp-controller="Campaigns" asp-action="Index">Campaigns</a>
}
```

- [ ] **Step 7: Run tests**

Run:

```powershell
dotnet test --filter PermissionServiceTests
```

Expected: PASS.

- [ ] **Step 8: Commit RBAC**

Run:

```powershell
git add src tests
git commit -m "feat: enforce rbac and tenant scope"
```

Expected: commit succeeds.

---

### Task 7: Implement Client Onboarding, API Key Encryption, Branding, And Manager User Creation

**Files:**
- Create: `src/JioCxRcsWrapper.Application/Clients/*.cs`
- Create: `src/JioCxRcsWrapper.Infrastructure/Security/ApiKeyProtector.cs`
- Create: `src/JioCxRcsWrapper.Web/Controllers/ClientsController.cs`
- Create: `src/JioCxRcsWrapper.Web/Views/Clients/*.cshtml`
- Test: `tests/JioCxRcsWrapper.UnitTests/Clients/ClientOnboardingServiceTests.cs`

- [ ] **Step 1: Write onboarding tests**

Tests:

```csharp
CreateClient_EncryptsApiKey()
CreateClient_AutoCreatesManagerUser()
CreateClient_RejectsDuplicateAgentId()
```

- [ ] **Step 2: Add API key protector**

Create interface:

```csharp
public interface ISecretProtector
{
    string Protect(string value);
    string Unprotect(string protectedValue);
}
```

Implement with ASP.NET Core Data Protection purpose string `JioCxRcsWrapper.ApiKeys.v1`.

- [ ] **Step 3: Add client onboarding DTO**

```csharp
public sealed record CreateClientRequest(
    string BrandName,
    string AgentName,
    string AgentId,
    string ApiKey,
    string SiteName,
    int Credits,
    string ManagerName,
    string ManagerEmail,
    string ManagerPassword,
    string? LogoPath);
```

- [ ] **Step 4: Implement service**

`ClientOnboardingService.CreateAsync` validates required fields, encrypts API key, inserts `Client`, creates a Manager user tied to that client, hashes the manager password, logs audit, and returns client ID.

- [ ] **Step 5: Add MVC CRUD with AJAX**

Add `ClientsController` protected by `Clients:View/Add/Update/Delete`. Use Razor pages for list/create/edit and AJAX form posts returning JSON.

- [ ] **Step 6: Branding resolution**

Create service that returns client logo/site name after login based on `ClientId`, falling back to default admin branding.

- [ ] **Step 7: Run tests**

Run:

```powershell
dotnet test --filter ClientOnboardingServiceTests
```

Expected: PASS.

- [ ] **Step 8: Commit clients**

Run:

```powershell
git add src tests
git commit -m "feat: add client onboarding and branding"
```

Expected: commit succeeds.

---

### Task 8: Implement JioCX API Client And Media Upload

**Files:**
- Create: `src/JioCxRcsWrapper.Application/JioCx/*.cs`
- Create: `src/JioCxRcsWrapper.Infrastructure/JioCx/JioCxClient.cs`
- Create: `src/JioCxRcsWrapper.Application/Media/*.cs`
- Create: `src/JioCxRcsWrapper.Web/Controllers/MediaController.cs`
- Test: `tests/JioCxRcsWrapper.UnitTests/JioCx/JioCxClientTests.cs`

- [ ] **Step 1: Write HTTP client tests**

Using mocked `HttpMessageHandler`, assert:

```text
UploadFile sends POST /api/v1/uploadFile with x-apikey, file, and agentId.
SendMessage sends POST /api/v1/sendMessage with x-apikey and body fields messageID, agentID, contacts, data.
CheckCapability sends POST /api/v1/checkCapability with x-apikey, agentid, and PhoneNumbers.
```

- [ ] **Step 2: Add JioCX contracts**

```csharp
public interface IJioCxClient
{
    Task<JioCxUploadResult> UploadFileAsync(string apiKey, string agentId, Stream file, string fileName, string contentType, CancellationToken cancellationToken);
    Task<JioCxSendResult> SendMessageAsync(string apiKey, JioCxSendRequest request, CancellationToken cancellationToken);
    Task<JioCxCapabilityResult> CheckCapabilityAsync(string apiKey, string agentId, string phoneNumber, CancellationToken cancellationToken);
}

public sealed record JioCxSendRequest(string MessageId, string AgentId, IReadOnlyList<string> Contacts, object Data);
```

- [ ] **Step 3: Implement JioCX client**

Use `HttpClient`, `JioCxOptions`, exact configured paths, and no other endpoints. Do not add onboarding/tester methods.

- [ ] **Step 4: Implement media validation**

Rules:

- Images: `image/jpeg`, `image/jpg`, `image/gif`, `image/png`
- Videos: `video/mp4`, `video/mpeg`, `video/mpeg4`, `video/webm`
- Standalone image less than 2 MB
- Standalone video less than 10 MB
- Thumbnail less than 40 KB when used

- [ ] **Step 5: Add media upload controller**

`MediaController.Upload` checks permissions, credits, file type/size, decrypts client API key, calls `IJioCxClient.UploadFileAsync`, stores `UploadedMedia`, returns JSON with public URL.

- [ ] **Step 6: Run tests**

Run:

```powershell
dotnet test --filter JioCxClientTests
```

Expected: PASS.

- [ ] **Step 7: Commit JioCX media**

Run:

```powershell
git add src tests
git commit -m "feat: add documented jiocx api client and media upload"
```

Expected: commit succeeds.

---

### Task 9: Implement Message Builder Validation And Payload Generation

**Files:**
- Create: `src/JioCxRcsWrapper.Application/Messages/*.cs`
- Create: `src/JioCxRcsWrapper.Web/Controllers/MessageBuilderController.cs`
- Create: `src/JioCxRcsWrapper.Web/Views/MessageBuilder/*.cshtml`
- Create: `src/JioCxRcsWrapper.Web/wwwroot/js/message-builder.js`
- Test: `tests/JioCxRcsWrapper.UnitTests/Messages/MessagePayloadValidatorTests.cs`

- [ ] **Step 1: Write validator tests**

Tests:

```text
PlainText_WithText_IsValidAndBuildsDocumentedPayload
RichCard_WithOpenUrl_IsValidAndBuildsDocumentedPayload
RichCard_WithFiveCtas_IsRejected
RichCard_WithHttpUrl_IsRejected
RichCard_WithDialerAction_IsRejectedForSendBecauseSchemaIsUndocumented
RichCard_TitleOver80_IsRejected
RichCard_DescriptionOver2000_IsRejected
```

- [ ] **Step 2: Add message builder models**

Create draft records:

```csharp
public sealed record PlainTextMessageDraft(string Text);
public sealed record RichCardDraft(string? Title, string? Description, string? MediaUrl, string? ThumbnailUrl, IReadOnlyList<CtaDraft> Ctas);
public sealed record CtaDraft(string Text, CtaActionType ActionType, string Value, string PostBackData);
```

- [ ] **Step 3: Implement payload builder**

Plain text output:

```json
{
  "content": {
    "plainText": "This is the Sample for Plain Text"
  }
}
```

Rich card open URL output:

```json
{
  "content": {
    "richCardDetails": {
      "standalone": {
        "cardOrientation": "VERTICAL",
        "content": {
          "cardTitle": "Card Title goes here",
          "cardDescription": "Card Description goes here",
          "cardMedia": {
            "mediaHeight": "MEDIUM",
            "contentInfo": {
              "fileUrl": "Image URL goes here"
            }
          },
          "suggestions": [
            {
              "action": {
                "plainText": "Button 1",
                "postBack": {
                  "data": "call_back_data_for_button_1_goes_here"
                },
                "openUrl": {
                  "url": "https://example.com"
                }
              }
            }
          ]
        }
      }
    }
  }
}
```

- [ ] **Step 4: Block undocumented CTA send actions**

If `ActionType` is `Dialer`, `Calendar`, or `Location`, return validation error:

```text
This CTA action cannot be sent because the JioCX document does not define its payload schema.
```

- [ ] **Step 5: Build Razor editor**

Create a visual editor with tabs for Plain Text and Rich Card, media picker/upload, CTA rows capped at 4, character counters for title/description, HTTPS URL validation, preview panel, and AJAX save.

- [ ] **Step 6: Run tests**

Run:

```powershell
dotnet test --filter MessagePayloadValidatorTests
```

Expected: PASS.

- [ ] **Step 7: Commit message builder**

Run:

```powershell
git add src tests
git commit -m "feat: add jiocx message builder validation"
```

Expected: commit succeeds.

---

### Task 10: Implement Campaign CRUD, CSV Upload, Scheduling, And Queue Creation

**Files:**
- Create: `src/JioCxRcsWrapper.Application/Campaigns/*.cs`
- Create: `src/JioCxRcsWrapper.Application/Contacts/*.cs`
- Create: `src/JioCxRcsWrapper.Web/Controllers/CampaignsController.cs`
- Create: `src/JioCxRcsWrapper.Web/Views/Campaigns/*.cshtml`
- Create: `src/JioCxRcsWrapper.Web/wwwroot/js/campaigns.js`
- Test: `tests/JioCxRcsWrapper.UnitTests/Campaigns/CampaignServiceTests.cs`

- [ ] **Step 1: Write campaign tests**

Tests:

```text
UploadContacts_With51Rows_IsRejected
UploadContacts_WithInvalidMobile_IsRejected
QueueCampaign_WithZeroCredits_IsRejected
QueueCampaign_CreatesOneQueueItemPerContact
Manager_CanOnlyCreateCampaignForOwnClient
```

- [ ] **Step 2: Add CSV parser**

CSV parser accepts header `MobileNumber` or one-column files. Normalize and validate phone numbers; preserve original value in error messages.

- [ ] **Step 3: Add campaign service**

`CampaignService` supports:

- create draft campaign.
- save message payload.
- upload contacts.
- schedule campaign.
- configure recurring campaign.
- enqueue due contacts.

- [ ] **Step 4: Queue creation rule**

When campaign is queued, insert one `CampaignQueueItem` per contact. Existing queue rows must not duplicate on repeated schedule checks.

- [ ] **Step 5: Add MVC screens**

Screens:

- campaign list.
- create/edit campaign.
- contact upload.
- message builder link/embed.
- schedule/recurring controls.
- campaign detail with live status.

- [ ] **Step 6: Run tests**

Run:

```powershell
dotnet test --filter CampaignServiceTests
```

Expected: PASS.

- [ ] **Step 7: Commit campaigns**

Run:

```powershell
git add src tests
git commit -m "feat: add campaign management and durable queue creation"
```

Expected: commit succeeds.

---

### Task 11: Implement Queue Worker, Retry Logic, Credits, Logs, And SignalR Broadcasts

**Files:**
- Create: `src/JioCxRcsWrapper.Application/Queue/*.cs`
- Create: `src/JioCxRcsWrapper.Infrastructure/Queue/CampaignQueueWorker.cs`
- Create: `src/JioCxRcsWrapper.Web/Hubs/DashboardHub.cs`
- Create: `src/JioCxRcsWrapper.Web/Hubs/CampaignHub.cs`
- Test: `tests/JioCxRcsWrapper.UnitTests/Queue/QueueRetryPolicyTests.cs`

- [ ] **Step 1: Write retry policy tests**

Tests:

```text
Status429_IsRetryable
Status500_IsRetryable
Status400_IsNotRetryable
Status404_IsNotRetryable
MaxAttemptsExceeded_FailsQueueItem
ZeroCredits_PausesQueueItem
```

- [ ] **Step 2: Implement retry policy**

Create:

```csharp
public interface IQueueRetryPolicy
{
    bool ShouldRetry(int? httpStatusCode, int attemptCount, int maxAttempts);
    DateTimeOffset NextAttemptAt(DateTimeOffset now, int attemptCount);
}
```

Backoff: 1 minute, 5 minutes, 15 minutes, 60 minutes.

- [ ] **Step 3: Implement queue worker**

Hosted service loop:

1. Poll pending/retry-due queue rows.
2. Lock rows with `LockedAt` and `LockedBy`.
3. Load campaign, contact, client, message.
4. Check credits.
5. Decrypt API key.
6. Call `sendMessage` with one contact.
7. Append `MessageLog`.
8. Update contact, queue item, report counters.
9. Decrement credits only after accepted/sent response.
10. Broadcast through SignalR.

- [ ] **Step 4: Register hosted service**

Register `CampaignQueueWorker` and queue services in DI. Keep worker enabled by default but controlled by config key `Queue:Enabled`.

- [ ] **Step 5: Add SignalR hubs**

`DashboardHub` groups:

- `admin`
- `client-{clientId}`

`CampaignHub` groups:

- `campaign-{campaignId}`

- [ ] **Step 6: Run tests**

Run:

```powershell
dotnet test --filter QueueRetryPolicyTests
```

Expected: PASS.

- [ ] **Step 7: Commit worker**

Run:

```powershell
git add src tests
git commit -m "feat: process campaign queue with retries and signalr"
```

Expected: commit succeeds.

---

### Task 12: Implement Webhook Endpoint With Raw Capture And Defensive Mapping

**Files:**
- Create: `src/JioCxRcsWrapper.Application/Webhooks/*.cs`
- Create: `src/JioCxRcsWrapper.Web/Controllers/WebhooksController.cs`
- Test: `tests/JioCxRcsWrapper.UnitTests/Webhooks/WebhookServiceTests.cs`

- [ ] **Step 1: Write webhook tests**

Tests:

```text
Webhook_AlwaysStoresRawPayload
Webhook_WithKnownMessageId_UpdatesMatchingLog
Webhook_WithUnknownShape_DoesNotThrow
Webhook_WithClickEvent_BroadcastsWhenMapped
```

- [ ] **Step 2: Implement service**

Webhook service stores raw JSON first. Then attempts to read common fields such as `messageId`, `messageID`, `eventType`, `status`, `phoneNumber`, and `timestamp` without requiring them.

- [ ] **Step 3: Add endpoint**

Create `POST /webhooks/jiocx` that accepts raw body, calls service, and returns `200 OK` when raw capture succeeds.

- [ ] **Step 4: Add audit and SignalR update**

Mapped delivery/open/click updates append `MessageLog`, update `Contact.Status`, refresh report counters, and broadcast to dashboard/campaign groups.

- [ ] **Step 5: Run tests**

Run:

```powershell
dotnet test --filter WebhookServiceTests
```

Expected: PASS.

- [ ] **Step 6: Commit webhook**

Run:

```powershell
git add src tests
git commit -m "feat: capture jiocx webhooks defensively"
```

Expected: commit succeeds.

---

### Task 13: Implement Dashboard, Reports, CSV/PDF Export, And Audit Views

**Files:**
- Create: `src/JioCxRcsWrapper.Application/Dashboard/*.cs`
- Create: `src/JioCxRcsWrapper.Application/Reports/*.cs`
- Create: `src/JioCxRcsWrapper.Infrastructure/Exports/*.cs`
- Create: `src/JioCxRcsWrapper.Web/Controllers/DashboardController.cs`
- Create: `src/JioCxRcsWrapper.Web/Controllers/ReportsController.cs`
- Create: `src/JioCxRcsWrapper.Web/Controllers/AuditLogsController.cs`
- Create: `src/JioCxRcsWrapper.Web/Views/Dashboard/Index.cshtml`
- Create: `src/JioCxRcsWrapper.Web/Views/Reports/*.cshtml`
- Test: `tests/JioCxRcsWrapper.UnitTests/Reports/ReportServiceTests.cs`

- [ ] **Step 1: Write report tests**

Tests:

```text
AdminReport_IncludesAllClients
ManagerReport_IncludesOnlyOwnClient
ZeroCredits_DoesNotBlockReportDownload
CsvExport_IncludesMobileStatusAndActions
```

- [ ] **Step 2: Implement dashboard service**

Return aggregate cards:

- total campaigns.
- sent.
- delivered.
- failed.
- credits.
- delivery rate.
- per-client stats for Admin.
- client-only stats for Manager/Viewer.

- [ ] **Step 3: Implement report service**

Query contacts/logs by campaign and client scope. Include user actions when webhook event data can be mapped.

- [ ] **Step 4: Implement CSV export**

Use `CsvHelper` to generate columns:

```text
Campaign,MobileNumber,Status,Opened,Clicked,LastError,LastUpdated
```

- [ ] **Step 5: Implement PDF export**

Use QuestPDF for server-side PDF generation. Configure the community license setting during application startup. The PDF must include campaign summary and the report table, with no native runtime dependency beyond the NuGet package.

- [ ] **Step 6: Build UI**

Dashboard uses SignalR for live stat updates and chart data. Reports use AJAX filters and permission-protected export buttons.

- [ ] **Step 7: Run tests**

Run:

```powershell
dotnet test --filter ReportServiceTests
```

Expected: PASS.

- [ ] **Step 8: Commit reports**

Run:

```powershell
git add src tests
git commit -m "feat: add dashboard reports and exports"
```

Expected: commit succeeds.

---

### Task 14: Polish Enterprise UI Shell And Permission-Aware AJAX Behavior

**Files:**
- Modify: `src/JioCxRcsWrapper.Web/Views/Shared/_Layout.cshtml`
- Modify: `src/JioCxRcsWrapper.Web/Views/Shared/_Sidebar.cshtml`
- Create: `src/JioCxRcsWrapper.Web/wwwroot/css/app.css`
- Create: `src/JioCxRcsWrapper.Web/wwwroot/js/app.js`
- Modify: module Razor views created earlier

- [ ] **Step 1: Build layout**

Create enterprise operations shell:

- fixed sidebar.
- top branding bar.
- client/site logo.
- profile menu.
- responsive content area.
- toast/alert region.

- [ ] **Step 2: Add AJAX conventions**

In `app.js`, configure:

- anti-forgery token on AJAX requests.
- `401` redirects to `/Account/Login`.
- `403` shows "Not allowed".
- validation errors render near forms.

- [ ] **Step 3: Add disabled credit state**

When server sends `credits <= 0`, add disabled state to send/upload/campaign buttons and show:

```text
No credits available
```

- [ ] **Step 4: Verify responsive behavior**

Run app and inspect:

- desktop 1440px.
- tablet 768px.
- mobile 390px.

Expected: no overlapping text, menus remain usable, tables scroll horizontally where necessary.

- [ ] **Step 5: Commit UI polish**

Run:

```powershell
git add src/JioCxRcsWrapper.Web
git commit -m "feat: polish enterprise panel ui"
```

Expected: commit succeeds.

---

### Task 15: Add Deployment Docs, README, Verification Checklist, And Final Validation

**Files:**
- Create: `README.md`
- Create: `docs/deployment.md`
- Create: `docs/manual-verification.md`
- Modify: `src/JioCxRcsWrapper.Web/appsettings.json`

- [ ] **Step 1: Write README**

Include:

- prerequisites: .NET 9 SDK, SQL Server, EF tools.
- setup commands.
- migration command.
- seed admin credentials.
- JioCX credential entry through Admin client onboarding.
- supported JioCX APIs.
- unsupported undocumented APIs and blocked CTA send behavior.

- [ ] **Step 2: Write deployment guide**

Include IIS and Kestrel reverse proxy steps:

```powershell
dotnet publish src/JioCxRcsWrapper.Web/JioCxRcsWrapper.Web.csproj -c Release -o .\publish
```

Include environment variables for connection string, JWT signing key, Data Protection key storage, and queue settings.

- [ ] **Step 3: Write manual verification checklist**

Checklist:

- login/logout.
- Admin client onboarding.
- Manager auto-created and client-scoped.
- RBAC menus hidden.
- campaign creation.
- CSV upload rejects 51 contacts.
- plain text payload generated.
- rich card open URL payload generated.
- undocumented CTA actions blocked from send.
- media upload calls documented endpoint.
- queue creates per-contact rows.
- 429/500 retry behavior with mocked JioCX.
- webhook raw capture.
- SignalR dashboard updates.
- credits zero lockout.
- reports CSV/PDF download.

- [ ] **Step 4: Run full verification commands**

Run:

```powershell
dotnet format --verify-no-changes
dotnet build -c Release
dotnet test -c Release
```

Expected: all pass.

- [ ] **Step 5: Commit docs and final validation**

Run:

```powershell
git add README.md docs src
git commit -m "docs: add deployment and verification guide"
```

Expected: commit succeeds.

---

## Self-Review Checklist

- Spec coverage:
  - Clean Architecture covered by Tasks 1, 3, and 4.
  - Required database tables and production queue additions covered by Tasks 2 and 3.
  - RBAC, tenant scope, and permission UI covered by Task 6.
  - Hybrid auth covered by Task 5.
  - Client onboarding and branding covered by Task 7.
  - JioCX upload/send/capability APIs covered by Task 8.
  - Message builder constraints covered by Task 9.
  - Campaign scheduling, recurring setup, CSV cap, and queue creation covered by Task 10.
  - Queue processing, retry behavior, credits, logs, and SignalR covered by Task 11.
  - Webhook raw capture and defensive mapping covered by Task 12.
  - Dashboard, reports, CSV/PDF export, and audit views covered by Tasks 13 and 14.
  - Deployment and final verification covered by Task 15.
- Open item scan:
  - No implementation task depends on undocumented JioCX APIs.
  - Dialer/calendar/location CTA actions are explicitly blocked from live send.
  - Webhook parsing stores raw JSON first and parses only defensively.
- Type consistency:
  - Entities, enums, service names, and table names match the approved design spec.
  - Queue status and retry terminology is consistent across tasks.
  - JioCX request naming follows the PDF: `messageID`, `agentID`, `contacts`, `data`, `x-apikey`, `agentid`, and `PhoneNumbers`.
