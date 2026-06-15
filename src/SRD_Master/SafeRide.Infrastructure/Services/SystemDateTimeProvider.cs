using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Infrastructure.Services;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
