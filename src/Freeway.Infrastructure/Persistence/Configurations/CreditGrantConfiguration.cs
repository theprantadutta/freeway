using Freeway.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freeway.Infrastructure.Persistence.Configurations;

public class CreditGrantConfiguration : IEntityTypeConfiguration<CreditGrant>
{
    public void Configure(EntityTypeBuilder<CreditGrant> builder)
    {
        builder.ToTable("credit_grants");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Provider)
            .HasColumnName("provider")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.AmountUsd)
            .HasColumnName("amount_usd")
            .HasPrecision(20, 10)
            .IsRequired();

        builder.Property(x => x.StartsAt)
            .HasColumnName("starts_at")
            .IsRequired();

        builder.Property(x => x.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(x => x.Source)
            .HasColumnName("source")
            .HasMaxLength(20)
            .HasDefaultValue("auto");

        builder.Property(x => x.Note)
            .HasColumnName("note");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()");

        builder.HasIndex(x => new { x.Provider, x.StartsAt })
            .HasDatabaseName("ix_credit_grants_provider_starts_at");
    }
}
