using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Drivers.DTOs;
using SafeRide.Application.Features.Trips.DTOs;
using SafeRide.Contracts.Responses.Drivers;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;
using SafeRide.Application.Common.Models;
using System.Text.Json;

namespace SafeRide.Infrastructure.Services;

public sealed class DriverQueryService : IDriverQueryService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IRedisService _redisService;
    private readonly IMapRoutingService _mapRoutingService;

    public DriverQueryService(
        ApplicationDbContext dbContext,
        IRedisService redisService,
        IMapRoutingService mapRoutingService)
    {
        _dbContext = dbContext;
        _redisService = redisService;
        _mapRoutingService = mapRoutingService;
    }

    public async Task<IReadOnlyList<NearbyDriverResponse>> GetNearbyDriversAsync(
        double latitude,
        double longitude,
        double radiusKm,
        int limit,
        CancellationToken cancellationToken)
    {
        var driverIds = await _redisService.GeoRadiusAsync(
            RedisKeys.OnlineDriversGeo,
            longitude,
            latitude,
            radiusKm,
            limit);

        var tasks = driverIds.Select(async id =>
        {
            var guid = Guid.Parse(id);
            var locationJson = await _redisService.GetAsync(RedisKeys.DriverLocation(guid));
            if (string.IsNullOrEmpty(locationJson))
            {
                return null;
            }

            var cache = JsonSerializer.Deserialize<DriverLocationCache>(locationJson);
            return cache is null
                ? null
                : new NearbyDriverResponse(
                    guid,
                    cache.Latitude,
                    cache.Longitude);
        });

        var results = await Task.WhenAll(tasks);
        return results.Where(x => x is not null).ToList()!;
    }

    public async Task<ActiveDriverTripDto?> GetActiveTripAsync(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var trip = await _dbContext.Trips
            .AsNoTracking()
            .Include(trip => trip.Booking)
            .Include(trip => trip.ReturnConfirmations)
            .ThenInclude(returnConfirmation => returnConfirmation.Evidence)

            .Where(trip => trip.DriverId == driverId
                && trip.TripStatus != TripStatus.COMPLETED
                && trip.TripStatus != TripStatus.CANCELLED)
            .OrderByDescending(trip => trip.DriverAssignedAt ?? trip.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (trip is null)
        {
            return null;
        }

        var confirmation = trip.ReturnConfirmations
            .OrderByDescending(returnConfirmation => returnConfirmation.ConfirmedAt)
            .ThenByDescending(returnConfirmation => returnConfirmation.Id)
            .FirstOrDefault();

        string? arrivalPolyline = null;
        if (trip.TripStatus is TripStatus.ACCEPTED or TripStatus.DRIVER_ARRIVING)
        {
            var locationJson = await _redisService.GetAsync(RedisKeys.DriverLocation(driverId));
            if (!string.IsNullOrEmpty(locationJson))
            {
                var cache = JsonSerializer.Deserialize<DriverLocationCache>(locationJson);
                if (cache is not null)
                {
                    try
                    {
                        var route = await _mapRoutingService.GetRouteEstimateAsync(
                            new RouteEstimateRequest
                            {
                                Origin = new LocationPoint(cache.Latitude, cache.Longitude),
                                Destination = new LocationPoint(trip.Booking.PickupLocation.Y, trip.Booking.PickupLocation.X),
                                Provider = MapProvider.Auto,
                                TravelMode = MapTravelMode.Car,
                                IncludePolyline = true,
                                RequestSource = "DriverArrival"
                            },
                            cancellationToken);
                        arrivalPolyline = route.EncodedPolyline;
                    }
                    catch
                    {
                        // Ignore routing errors
                    }
                }
            }
        }

        return new ActiveDriverTripDto(
            trip.BookingId,
            trip.Id,
            trip.TripStatus,
            trip.Booking.PickupLocation.Y,
            trip.Booking.PickupLocation.X,
            trip.Booking.DestinationLocation != null
                ? trip.Booking.DestinationLocation.Y
                : (double?)null,
            trip.Booking.DestinationLocation != null
                ? trip.Booking.DestinationLocation.X
                : (double?)null,
            trip.Booking.RoutePolyline,
            confirmation is null
                ? null
                : new TripReturnConfirmationSummaryDto(
                    confirmation.Id,
                    confirmation.HandoverStatus,
                    confirmation.DriverId,
                    confirmation.ConfirmedByUserId,
                    confirmation.ConfirmedAt,
                    confirmation.DriverLatitude,
                    confirmation.DriverLongitude,
                    confirmation.Note,
                    confirmation.Evidence
                        .OrderBy(evidence => evidence.DisplayOrder)
                        .Select(evidence => new TripReturnEvidenceSummaryDto(
                            evidence.Id,
                            evidence.ImageUrl,
                            evidence.ContentType,
                            evidence.DisplayOrder))
                        .ToList()),
            arrivalPolyline);
    }

    public async Task<IReadOnlyList<DriverTripRequestDto>> GetOpenTripRequestsAsync(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var openOffers = await _dbContext.BookingDriverOffers
            .AsNoTracking()
            .Include(offer => offer.Booking)
            .Where(offer => offer.DriverId == driverId
                && (offer.OfferStatus == DriverOfferStatus.Sent
                    || offer.OfferStatus == DriverOfferStatus.DriverAccepted)
                && offer.ExpiresAt > utcNow
                && offer.Booking.BookingStatus == BookingStatus.Searching)
            .OrderByDescending(offer => offer.ConfirmedAt ?? offer.OfferedAt)
            .ToListAsync(cancellationToken);

        if (openOffers.Count == 0)
        {
            return [];
        }

        LocationPoint? driverLocation = null;
        var locationJson = await _redisService.GetAsync(RedisKeys.DriverLocation(driverId));
        if (!string.IsNullOrWhiteSpace(locationJson))
        {
            try
            {
                var cache = JsonSerializer.Deserialize<DriverLocationCache>(locationJson);
                if (cache is not null)
                {
                    driverLocation = new LocationPoint(cache.Latitude, cache.Longitude);
                }
            }
            catch (JsonException)
            {
                await _redisService.RemoveAsync(RedisKeys.DriverLocation(driverId));
            }
        }

        var tripRequests = new List<DriverTripRequestDto>(openOffers.Count);
        foreach (var offer in openOffers)
        {
            double? pickupDistanceKm = null;
            int? pickupDurationMinutes = null;

            if (driverLocation is not null)
            {
                try
                {
                    var route = await _mapRoutingService.GetRouteEstimateAsync(
                        new RouteEstimateRequest
                        {
                            Origin = driverLocation,
                            Destination = new LocationPoint(
                                offer.Booking.PickupLocation.Y,
                                offer.Booking.PickupLocation.X),
                            Provider = MapProvider.Auto,
                            TravelMode = MapTravelMode.Car,
                            IncludePolyline = false,
                            RequestSource = "DriverTripRequest"
                        },
                        cancellationToken);

                    pickupDistanceKm = route.DistanceKm;
                    pickupDurationMinutes = route.DurationMinutes;
                }
                catch
                {
                    // Ignore routing errors so the request list still loads.
                }
            }

            tripRequests.Add(new DriverTripRequestDto(
                offer.Id,
                offer.BookingId,
                offer.OfferStatus,
                offer.ExpiresAt,
                offer.Booking.EstimatedFare,
                offer.Booking.PickupAddress,
                offer.Booking.DestinationAddress,
                pickupDistanceKm,
                pickupDurationMinutes,
                offer.OfferStatus == DriverOfferStatus.DriverAccepted
                    ? Math.Max(
                        0,
                        (int)Math.Ceiling((offer.ExpiresAt - utcNow).TotalSeconds))
                    : null));
        }

        return tripRequests;
    }

    public async Task<bool> HasActiveTripAsync(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Trips
            .AnyAsync(trip => trip.DriverId == driverId
                && trip.TripStatus != TripStatus.COMPLETED
                && trip.TripStatus != TripStatus.CANCELLED,
                cancellationToken);
    }

    public async Task<DriverWalletDto> GetWalletAsync(
        Guid driverId,
        WalletPeriod period,
        int utcOffsetMinutes,
        int recentLimit,
        CancellationToken cancellationToken)
    {
        var wallet = await _dbContext.DriverWallets
            .AsNoTracking()
            .Where(x => x.DriverId == driverId)
            .Select(x => new
            {
                x.Id,
                x.CurrentBalance
            })
            .SingleOrDefaultAsync(cancellationToken);

        var localNow = DateTime.UtcNow.AddMinutes(utcOffsetMinutes);
        var currentStartLocal = GetPeriodStart(localNow, period);
        var currentEndLocal = GetPeriodEnd(currentStartLocal, period);
        var previousStartLocal = GetPreviousPeriodStart(currentStartLocal, period);
        var offset = TimeSpan.FromMinutes(utcOffsetMinutes);

        if (wallet is null)
        {
            return new DriverWalletDto(
                0m,
                BuildIncomeSummary(
                    period,
                    currentStartLocal,
                    currentEndLocal,
                    previousStartLocal,
                    [],
                    offset),
                [],
                null);
        }

        var queryStartUtc = previousStartLocal.Subtract(offset);
        var queryEndUtc = currentEndLocal.Subtract(offset);

        var incomeTransactions = await _dbContext.WalletTransactions
            .AsNoTracking()
            .Where(x => x.WalletId == wallet.Id
                && (x.TransactionType == WalletTransactionType.Income
                    || x.TransactionType == WalletTransactionType.Bonus)
                && x.CreatedAt >= queryStartUtc
                && x.CreatedAt < queryEndUtc)
            .Select(x => new WalletIncomeRow(x.Amount, x.CreatedAt))
            .ToListAsync(cancellationToken);

        var recentTransactions = await _dbContext.WalletTransactions
            .AsNoTracking()
            .Where(x => x.WalletId == wallet.Id)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(recentLimit)
            .Select(x => new DriverWalletTransactionDto(
                x.Id,
                x.TripId,
                x.TransactionType,
                x.Amount,
                x.TransactionType == WalletTransactionType.Income
                    || x.TransactionType == WalletTransactionType.Bonus,
                x.TripId != null
                    ? "Chuyến đi #TRP-" + x.TripId
                    : x.TransactionType == WalletTransactionType.Withdrawal
                        ? "Rút tiền về ngân hàng"
                        : x.TransactionType == WalletTransactionType.Bonus
                            ? "Tiền thưởng"
                            : x.TransactionType == WalletTransactionType.Penalty
                                ? "Khoản khấu trừ"
                                : "Thu nhập",
                x.Description,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        var savedBankAccount = await _dbContext.WithdrawalRequests
            .AsNoTracking()
            .Where(x => x.WalletId == wallet.Id)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(x => new SavedBankAccountDto(
                x.BankName,
                x.BankAccountNumber,
                x.BankAccountName))
            .FirstOrDefaultAsync(cancellationToken);

        return new DriverWalletDto(
            wallet.CurrentBalance,
            BuildIncomeSummary(
                period,
                currentStartLocal,
                currentEndLocal,
                previousStartLocal,
                incomeTransactions,
                offset),
            recentTransactions,
            savedBankAccount);
    }

    private static WalletIncomeSummaryDto BuildIncomeSummary(
        WalletPeriod period,
        DateTime currentStartLocal,
        DateTime currentEndLocal,
        DateTime previousStartLocal,
        IReadOnlyList<WalletIncomeRow> transactions,
        TimeSpan offset)
    {
        var currentTotal = 0m;
        var previousTotal = 0m;
        foreach (var transaction in transactions)
        {
            var localCreatedAt = transaction.CreatedAt.Add(offset);
            if (localCreatedAt >= currentStartLocal)
            {
                currentTotal += transaction.Amount;
            }
            else
            {
                previousTotal += transaction.Amount;
            }
        }

        decimal? changePercentage = previousTotal > 0
            ? decimal.Round(
                (currentTotal - previousTotal) * 100m / previousTotal,
                1,
                MidpointRounding.AwayFromZero)
            : currentTotal == 0 ? 0m : null;

        var buckets = CreateBuckets(currentStartLocal, currentEndLocal, period);
        foreach (var transaction in transactions)
        {
            var localCreatedAt = transaction.CreatedAt.Add(offset);
            if (localCreatedAt < currentStartLocal || localCreatedAt >= currentEndLocal)
            {
                continue;
            }

            var bucket = buckets.First(x =>
                localCreatedAt >= x.Start && localCreatedAt < x.End);
            bucket.Amount += transaction.Amount;
        }

        return new WalletIncomeSummaryDto(
            period,
            currentStartLocal,
            currentEndLocal,
            currentTotal,
            previousTotal,
            changePercentage,
            buckets.Select(x => new WalletChartPointDto(x.Start, x.Label, x.Amount)).ToList());
    }

    private static List<WalletBucket> CreateBuckets(
        DateTime start,
        DateTime end,
        WalletPeriod period)
    {
        var result = new List<WalletBucket>();
        if (period == WalletPeriod.Day)
        {
            for (var hour = 0; hour < 24; hour += 4)
            {
                var bucketStart = start.AddHours(hour);
                result.Add(new WalletBucket(
                    bucketStart,
                    bucketStart.AddHours(4),
                    $"{hour:00}h"));
            }
            return result;
        }

        if (period == WalletPeriod.Week)
        {
            for (var day = 0; day < 7; day++)
            {
                var bucketStart = start.AddDays(day);
                result.Add(new WalletBucket(
                    bucketStart,
                    bucketStart.AddDays(1),
                    day == 6 ? "CN" : $"T{day + 2}"));
            }
            return result;
        }

        var bucketNumber = 1;
        for (var bucketStart = start; bucketStart < end; bucketStart = bucketStart.AddDays(7))
        {
            result.Add(new WalletBucket(
                bucketStart,
                bucketStart.AddDays(7) < end ? bucketStart.AddDays(7) : end,
                $"T{bucketNumber++}"));
        }
        return result;
    }

    private static DateTime GetPeriodStart(DateTime value, WalletPeriod period)
    {
        var day = value.Date;
        return period switch
        {
            WalletPeriod.Day => day,
            WalletPeriod.Week => day.AddDays(-(((int)day.DayOfWeek + 6) % 7)),
            WalletPeriod.Month => new DateTime(day.Year, day.Month, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(period))
        };
    }

    private static DateTime GetPeriodEnd(DateTime start, WalletPeriod period) =>
        period switch
        {
            WalletPeriod.Day => start.AddDays(1),
            WalletPeriod.Week => start.AddDays(7),
            WalletPeriod.Month => start.AddMonths(1),
            _ => throw new ArgumentOutOfRangeException(nameof(period))
        };

    private static DateTime GetPreviousPeriodStart(DateTime start, WalletPeriod period) =>
        period switch
        {
            WalletPeriod.Day => start.AddDays(-1),
            WalletPeriod.Week => start.AddDays(-7),
            WalletPeriod.Month => start.AddMonths(-1),
            _ => throw new ArgumentOutOfRangeException(nameof(period))
        };

    private sealed record WalletIncomeRow(decimal Amount, DateTime CreatedAt);

    private sealed class WalletBucket(DateTime start, DateTime end, string label)
    {
        public DateTime Start { get; } = start;
        public DateTime End { get; } = end;
        public string Label { get; } = label;
        public decimal Amount { get; set; }
    }
}
