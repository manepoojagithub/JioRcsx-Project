using JioCxRcsWrapper.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JioCxRcsWrapper.Infrastructure.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

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
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        SeedData.Apply(modelBuilder);
    }
}
