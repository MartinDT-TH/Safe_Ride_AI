using SafeRide.Domain.Entities;

namespace SafeRide.Application.Common.Interfaces;

public interface IBookingRepository
{
    Task AddAsync(Booking booking, CancellationToken cancellationToken);

    Task<Booking?> GetCustomerBookingAsync(
        long bookingId,
        Guid customerId,
        CancellationToken cancellationToken);

    Task<Vehicle?> GetCustomerVehicleAsync(
        long vehicleId,
        Guid customerId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Vehicle>> GetCustomerVehiclesAsync(
        Guid customerId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PricingRule>> GetBookablePricingRulesAsync(
        Guid customerId,
        CancellationToken cancellationToken);

    Task<PricingRule?> GetPricingRuleAsync(
        long serviceTypeId,
        long vehicleId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Booking>> GetScheduledBookingsReadyForMatchingAsync(
        DateTime matchingCutoffUtc,
        CancellationToken cancellationToken);
}
