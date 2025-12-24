using Freeway.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Freeway.Domain.Interfaces;

public interface IAppDbContext
{
    DbSet<Project> Projects { get; }
    DbSet<UsageLog> UsageLogs { get; }
    DbSet<ProviderBenchmark> ProviderBenchmarks { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
