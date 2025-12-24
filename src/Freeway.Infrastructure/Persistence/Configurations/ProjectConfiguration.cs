using Freeway.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freeway.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(p => p.ApiKeyHash)
            .HasColumnName("api_key_hash")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(p => p.ApiKeyPrefix)
            .HasColumnName("api_key_prefix")
            .HasMaxLength(8)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(p => p.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("now()");

        builder.Property(p => p.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(p => p.RateLimitPerMinute)
            .HasColumnName("rate_limit_per_minute")
            .HasDefaultValue(60);

        builder.Property(p => p.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

        // Indexes
        builder.HasIndex(p => p.ApiKeyHash)
            .HasDatabaseName("ix_projects_api_key_hash")
            .IsUnique();

        builder.HasIndex(p => p.IsActive)
            .HasDatabaseName("ix_projects_is_active");

        // Navigation
        builder.HasMany(p => p.UsageLogs)
            .WithOne(u => u.Project)
            .HasForeignKey(u => u.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
