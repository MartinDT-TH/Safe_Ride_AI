using SafeRide.Domain.Enums;

namespace SafeRide.Contracts.Requests.Trips;

public sealed record EndTripRequest(TripEndReason Reason);
