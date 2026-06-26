using SafeRide.Application.Common.Models;
using SafeRide.Domain.Entities;

namespace SafeRide.Application.Common.Interfaces;

public interface IMatchingPolicyProvider
{
    MatchingOptions Current { get; }

    DateTime? GetMatchingStartedAt(Booking booking);

    BookingMatchingSnapshot GetSnapshot(Booking booking, DateTime utcNow);
}
