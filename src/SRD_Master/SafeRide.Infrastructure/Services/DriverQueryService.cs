using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Drivers.DTOs;
using SafeRide.Application.Features.Drivers.Services;
using SafeRide.Application.Features.RiskProtection;
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
    private readonly ITripCommissionCalculator _commissionCalculator;
    private readonly DriverCompensationOptions _compensationOptions;

    public DriverQueryService(
        ApplicationDbContext dbContext,
        IRedisService redisService,
        IMapRoutingService mapRoutingService,
        ITripCommissionCalculator commissionCalculator,
        IOptions<DriverCompensationOptions> compensationOptions)
    {
        _dbContext = dbContext;
        _redisService = redisService;
        _mapRoutingService = mapRoutingService;
        _commissionCalculator = commissionCalculator;
        _compensationOptions = compensationOptions.Value;
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
            .Include(trip => trip.Payments)

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

        var fareIsUnresolved = trip.TripStatus == TripStatus.WAITING_PAYMENT
            && (!trip.ActualFare.HasValue || !trip.FinalFare.HasValue);
        var endReconciliationPending = await _dbContext.TripEndReconciliationRequests
            .AsNoTracking()
            .AnyAsync(
                x => x.TripId == trip.Id
                    && (x.Status == TripEndReconciliationStatus.PENDING
                        || fareIsUnresolved),
                cancellationToken);

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
            arrivalPolyline,
            trip.FinalFare is <= 0m
                || trip.Payments.Any(payment => payment.PaymentStatus == PaymentStatus.Success),
            endReconciliationPending);
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

        var tripRequests = new List<DriverTripRequestDto>(openOffers.Count);
        foreach (var offer in openOffers)
        {
            tripRequests.Add(new DriverTripRequestDto(
                offer.Id,
                offer.BookingId,
                offer.OfferStatus,
                offer.ExpiresAt,
                offer.Booking.PickupAddress,
                offer.Booking.DestinationAddress,
                offer.PickupDistanceKm.HasValue ? (double)offer.PickupDistanceKm.Value : null,
                null,
                offer.OfferStatus == DriverOfferStatus.DriverAccepted
                    ? Math.Max(
                        0,
                        (int)Math.Ceiling((offer.ExpiresAt - utcNow).TotalSeconds))
                    : null,
                offer.LongPickupCompensation,
                offer.PickupDistanceKm.HasValue
                    && offer.PickupDistanceKm.Value
                        > (decimal)_compensationOptions.LongPickupThresholdKm,
                offer.Booking.AcceptedPricePerHour is not > 0m
                    && offer.Booking.EstimatedDistanceKm.HasValue
                    && offer.Booking.AcceptedLongDistanceThresholdKm.HasValue
                    && offer.Booking.EstimatedDistanceKm.Value
                        > offer.Booking.AcceptedLongDistanceThresholdKm.Value));
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
                && x.SettlementEffect != WalletSettlementEffect.CashPromotionSubsidy
                && x.CreatedAt >= queryStartUtc
                && x.CreatedAt < queryEndUtc)
            .Select(x => new WalletIncomeRow(x.Amount, x.CreatedAt))
            .ToListAsync(cancellationToken);

        var cashIncome = await _dbContext.Payments
            .AsNoTracking()
            .Where(x => x.Trip.DriverId == driverId
                && x.PaymentMethod == PaymentMethod.CASH
                && x.PaymentStatus == PaymentStatus.Success
                && x.PaidAt != null
                && x.PaidAt >= queryStartUtc
                && x.PaidAt < queryEndUtc)
            .Select(x => new
            {
                x.TripId,
                Fare = x.Trip.ActualFare ?? x.Trip.Booking.EstimatedFare,
                PaidAt = x.PaidAt!.Value
            })
            .ToListAsync(cancellationToken);

        var cashTripIds = cashIncome.Select(x => x.TripId).ToArray();
        var settlementEarnings = await _dbContext.TripFinancialSettlements
            .AsNoTracking()
            .Where(x => cashTripIds.Contains(x.TripId))
            .ToDictionaryAsync(x => x.TripId, x => x.DriverEarning, cancellationToken);
        var legacyCommissionRate = await _dbContext.RiskProtectionPolicyVersions
            .AsNoTracking()
            .OrderBy(x => x.EffectiveFromUtc)
            .Select(x => x.BasePlatformCommissionRate)
            .FirstAsync(cancellationToken);

        incomeTransactions.AddRange(cashIncome.Select(x =>
        {
            var driverEarning = settlementEarnings.TryGetValue(x.TripId, out var snapshotEarning)
                ? snapshotEarning
                : _commissionCalculator.Calculate(new CommissionCalculationInput(
                    x.Fare,
                    PromotionExpense: 0m,
                    legacyCommissionRate,
                    RiskReserveRate: 0m,
                    IsRiskContributionEligible: false)).DriverEarning;
            return new WalletIncomeRow(driverEarning, x.PaidAt);
        }));

        var zeroPayCashIncome = await _dbContext.TripFinancialSettlements
            .AsNoTracking()
            .Where(x => x.Trip.DriverId == driverId
                && x.SettledAtUtc >= queryStartUtc
                && x.SettledAtUtc < queryEndUtc
                && x.Trip.WalletTransactions.Any(transaction =>
                    transaction.SettlementEffect == WalletSettlementEffect.CashPromotionSubsidy)
                && !x.Trip.Payments.Any(payment =>
                    payment.PaymentMethod == PaymentMethod.CASH
                    && payment.PaymentStatus == PaymentStatus.Success))
            .Select(x => new WalletIncomeRow(x.DriverEarning, x.SettledAtUtc!.Value))
            .ToListAsync(cancellationToken);
        incomeTransactions.AddRange(zeroPayCashIncome);

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
                    || x.TransactionType == WalletTransactionType.Bonus
                    || x.TransactionType == WalletTransactionType.TopUp,
                x.TripId != null
                    ? "Chuyến đi #TRP-" + x.TripId
                    : x.TransactionType == WalletTransactionType.Withdrawal
                        ? "Rút tiền về ngân hàng"
                        : x.TransactionType == WalletTransactionType.TopUp
                            ? "Nạp tiền qua PayOS"
                        : x.TransactionType == WalletTransactionType.Bonus
                            ? "Tiền thưởng"
                            : x.TransactionType == WalletTransactionType.Penalty
                                ? "Khoản khấu trừ"
                                : "Thu nhập",
                x.Description,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        var recentCashReceipts = await _dbContext.Payments
            .AsNoTracking()
            .Where(x => x.Trip.DriverId == driverId
                && x.PaymentMethod == PaymentMethod.CASH
                && x.PaymentStatus == PaymentStatus.Success
                && x.PaidAt != null)
            .OrderByDescending(x => x.PaidAt)
            .ThenByDescending(x => x.Id)
            .Take(recentLimit)
            .Select(x => new DriverWalletTransactionDto(
                -x.Id,
                x.TripId,
                WalletTransactionType.Income,
                x.Amount,
                true,
                "Đã nhận tiền mặt chuyến #TRP-" + x.TripId,
                "Tiền mặt đã nhận trực tiếp từ khách hàng",
                x.PaidAt!.Value))
            .ToListAsync(cancellationToken);

        recentTransactions.AddRange(recentCashReceipts);
        recentTransactions = recentTransactions
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(recentLimit)
            .ToList();

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
