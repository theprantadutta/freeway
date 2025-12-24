using Freeway.Domain.Interfaces;

namespace Freeway.Infrastructure.Services;

public class DateTimeService : IDateTimeService
{
    public DateTime UtcNow => DateTime.UtcNow;
}
