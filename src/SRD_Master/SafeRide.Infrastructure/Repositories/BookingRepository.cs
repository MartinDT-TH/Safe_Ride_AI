using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;
using System.Text.Json;

namespace SafeRide.Infrastructure.Repositories;

public sealed class BookingRepository : IBookingRepository
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IRedisService _redisService;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public BookingRepository(
        ApplicationDbContext dbContext,
        IRedisService redisService)
    {
        _dbContext = dbContext;
        _redisService = redisService;
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
        var customerVehicleClasses = await _dbContext.Vehicles
            .AsNoTracking()
            .Where(vehicle => vehicle.OwnerUserId == customerId
                && !vehicle.IsDeleted)
            .Select(vehicle => vehicle.RequiredLicenseClass)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (customerVehicleClasses.Count == 0)
        {
            return [];
        }

        var activeRules = await GetActivePricingRulesAsync(cancellationToken);
        return activeRules
            .Where(rule => customerVehicleClasses.Contains(rule.VehicleClass))
            .ToList();
    }

    public async Task<PricingRule?> GetPricingRuleAsync(
        long serviceTypeId,
        long vehicleId,
        CancellationToken cancellationToken)
    {
        var vehicleClass = await _dbContext.Vehicles
            .AsNoTracking()
            .Where(vehicle => vehicle.Id == vehicleId)
            .Select(vehicle => (RequiredLicenseClass?)vehicle.RequiredLicenseClass)
            .FirstOrDefaultAsync(cancellationToken);
        if (!vehicleClass.HasValue)
        {
            return null;
        }

        var activeRules = await GetActivePricingRulesAsync(cancellationToken);
        return activeRules
            .Where(rule => rule.ServiceTypeId == serviceTypeId
                && rule.VehicleClass == vehicleClass.Value)
            .OrderByDescending(pricingRule => pricingRule.CreatedAt)
            .FirstOrDefault();
    }

    private async Task<IReadOnlyList<PricingRule>> GetActivePricingRulesAsync(
        CancellationToken cancellationToken)
    {
        var cached = await _redisService.GetAsync(RedisKeys.ActivePricingRules);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            try
            {
                var cachedItems = JsonSerializer.Deserialize<List<PricingRuleCacheItem>>(
                    cached,
                    JsonOptions);
                if (cachedItems is not null)
                {
                    return cachedItems
                        .Select(item => item.ToEntity())
                        .ToList();
                }
            }
            catch (JsonException)
            {
                await _redisService.RemoveAsync(RedisKeys.ActivePricingRules);
            }
        }

        var rules = await _dbContext.PricingRules
            .AsNoTracking()
            .Include(rule => rule.ServiceType)
            .Where(rule => rule.IsActive)
            .OrderByDescending(rule => rule.CreatedAt)
            .ToListAsync(cancellationToken);

        var cacheItems = rules
            .Select(rule => rule.ToCacheItem())
            .ToList();
        await _redisService.SetAsync(
            RedisKeys.ActivePricingRules,
            JsonSerializer.Serialize(cacheItems, JsonOptions),
            TimeSpan.FromMinutes(10));

        return rules;
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
            await _redisService.RemoveAsync(
                RedisKeys.MatchingOffer(offer.BookingId, offer.DriverId));
            await _redisService.RemoveAsync(
                RedisKeys.MatchingDriverLock(offer.DriverId));
        }

        await _redisService.RemoveAsync(RedisKeys.MatchingBooking(bookingId));
    }
}
