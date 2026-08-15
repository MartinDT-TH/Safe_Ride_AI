namespace SafeRide.Contracts.Requests.Trips;

public sealed record ConfirmTripReturnRequest(
    bool VehicleReturnedConfirmed,
    int? RatingScore = null,
    string? Comment = null);
