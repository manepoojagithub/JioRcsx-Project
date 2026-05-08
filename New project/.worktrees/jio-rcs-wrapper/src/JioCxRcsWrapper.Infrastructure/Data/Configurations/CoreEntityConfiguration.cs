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
        builder.Property(x => x.Credits).HasDefaultValue(0);
        builder.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(80).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
    }
}

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(80).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();
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
        builder.Property(x => x.LogoPath).HasMaxLength(500);
        builder.Property(x => x.SiteName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Credits).HasDefaultValue(0);
        builder.Property(x => x.CreditCostPerMessage).HasDefaultValue(1);
        builder.Property(x => x.LowCreditThreshold).HasDefaultValue(10);
        builder.HasIndex(x => x.AgentId);
        builder.HasIndex(x => x.BrandName).IsUnique();
    }
}

public sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.IsRCSEnabled).HasDefaultValue(true);
        builder.HasIndex(x => new { x.ClientId, x.Status });
    }
}

public sealed class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.Property(x => x.MobileNumber).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.CampaignId, x.MobileNumber });
    }
}

public sealed class CampaignQueueItemConfiguration : IEntityTypeConfiguration<CampaignQueueItem>
{
    public void Configure(EntityTypeBuilder<CampaignQueueItem> builder)
    {
        builder.HasIndex(x => new { x.Status, x.NextAttemptAt });
        builder.HasIndex(x => new { x.CampaignId, x.ContactId }).IsUnique();
        builder.Property(x => x.LockedBy).HasMaxLength(100);
        builder.Property(x => x.LastError).HasMaxLength(2000);
    }
}
