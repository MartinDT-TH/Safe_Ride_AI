using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Repositories;

public sealed class BookingRepository : IBookingRepository
{
    private readonly ApplicationDbContext _dbContext;

    public BookingRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        Booking booking,
        CancellationToken cancellationToken)
    {
        await _dbContext.Bookings.AddAsync(booking, cancellationToken);
    }

    public Task<Booking?> GetCustomerBookingAsync(
        long bookingId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Bookings
            .FirstOrDefaultAsync(
                booking => booking.BookingId == bookingId
                    && booking.CustomerId == customerId,
                cancellationToken);
    }
    
    public Task<Vehicle?> GetCustomerVehicleAsync(
        long vehicleId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                vehicle => vehicle.Id == vehicleId
                    && vehicle.OwnerUserId == customerId
                    && !vehicle.IsDeleted,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Vehicle>> GetCustomerVehiclesAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Vehicles
            .AsNoTracking()
            .Where(vehicle => vehicle.OwnerUserId == customerId
                && !vehicle.IsDeleted)
            .OrderBy(vehicle => vehicle.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PricingRule>> GetBookablePricingRulesAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        return await (
            from pricingRule in _dbContext.PricingRules
                .AsNoTracking()
                .Include(rule => rule.ServiceType)
            join vehicle in _dbContext.Vehicles.AsNoTracking()
                on pricingRule.VehicleClass equals vehicle.RequiredLicenseClass
            where vehicle.OwnerUserId == customerId
                && !vehicle.IsDeleted
                && pricingRule.IsActive
            select pricingRule)
            .ToListAsync(cancellationToken);
    }

    public Task<PricingRule?> GetPricingRuleAsync(
        long serviceTypeId,
        long vehicleId,
        CancellationToken cancellationToken)
    {
        return (
            from pricingRule in _dbContext.PricingRules.AsNoTracking()
            join vehicle in _dbContext.Vehicles.AsNoTracking()
                on pricingRule.VehicleClass equals vehicle.RequiredLicenseClass
            where pricingRule.ServiceTypeId == serviceTypeId
                && vehicle.Id == vehicleId
                && pricingRule.IsActive
            select pricingRule)
            .OrderByDescending(pricingRule => pricingRule.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Booking>> GetScheduledBookingsReadyForMatchingAsync(
        DateTime matchingCutoffUtc,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Bookings
            .Where(booking => booking.BookingType == BookingType.Scheduled
                && booking.BookingStatus == BookingStatus.PendingSchedule
                && booking.ScheduledAt <= matchingCutoffUtc)
            .OrderBy(booking => booking.ScheduledAt)
            .ToListAsync(cancellationToken);
    }

    public async Task CancelActiveDriverOffersAsync(
        long bookingId,
        DateTime cancelledAt,
        CancellationToken cancellationToken)
    {
        var offers = await _dbContext.BookingDriverOffers
            .Where(offer => offer.BookingId == bookingId
                && offer.OfferStatus == DriverOfferStatus.Offered)
            .ToListAsync(cancellationToken);

        foreach (var offer in offers)
        {
            offer.OfferStatus = DriverOfferStatus.Cancelled;
            offer.CancelledAt = cancelledAt;
        }
    }
}
