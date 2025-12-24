using Freeway.Domain.Interfaces;
using Freeway.Infrastructure.Jobs;
using Freeway.Infrastructure.Persistence;
using Freeway.Infrastructure.Services;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Freeway.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Build connection string
        var connectionString = BuildConnectionString(configuration);

        // Configure Npgsql data source with dynamic JSON support
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();

        // Add DbContext
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(dataSource, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
            });
        });

        // Register IAppDbContext
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        // Add HttpClient for OpenRouter
        services.AddHttpClient<IOpenRouterService, OpenRouterService>();

        // Register services
        services.AddSingleton<IDateTimeService, DateTimeService>();
        services.AddSingleton<IApiKeyService, ApiKeyService>();
        services.AddSingleton<IModelCacheService, ModelCacheService>();
        services.AddSingleton<IProjectCacheService, ProjectCacheService>();

        // Register background job service
        services.AddScoped<IBackgroundJobService, BackgroundJobService>();

        // Configure Hangfire
        services.AddHangfire(config =>
        {
            config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180);
            config.UseSimpleAssemblyNameTypeSerializer();
            config.UseRecommendedSerializerSettings();
            config.UsePostgreSqlStorage(options =>
            {
                options.UseNpgsqlConnection(connectionString);
            }, new PostgreSqlStorageOptions
            {
                SchemaName = configuration["Hangfire:SchemaName"] ?? "freeway_hangfire",
                QueuePollInterval = TimeSpan.FromSeconds(15),
                PrepareSchemaIfNecessary = true
            });
        });

        services.AddHangfireServer();

        return services;
    }

    private static string BuildConnectionString(IConfiguration configuration)
    {
        // Check for full connection string first
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(connectionString))
        {
            return connectionString;
        }

        // Check for DATABASE_URL environment variable
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrEmpty(databaseUrl))
        {
            return databaseUrl;
        }

        // Build from individual environment variables
        var host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        var database = Environment.GetEnvironmentVariable("DB_NAME") ?? "freeway";
        var username = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "";

        return $"Host={host};Port={port};Database={database};Username={username};Password={password}";
    }
}
