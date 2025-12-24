using System.Text.Json;
using DotNetEnv;
using Freeway.Api.Middleware;
using Freeway.Application;
using Freeway.Domain.Interfaces;
using Freeway.Infrastructure;
using Freeway.Infrastructure.Jobs;
using Hangfire;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

// Load .env file (search in current dir, then parent directories)
var envPath = FindEnvFile();
if (!string.IsNullOrEmpty(envPath))
{
    Env.Load(envPath);
}

static string? FindEnvFile()
{
    var currentDir = Directory.GetCurrentDirectory();

    // Check current directory
    var envFile = Path.Combine(currentDir, ".env");
    if (File.Exists(envFile)) return envFile;

    // Check parent directories (for when running from src/Freeway.Api)
    var dir = new DirectoryInfo(currentDir);
    while (dir.Parent != null)
    {
        dir = dir.Parent;
        envFile = Path.Combine(dir.FullName, ".env");
        if (File.Exists(envFile)) return envFile;

        // Stop at solution directory (contains .sln file)
        if (Directory.GetFiles(dir.FullName, "*.sln").Length > 0)
            break;
    }

    return null;
}

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Hangfire", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/freeway-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting Freeway API...");

    var builder = WebApplication.CreateBuilder(args);

    // Use Serilog
    builder.Host.UseSerilog();

    // Add services
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // Configure JSON serialization (snake_case for frontend compatibility)
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
        });

    // OpenAPI
    builder.Services.AddOpenApi();

    // CORS
    var allowedOrigins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS") ?? "*";
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            if (allowedOrigins == "*")
            {
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            }
            else
            {
                policy.WithOrigins(allowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            }
        });
    });

    var app = builder.Build();

    // Initialize caches on startup
    using (var scope = app.Services.CreateScope())
    {
        var modelCacheService = scope.ServiceProvider.GetRequiredService<IModelCacheService>();
        var projectCacheService = scope.ServiceProvider.GetRequiredService<IProjectCacheService>();

        Log.Information("Initializing model cache...");
        await modelCacheService.RefreshModelsAsync();

        Log.Information("Initializing project cache...");
        await projectCacheService.LoadCacheAsync();
    }

    // Configure middleware pipeline
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    app.UseCors();
    app.UseMiddleware<ApiKeyAuthenticationMiddleware>();

    // Hangfire Dashboard
    var hangfireUsername = Environment.GetEnvironmentVariable("HANGFIRE_USERNAME") ?? "admin";
    var hangfirePassword = Environment.GetEnvironmentVariable("HANGFIRE_PASSWORD") ?? "admin";

    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireDashboardAuthFilter(hangfireUsername, hangfirePassword) }
    });

    // Configure recurring jobs
    RecurringJob.AddOrUpdate<IBackgroundJobService>(
        "refresh-models",
        service => service.RefreshModelsAsync(),
        Cron.Daily(0, 0)); // Daily at midnight UTC

    RecurringJob.AddOrUpdate<IBackgroundJobService>(
        "refresh-project-cache",
        service => service.RefreshProjectCacheAsync(),
        Cron.Daily(1, 0)); // Daily at 1 AM UTC

    app.MapControllers();

    Log.Information("Freeway API started successfully");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
