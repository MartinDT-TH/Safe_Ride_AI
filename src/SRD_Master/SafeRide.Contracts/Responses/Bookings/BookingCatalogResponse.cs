namespace SafeRide.Contracts.Responses.Bookings;

public sealed record BookingCatalogResponse(
    IReadOnlyList<BookingServiceOptionResponse> Services,
    IReadOnlyList<BookingVehicleOptionResponse> Vehicles);

public sealed record BookingServiceOptionResponse(
    long Id,
    string Name,
    string Mode,
    string Description);

public sealed record BookingVehicleOptionResponse(
    long Id,
    string Name,
    string PlateNumber,
    string Color,
    bool IsMotorbike);
