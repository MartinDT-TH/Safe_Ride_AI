using SafeRide.Domain.Enums;

namespace SafeRide.Contracts.Requests.Trips;

public sealed record UpdateTripStatusRequest(
    TripStatus TripStatus);
