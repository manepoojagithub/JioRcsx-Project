using JioCxRcsWrapper.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JioCxRcsWrapper.Infrastructure.Data.Configurations;

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.Property(x => x.Module).HasMaxLength(100).IsRequired();
        builder.HasOne(x => x.Role).WithMany(x => x.RolePermissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Permission).WithMany(x => x.RolePermissions).HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
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

public sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.HasIndex(x => x.CampaignId);
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.Property(x => x.Action).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Module).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.Timestamp });
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

public sealed class ClientBrandingSettingConfiguration : IEntityTypeConfiguration<ClientBrandingSetting>
{
    public void Configure(EntityTypeBuilder<ClientBrandingSetting> builder)
    {
        builder.Property(x => x.SiteName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.LogoPath).HasMaxLength(500);
        builder.HasIndex(x => new { x.ClientId, x.IsDefault });
    }
}

public sealed class UploadedMediaConfiguration : IEntityTypeConfiguration<UploadedMedia>
{
    public void Configure(EntityTypeBuilder<UploadedMedia> builder)
    {
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LocalPath).HasMaxLength(500);
        builder.Property(x => x.PublicUrl).HasMaxLength(2000).IsRequired();
        builder.HasIndex(x => new { x.ClientId, x.CreatedAt });
    }
}

public sealed class MessageTemplateConfiguration : IEntityTypeConfiguration<MessageTemplate>
{
    public void Configure(EntityTypeBuilder<MessageTemplate> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(160).IsRequired();
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.Property(x => x.LocalMediaPath).HasMaxLength(500);
        builder.Property(x => x.RcsMediaUrl).HasMaxLength(2000);
        builder.Property(x => x.MediaContentType).HasMaxLength(100);
        builder.HasIndex(x => new { x.ClientId, x.Name });
    }
}
