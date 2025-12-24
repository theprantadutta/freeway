using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace Freeway.Infrastructure.Persistence;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Load .env file if it exists
        LoadEnvFile();

        var connectionString = BuildConnectionString();

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(dataSource, options =>
        {
            options.MigrationsHistoryTable("__EFMigrationsHistory", "public");
        });

        return new AppDbContext(optionsBuilder.Options);
    }

    private static void LoadEnvFile()
    {
        // Try to find .env file in various locations
        var currentDir = Directory.GetCurrentDirectory();
        var possiblePaths = new[]
        {
            Path.Combine(currentDir, ".env"),
            Path.GetFullPath(Path.Combine(currentDir, "..", ".env")),
            Path.GetFullPath(Path.Combine(currentDir, "..", "..", ".env")),
            Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", ".env")),
            Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", "..", ".env")),
            @"F:\Personal\MyProjects\freeway\.env" // Fallback to absolute path
        };

        var envFilePath = possiblePaths.FirstOrDefault(File.Exists);
        if (envFilePath == null) return;

        foreach (var line in File.ReadAllLines(envFilePath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;

            var parts = line.Split('=', 2);
            if (parts.Length == 2)
            {
                var key = parts[0].Trim();
                var value = parts[1].Trim();
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    private static string BuildConnectionString()
    {
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrEmpty(databaseUrl))
        {
            return databaseUrl;
        }

        var host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        var database = Environment.GetEnvironmentVariable("DB_NAME") ?? "freeway";
        var username = Environment.GetEnvironmentVariable("DB_USER") ?? "postgres";
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? "";

        return $"Host={host};Port={port};Database={database};Username={username};Password={password}";
    }
}
