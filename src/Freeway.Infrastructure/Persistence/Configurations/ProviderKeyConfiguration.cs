using Freeway.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freeway.Infrastructure.Persistence.Configurations;

public class ProviderKeyConfiguration : IEntityTypeConfiguration<ProviderKey>
{
    public void Configure(EntityTypeBuilder<ProviderKey> builder)
    {
        builder.ToTable("provider_keys");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Provider)
            .HasColumnName("provider")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Fingerprint)
            .HasColumnName("fingerprint")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Hint)
            .HasColumnName("hint")
            .HasMaxLength(40)
            .IsRequired();

        builder.Property(x => x.FirstSeenAt).HasColumnName("first_seen_at");
        builder.Property(x => x.LastSeenAt).HasColumnName("last_seen_at");
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");

        builder.Property(x => x.ExpirySource)
            .HasColumnName("expiry_source")
            .HasMaxLength(20)
            .HasDefaultValue("estimated");

        builder.Property(x => x.LastRejectedAt).HasColumnName("last_rejected_at");
        builder.Property(x => x.LastRejection).HasColumnName("last_rejection");
        builder.Property(x => x.WarnedAt).HasColumnName("warned_at");
        builder.Property(x => x.ExpiredNoticeAt).HasColumnName("expired_notice_at");
        builder.Property(x => x.RejectedNoticeAt).HasColumnName("rejected_notice_at");

        builder.HasIndex(x => new { x.Provider, x.Fingerprint })
            .IsUnique()
            .HasDatabaseName("ix_provider_keys_provider_fingerprint");
    }
}
