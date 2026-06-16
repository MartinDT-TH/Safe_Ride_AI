namespace SafeRide.Application.Features.Bookings.Queries.GetBookingCatalog;

public sealed record GetBookingCatalogResult(
    IReadOnlyList<BookingServiceOptionResult> Services,
    IReadOnlyList<BookingVehicleOptionResult> Vehicles);

public sealed record BookingServiceOptionResult(
    long Id,
    string Name,
    string Mode,
    string Description);

public sealed record BookingVehicleOptionResult(
    long Id,
    string Name,
    string PlateNumber,
    string Color,
    bool IsMotorbike);
