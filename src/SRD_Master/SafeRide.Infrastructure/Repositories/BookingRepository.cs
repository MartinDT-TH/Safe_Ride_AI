using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Features.Bookings.DTOs;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;
using SafeRide.Infrastructure.Services;
using System.Text.Json;

namespace SafeRide.Infrastructure.Repositories;

public sealed class BookingRepository : IBookingRepository
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IRedisService _redisService;
    private readonly IMatchingPolicyProvider _matchingPolicyProvider;
    private readonly IOptionsMonitor<TripTrackingOptions> _tripTrackingOptions;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly BookingStatus[] CustomerHistoryStatuses =
    [
        BookingStatus.PendingSchedule,
        BookingStatus.Searching,
        BookingStatus.DriverAssigned,
        BookingStatus.Cancelled,
        BookingStatus.Completed
    ];

    private static readonly BookingStatus[] DriverHistoryStatuses =
    [
        BookingStatus.DriverAssigned,
        BookingStatus.Cancelled,
        BookingStatus.Completed
    ];

    public BookingRepository(
        ApplicationDbContext dbContext,
        IRedisService redisService,
        IMatchingPolicyProvider matchingPolicyProvider,
        IOptionsMonitor<TripTrackingOptions> tripTrackingOptions)
    {
        _dbContext = dbContext;
        _redisService = redisService;
        _matchingPolicyProvider = matchingPolicyProvider;
        _tripTrackingOptions = tripTrackingOptions;
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
            .Include(booking => booking.Trip)
            .FirstOrDefaultAsync(
                booking => booking.BookingId == bookingId
                    && booking.CustomerId == customerId,
                cancellationToken);
    }

    public Task<Booking?> GetCustomerBookingWithDetailsAsync(
        long bookingId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Bookings
            .AsNoTracking()
            .Include(booking => booking.Vehicle)
            .Include(booking => booking.Trip)
                .ThenInclude(trip => trip!.ReturnConfirmations)
                    .ThenInclude(returnConfirmation => returnConfirmation.Evidence)
            .Include(booking => booking.Trip)
                .ThenInclude(trip => trip!.Payments)
            .Include(booking => booking.BookingPromotions)
                .ThenInclude(bookingPromotion => bookingPromotion.Promotion)
            .FirstOrDefaultAsync(
                booking => booking.BookingId == bookingId
                    && booking.CustomerId == customerId,
                cancellationToken);
    }

    public Task<Booking?> GetActiveNowBookingAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Bookings
            .AsNoTracking()
            .Include(booking => booking.Vehicle)
            .Include(booking => booking.Trip)
                .ThenInclude(trip => trip!.ReturnConfirmations)
                    .ThenInclude(returnConfirmation => returnConfirmation.Evidence)
            .Include(booking => booking.Trip)
                .ThenInclude(trip => trip!.Payments)
            .Include(booking => booking.BookingPromotions)
                .ThenInclude(bookingPromotion => bookingPromotion.Promotion)
            .Where(booking => booking.CustomerId == customerId
                && booking.BookingType == BookingType.Now
                && (booking.BookingStatus == BookingStatus.Searching
                    || booking.BookingStatus == BookingStatus.DriverAssigned))
            .OrderByDescending(booking => booking.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BookingHistoryItemDto>> GetCustomerBookingHistoryAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var bookings = await _dbContext.Bookings
            .AsNoTracking()
            .Include(booking => booking.Vehicle)
            .Include(booking => booking.BookingPromotions)
                .ThenInclude(bookingPromotion => bookingPromotion.Promotion)
            .Include(booking => booking.Trip!)
                .ThenInclude(trip => trip.Driver)
                    .ThenInclude(driverProfile => driverProfile.Driver)
            .Where(booking => booking.CustomerId == customerId
                && CustomerHistoryStatuses.Contains(booking.BookingStatus))
            .OrderByDescending(booking => booking.UpdatedAt)
            .ToListAsync(cancellationToken);

        var driverRatings = await LoadDriverRatingsAsync(
            bookings
                .Where(booking => booking.Trip is not null)
                .Select(booking => booking.Trip!.DriverId)
                .Distinct()
                .ToList(),
            cancellationToken);
        var reportedTripIds = await LoadReportedTripIdsAsync(
            customerId,
            bookings
                .Where(booking => booking.Trip is not null)
                .Select(booking => booking.Trip!.Id)
                .Distinct()
                .ToList(),
            cancellationToken);

        return bookings
            .Select(booking => ToCustomerHistoryItem(
                booking,
                driverRatings,
                reportedTripIds))
            .ToList();
    }

    public async Task<IReadOnlyList<BookingHistoryItemDto>> GetDriverBookingHistoryAsync(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var bookings = await _dbContext.Bookings
            .AsNoTracking()
            .Include(booking => booking.Vehicle)
            .Include(booking => booking.BookingPromotions)
                .ThenInclude(bookingPromotion => bookingPromotion.Promotion)
            .Include(booking => booking.Trip)
            .Where(booking => booking.Trip != null
                && booking.Trip.DriverId == driverId
                && DriverHistoryStatuses.Contains(booking.BookingStatus))
            .OrderByDescending(booking => booking.UpdatedAt)
            .ToListAsync(cancellationToken);

        return bookings
            .Select(ToDriverHistoryItem)
            .ToList();
    }

    public async Task<BookingDriverOfferDto?> GetLatestBookingDriverOfferAsync(
        long bookingId,
        CancellationToken cancellationToken)
    {
        var latestOffer = await (
            from driverOffer in _dbContext.BookingDriverOffers.AsNoTracking()
            join profile in _dbContext.DriverProfiles.AsNoTracking()
                on driverOffer.DriverId equals profile.DriverId
            join user in _dbContext.AspNetUsers.AsNoTracking()
                on driverOffer.DriverId equals user.Id
            where driverOffer.BookingId == bookingId
                && (driverOffer.OfferStatus == DriverOfferStatus.DriverAccepted
                    || driverOffer.OfferStatus == DriverOfferStatus.CustomerConfirmed)
            orderby driverOffer.ConfirmedAt ?? driverOffer.OfferedAt descending
            select new
            {
                driverOffer.Id,
                driverOffer.DriverId,
                user.FullName,
                user.UserName,
                user.AvatarUrl,
                profile.ExperienceYears,
                driverOffer.ExpiresAt,
                driverOffer.OfferStatus
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (latestOffer is null)
        {
            return null;
        }

        var licenseClass = await _dbContext.DriverKycs
            .AsNoTracking()
            .Where(kyc => kyc.DriverId == latestOffer.DriverId
                && kyc.DocumentType == KycDocumentType.DRIVING_LICENSE
                && kyc.KycStatus == KycStatus.Approved
                && kyc.LicenseClass.HasValue)
            .OrderByDescending(kyc => kyc.VerifiedAt ?? kyc.CreatedAt)
            .Select(kyc => kyc.LicenseClass)
            .FirstOrDefaultAsync(cancellationToken);

        var ratingStats = await _dbContext.Ratings
            .AsNoTracking()
            .Where(rating => rating.DriverId == latestOffer.DriverId)
            .GroupBy(rating => rating.DriverId)
            .Select(group => new
            {
                Rating = Math.Round(group.Average(rating => (double)rating.RatingScore), 1),
                TripCount = group.Count()
            })
            .FirstOrDefaultAsync(cancellationToken);
        var driverLocation = await GetDriverLocationAsync(
            latestOffer.DriverId,
            cancellationToken);

        return new BookingDriverOfferDto(
            latestOffer.Id,
            latestOffer.DriverId,
            latestOffer.FullName ?? latestOffer.UserName ?? "Tài xế SafeRide",
            latestOffer.AvatarUrl,
            ratingStats?.Rating ?? 0,
            ratingStats?.TripCount ?? 0,
            latestOffer.ExperienceYears ?? 0,
            licenseClass ?? LicenseClass.A1,
            latestOffer.ExpiresAt,
            latestOffer.OfferStatus,
            driverLocation?.Latitude,
            driverLocation?.Longitude,
            latestOffer.OfferStatus == DriverOfferStatus.DriverAccepted
                ? (int?)Math.Max(0, (int)Math.Ceiling((latestOffer.ExpiresAt - DateTime.UtcNow).TotalSeconds))
                : null);
    }

    public async Task<LocationPoint?> GetDriverLocationAsync(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var locationJson = await _redisService.GetAsync(RedisKeys.DriverLocation(driverId));
        if (string.IsNullOrWhiteSpace(locationJson))
        {
            return null;
        }

        try
        {
            var cache = JsonSerializer.Deserialize<DriverLocationCache>(
                locationJson,
                JsonOptions);

            return cache is null
                ? null
                : new LocationPoint(cache.Latitude, cache.Longitude);
        }
        catch (JsonException)
        {
            await _redisService.RemoveAsync(RedisKeys.DriverLocation(driverId));
            return null;
        }
    }

    public async Task ExpireStaleNowBookingsAsync(
        Guid customerId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var bookings = await _dbContext.Bookings
            .Include(booking => booking.Trip)
            .Include(booking => booking.DriverOffers)
            .Include(booking => booking.BookingPromotions)
            .Where(booking => booking.CustomerId == customerId
                && booking.BookingType == BookingType.Now
                && (booking.BookingStatus == BookingStatus.Searching
                    || booking.BookingStatus == BookingStatus.DriverAssigned))
            .ToListAsync(cancellationToken);

        var changed = false;
        foreach (var booking in bookings)
        {
            if (!ShouldExpireNowBooking(booking, utcNow))
            {
                continue;
            }

            booking.BookingStatus = BookingStatus.Expired;
            booking.UpdatedAt = utcNow;
            changed = true;

            _dbContext.BookingPromotions.RemoveRange(booking.BookingPromotions);

            foreach (var offer in booking.DriverOffers
                .Where(offer => offer.OfferStatus == DriverOfferStatus.Sent
                    || offer.OfferStatus == DriverOfferStatus.DriverAccepted))
            {
                offer.OfferStatus = DriverOfferStatus.Expired;
                offer.ExpiredAt ??= utcNow;

                await _redisService.RemoveAsync(
                    RedisKeys.MatchingOffer(offer.BookingId, offer.DriverId));

                await _redisService.RemoveAsync(
                    RedisKeys.MatchingDriverLock(offer.DriverId));
            }

            await _redisService.RemoveAsync(RedisKeys.MatchingBooking(booking.BookingId));
        }

        if (changed)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<Dictionary<Guid, double>> LoadDriverRatingsAsync(
        IReadOnlyCollection<Guid> driverIds,
        CancellationToken cancellationToken)
    {
        if (driverIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.Ratings
            .AsNoTracking()
            .Where(rating => driverIds.Contains(rating.DriverId))
            .GroupBy(rating => rating.DriverId)
            .ToDictionaryAsync(
                group => group.Key,
                group => Math.Round(
                    group.Average(rating => (double)rating.RatingScore),
                    1),
                cancellationToken);
    }

    private async Task<HashSet<long>> LoadReportedTripIdsAsync(
        Guid customerId,
        IReadOnlyCollection<long> tripIds,
        CancellationToken cancellationToken)
    {
        if (tripIds.Count == 0)
        {
            return [];
        }

        var reportedTripIds = await _dbContext.Reports
            .AsNoTracking()
            .Where(report => report.UserId == customerId
                && report.TripId.HasValue
                && tripIds.Contains(report.TripId.Value))
            .Select(report => report.TripId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        return reportedTripIds.ToHashSet();
    }

    private static BookingHistoryItemDto ToCustomerHistoryItem(
        Booking booking,
        IReadOnlyDictionary<Guid, double> driverRatings,
        IReadOnlySet<long> reportedTripIds)
    {
        var price = BookingPriceMapper.FromBooking(booking);
        var driverId = booking.Trip?.DriverId;
        var driverUser = booking.Trip?.Driver?.Driver;
        var hasReported = booking.Trip is not null &&
            reportedTripIds.Contains(booking.Trip.Id);

        double? driverRating = null;
        if (driverId.HasValue &&
            driverRatings.TryGetValue(driverId.Value, out var rating))
        {
            driverRating = rating;
        }

        return new BookingHistoryItemDto(
            booking.BookingId,
            booking.PickupAddress,
            booking.DestinationAddress ?? string.Empty,
            ResolveOccurredAt(booking),
            (double)(booking.EstimatedDistanceKm ?? 0m),
            booking.EstimatedFare,
            price.FinalFare,
            booking.BookingStatus,
            booking.Vehicle.BrandModel,
            booking.Vehicle.VehicleType == VehicleType.Motorbike,
            driverUser?.FullName ?? driverUser?.UserName,
            driverRating,
            driverUser?.AvatarUrl,
            hasReported);
    }

    private static BookingHistoryItemDto ToDriverHistoryItem(Booking booking)
    {
        var price = BookingPriceMapper.FromBooking(booking);

        return new BookingHistoryItemDto(
            booking.BookingId,
            booking.PickupAddress,
            booking.DestinationAddress ?? string.Empty,
            ResolveOccurredAt(booking),
            (double)(booking.EstimatedDistanceKm ?? 0m),
            booking.EstimatedFare,
            price.FinalFare,
            booking.BookingStatus,
            booking.Vehicle.BrandModel,
            booking.Vehicle.VehicleType == VehicleType.Motorbike,
            null,
            null,
            null,
            false);
    }

    private static DateTime ResolveOccurredAt(Booking booking)
    {
        if (booking.BookingStatus == BookingStatus.Completed)
        {
            return booking.Trip?.CompletedAt
                ?? booking.ScheduledAt
                ?? booking.UpdatedAt;
        }

        if (booking.BookingStatus == BookingStatus.PendingSchedule)
        {
            return booking.ScheduledAt
                ?? booking.UpdatedAt;
        }

        if (booking.BookingStatus == BookingStatus.DriverAssigned)
        {
            return booking.ScheduledAt
                ?? booking.Trip?.DriverAssignedAt
                ?? booking.UpdatedAt;
        }

        return booking.UpdatedAt;
    }

    private bool ShouldExpireNowBooking(Booking booking, DateTime utcNow)
    {
        if (booking.BookingStatus == BookingStatus.DriverAssigned)
        {
            return booking.Trip is null
                || booking.Trip.TripStatus == TripStatus.CANCELLED;
        }

        var startedAt = _matchingPolicyProvider.GetMatchingStartedAt(booking)
            ?? booking.CreatedAt;

        return utcNow >= startedAt.AddMinutes(
            _matchingPolicyProvider.Current.BookingExpireAfterMinutes);
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

        var activeRules = await GetActivePricingRulesAsync(cancellationToken);

        if (customerVehicleClasses.Count == 0)
        {
            return activeRules;
        }

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

    public async Task<SurgePricingRule?> GetActiveSurgePricingRuleAsync(
        DateTime currentUtcTime,
        CancellationToken cancellationToken)
    {
        var activeRules = await GetActiveSurgePricingRulesAsync(cancellationToken);

        var localTime = TimeZoneInfo.ConvertTimeFromUtc(
            currentUtcTime,
            TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"));

        var currentTime = TimeOnly.FromDateTime(localTime);
        var currentDayString = localTime.DayOfWeek.ToString().Substring(0, 3);

        return activeRules.FirstOrDefault(rule =>
        {
            if (!IsTimeInRange(currentTime, rule.StartTime, rule.EndTime))
            {
                return false;
            }

            var days = rule.AppliedDays.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return days.Contains(currentDayString, StringComparer.OrdinalIgnoreCase);
        });
    }

    private async Task<IReadOnlyList<SurgePricingRule>> GetActiveSurgePricingRulesAsync(
        CancellationToken cancellationToken)
    {
        var cached = await _redisService.GetAsync(RedisKeys.ActiveSurgePricingRules);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            try
            {
                var cachedItems = JsonSerializer.Deserialize<List<SurgePricingRuleCacheItem>>(
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
                await _redisService.RemoveAsync(RedisKeys.ActiveSurgePricingRules);
            }
        }

        var rules = await _dbContext.SurgePricingRules
            .AsNoTracking()
            .Where(rule => rule.IsActive)
            .OrderByDescending(rule => rule.CreatedAt)
            .ToListAsync(cancellationToken);

        var cacheItems = rules
            .Select(rule => rule.ToCacheItem())
            .ToList();

        await _redisService.SetAsync(
            RedisKeys.ActiveSurgePricingRules,
            JsonSerializer.Serialize(cacheItems, JsonOptions),
            TimeSpan.FromMinutes(10));

        return rules;
    }

    private static bool IsTimeInRange(
        TimeOnly currentTime,
        TimeOnly startTime,
        TimeOnly endTime)
    {
        if (startTime <= endTime)
        {
            return currentTime >= startTime
                && currentTime <= endTime;
        }

        return currentTime >= startTime
            || currentTime <= endTime;
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
                && (offer.OfferStatus == DriverOfferStatus.Sent
                    || offer.OfferStatus == DriverOfferStatus.DriverAccepted))
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

    public async Task<bool> CancelAssignedTripAsync(
        long bookingId,
        Guid cancelledByUserId,
        string? reason,
        DateTime cancelledAt,
        CancellationToken cancellationToken)
    {
        var trip = await _dbContext.Trips
            .FirstOrDefaultAsync(
                trip => trip.BookingId == bookingId,
                cancellationToken);

        if (trip is null)
        {
            var confirmedOffer = await _dbContext.BookingDriverOffers
                .AsNoTracking()
                .Where(offer => offer.BookingId == bookingId
                    && offer.OfferStatus == DriverOfferStatus.CustomerConfirmed)
                .OrderByDescending(offer => offer.ConfirmedAt ?? offer.OfferedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (confirmedOffer is not null)
            {
                await ReleaseDriverAsync(
                    confirmedOffer.DriverId,
                    cancelledAt,
                    cancellationToken);
                await _redisService.RemoveAsync(
                    RedisKeys.DriverActiveTrip(confirmedOffer.DriverId));
            }

            return true;
        }

        if (trip.TripStatus != TripStatus.ACCEPTED &&
            trip.TripStatus != TripStatus.DRIVER_ARRIVING &&
            trip.TripStatus != TripStatus.ARRIVED)
        {
            return false;
        }

        trip.TripStatus = TripStatus.CANCELLED;
        trip.CancelledByUserId = cancelledByUserId;
        trip.CancellationReason = reason;

        await ReleaseDriverAsync(trip.DriverId, cancelledAt, cancellationToken);
        await _redisService.RemoveAsync(RedisKeys.TripLive(trip.Id));
        await _redisService.RemoveAsync(RedisKeys.DriverActiveTrip(trip.DriverId));

        return true;
    }

    private async Task ReleaseDriverAsync(
        Guid driverId,
        DateTime releasedAt,
        CancellationToken cancellationToken)
    {
        var driverProfile = await _dbContext.DriverProfiles
            .FirstOrDefaultAsync(
                profile => profile.DriverId == driverId,
                cancellationToken);

        if (driverProfile is not null)
        {
            driverProfile.WorkStatus = DriverWorkStatus.Online;
            driverProfile.LastActiveAt = releasedAt;
            driverProfile.UpdatedAt = releasedAt;
        }

        await _redisService.SetAsync(
            RedisKeys.DriverOnline(driverId),
            "1",
            TimeSpan.FromMinutes(_tripTrackingOptions.CurrentValue.DriverStatusTtlMinutes));

        await _redisService.SetAsync(
            RedisKeys.DriverStatus(driverId),
            DriverWorkStatus.Online.ToString(),
            TimeSpan.FromMinutes(_tripTrackingOptions.CurrentValue.DriverStatusTtlMinutes));
        await _redisService.RemoveAsync(RedisKeys.DriverActiveTrip(driverId));
    }
}

