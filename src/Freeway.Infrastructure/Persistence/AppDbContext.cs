using Freeway.Domain.Entities;
using Freeway.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Freeway.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<UsageLog> UsageLogs => Set<UsageLog>();
    public DbSet<ProviderBenchmark> ProviderBenchmarks => Set<ProviderBenchmark>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
