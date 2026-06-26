using SafeRide.Domain.Entities;
using SafeRide.Application.Features.Bookings.DTOs;
using SafeRide.Application.Common.Models;

namespace SafeRide.Application.Common.Interfaces;

public interface IBookingRepository
{
    Task AddAsync(Booking booking, CancellationToken cancellationToken);

    Task<Booking?> GetCustomerBookingAsync(
        long bookingId,
        Guid customerId,
        CancellationToken cancellationToken);

    Task<Booking?> GetCustomerBookingWithDetailsAsync(
        long bookingId,
        Guid customerId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BookingHistoryItemDto>> GetCustomerBookingHistoryAsync(
        Guid customerId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<BookingHistoryItemDto>> GetDriverBookingHistoryAsync(
        Guid driverId,
        CancellationToken cancellationToken);

    Task<Booking?> GetActiveNowBookingAsync(
        Guid customerId,
        CancellationToken cancellationToken);

    Task<BookingDriverOfferDto?> GetLatestBookingDriverOfferAsync(
        long bookingId,
        CancellationToken cancellationToken);

    Task<LocationPoint?> GetDriverLocationAsync(
        Guid driverId,
        CancellationToken cancellationToken);

    Task ExpireStaleNowBookingsAsync(
        Guid customerId,
        DateTime utcNow,
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

    Task CancelActiveDriverOffersAsync(
        long bookingId,
        DateTime cancelledAt,
        CancellationToken cancellationToken);

    Task<bool> CancelAssignedTripAsync(
        long bookingId,
        Guid cancelledByUserId,
        string? reason,
        DateTime cancelledAt,
        CancellationToken cancellationToken);
}
