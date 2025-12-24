using Freeway.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Freeway.Infrastructure.Persistence.Configurations;

public class UsageLogConfiguration : IEntityTypeConfiguration<UsageLog>
{
    public void Configure(EntityTypeBuilder<UsageLog> builder)
    {
        builder.ToTable("usage_logs");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(u => u.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(u => u.ModelId)
            .HasColumnName("model_id")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(u => u.ModelType)
            .HasColumnName("model_type")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(u => u.InputTokens)
            .HasColumnName("input_tokens")
            .HasDefaultValue(0);

        builder.Property(u => u.OutputTokens)
            .HasColumnName("output_tokens")
            .HasDefaultValue(0);

        builder.Property(u => u.ResponseTimeMs)
            .HasColumnName("response_time_ms");

        builder.Property(u => u.CostUsd)
            .HasColumnName("cost_usd")
            .HasPrecision(20, 10)
            .HasDefaultValue(0m);

        builder.Property(u => u.PromptCostPerToken)
            .HasColumnName("prompt_cost_per_token")
            .HasPrecision(20, 15);

        builder.Property(u => u.CompletionCostPerToken)
            .HasColumnName("completion_cost_per_token")
            .HasPrecision(20, 15);

        builder.Property(u => u.Success)
            .HasColumnName("success")
            .HasDefaultValue(true);

        builder.Property(u => u.ErrorMessage)
            .HasColumnName("error_message");

        builder.Property(u => u.RequestId)
            .HasColumnName("request_id")
            .HasMaxLength(255);

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(u => u.Provider)
            .HasColumnName("provider")
            .HasMaxLength(50);

        builder.Property(u => u.RequestMessages)
            .HasColumnName("request_messages")
            .HasColumnType("jsonb");

        builder.Property(u => u.ResponseContent)
            .HasColumnName("response_content");

        builder.Property(u => u.FinishReason)
            .HasColumnName("finish_reason")
            .HasMaxLength(50);

        builder.Property(u => u.RequestParams)
            .HasColumnName("request_params")
            .HasColumnType("jsonb");

        // Indexes
        builder.HasIndex(u => u.ProjectId)
            .HasDatabaseName("ix_usage_logs_project_id");

        builder.HasIndex(u => u.CreatedAt)
            .HasDatabaseName("ix_usage_logs_created_at");

        builder.HasIndex(u => new { u.ProjectId, u.CreatedAt })
            .HasDatabaseName("ix_usage_logs_project_id_created_at");
    }
}
