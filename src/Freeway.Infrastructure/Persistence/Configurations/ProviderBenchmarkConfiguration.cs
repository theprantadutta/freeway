using Freeway.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freeway.Infrastructure.Persistence.Configurations;

public class ProviderBenchmarkConfiguration : IEntityTypeConfiguration<ProviderBenchmark>
{
    public void Configure(EntityTypeBuilder<ProviderBenchmark> builder)
    {
        builder.ToTable("provider_benchmarks");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.ProviderName)
            .HasColumnName("provider_name")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ModelId)
            .HasColumnName("model_id")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.ResponseTimeMs)
            .HasColumnName("response_time_ms")
            .IsRequired();

        builder.Property(x => x.Success)
            .HasColumnName("success")
            .IsRequired();

        builder.Property(x => x.ErrorMessage)
            .HasColumnName("error_message");

        builder.Property(x => x.ErrorCode)
            .HasColumnName("error_code");

        builder.Property(x => x.TestedAt)
            .HasColumnName("tested_at")
            .IsRequired();

        // Indexes for querying benchmark results
        builder.HasIndex(x => x.ProviderName)
            .HasDatabaseName("ix_provider_benchmarks_provider");

        builder.HasIndex(x => x.TestedAt)
            .HasDatabaseName("ix_provider_benchmarks_tested_at");

        builder.HasIndex(x => new { x.ProviderName, x.TestedAt })
            .HasDatabaseName("ix_provider_benchmarks_provider_tested_at");
    }
}
