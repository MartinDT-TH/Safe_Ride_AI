using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Common.Realtime;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.Bookings.Services;
using SafeRide.Application.Features.Ratings;
using SafeRide.Application.Features.RiskProtection;
using SafeRide.Application.Features.Trips.DTOs;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.ExternalServices.PayOS;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;
using SafeRide.Infrastructure.Services;

namespace SafeRide.IntegrationTests;

[Trait(SqlServerTestDatabase.ProviderTraitName, SqlServerTestDatabase.InMemoryProvider)]
public sealed class TripStatusServiceTests
{
    private static readonly DateTime UtcNow =
        new(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DriverArrival_VerifiesFreshLocationAndStoresSnapshot()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.DRIVER_ARRIVING);
        var arrival = CreateArrivalVerificationService(fixture);
        var pickup = await fixture.DbContext.Bookings
            .Where(x => x.Trip!.Id == fixture.TripId)
            .Select(x => x.PickupLocation)
            .SingleAsync();
        fixture.Redis.SetDriverLocation(
            fixture.DriverId,
            new DriverLocationCache(fixture.DriverId, pickup.Y, pickup.X, UtcNow));

        var result = await arrival.VerifyAndRecordAsync(
            fixture.DriverId, fixture.TripId, CancellationToken.None);
        await fixture.Service.UpdateDriverTripStatusAsync(
            fixture.DriverId, fixture.TripId, TripStatus.ARRIVED, CancellationToken.None);

        var trip = await fixture.DbContext.Trips.SingleAsync(x => x.Id == fixture.TripId);
        Assert.Equal(TripStatus.ARRIVED, trip.TripStatus);
        Assert.NotNull(trip.ArrivedAt);
        Assert.Equal((decimal)result.Latitude, trip.ArrivalLatitude);
        Assert.Equal((decimal)result.Longitude, trip.ArrivalLongitude);
        Assert.Equal(0m, trip.ArrivalDistanceMeters);
        Assert.Equal(UtcNow, trip.ArrivalLocationVerifiedAt);
        Assert.Empty(await fixture.DbContext.CustomerBehaviorEvents.ToListAsync());
        Assert.Empty(await fixture.DbContext.DriverNoShowSupports.ToListAsync());
        Assert.Empty(await fixture.DbContext.Payments.ToListAsync());
        Assert.Empty(await fixture.DbContext.WalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task DriverArrival_RejectsMissingLocationWithoutChangingTrip()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.DRIVER_ARRIVING);
        var arrival = CreateArrivalVerificationService(fixture);

        var exception = await Assert.ThrowsAsync<BookingException>(() => arrival.VerifyAndRecordAsync(
            fixture.DriverId, fixture.TripId, CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
        await AssertArrivalUnchangedAsync(fixture);
    }

    [Fact]
    public async Task DriverArrival_RejectsStaleLocationWithoutChangingTrip()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.DRIVER_ARRIVING);
        var arrival = CreateArrivalVerificationService(fixture);
        fixture.Redis.SetDriverLocation(
            fixture.DriverId,
            new DriverLocationCache(fixture.DriverId, 106.660172, 10.762622, UtcNow.AddSeconds(-121)));

        var exception = await Assert.ThrowsAsync<BookingException>(() => arrival.VerifyAndRecordAsync(
            fixture.DriverId, fixture.TripId, CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
        await AssertArrivalUnchangedAsync(fixture);
    }

    [Fact]
    public async Task DriverArrival_RejectsLocationOutsideRadiusWithoutChangingTrip()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.DRIVER_ARRIVING);
        var arrival = CreateArrivalVerificationService(fixture);
        fixture.Redis.SetDriverLocation(
            fixture.DriverId,
            new DriverLocationCache(fixture.DriverId, 106.700000, 10.800000, UtcNow));

        var exception = await Assert.ThrowsAsync<BookingException>(() => arrival.VerifyAndRecordAsync(
            fixture.DriverId, fixture.TripId, CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
        await AssertArrivalUnchangedAsync(fixture);
    }

    [Fact]
    public async Task DriverArrival_RejectsUnassignedDriverWithoutChangingTrip()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.DRIVER_ARRIVING);
        var arrival = CreateArrivalVerificationService(fixture);

        var exception = await Assert.ThrowsAsync<BookingException>(() => arrival.VerifyAndRecordAsync(
            Guid.NewGuid(), fixture.TripId, CancellationToken.None));

        Assert.Equal(403, exception.StatusCode);
        await AssertArrivalUnchangedAsync(fixture);
    }

    private static TripArrivalVerificationService CreateArrivalVerificationService(TripStatusFixture fixture) =>
        new(
            fixture.DbContext,
            fixture.Redis,
            new DateTimeProviderFake(UtcNow),
            new OptionsMonitorFake<CustomerNoShowOptions>(new CustomerNoShowOptions()));

    private static async Task AssertArrivalUnchangedAsync(TripStatusFixture fixture)
    {
        var trip = await fixture.DbContext.Trips.SingleAsync(x => x.Id == fixture.TripId);
        Assert.Equal(TripStatus.DRIVER_ARRIVING, trip.TripStatus);
        Assert.Null(trip.ArrivedAt);
        Assert.Null(trip.ArrivalLatitude);
        Assert.Null(trip.ArrivalLongitude);
        Assert.Null(trip.ArrivalDistanceMeters);
        Assert.Null(trip.ArrivalLocationVerifiedAt);
    }
    [Fact]
    public async Task EndTrip_MovesDirectlyToWaitingPaymentWithoutCustomerResponse()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.IN_PROGRESS,
            pricingSnapshotV1: true);
        fixture.Redis.SetTripTrackingSnapshot(
            CreateV1RouteSnapshot(fixture.TripId, 43.252, -126.453, 14_000));
        await fixture.Service.EndTripAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);

        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
                    .ThenInclude(x => x.Promotion)
            .SingleAsync(x => x.Id == fixture.TripId);
        var driver = await fixture.DbContext.DriverProfiles
            .SingleAsync(x => x.DriverId == fixture.DriverId);

        Assert.Equal(TripStatus.WAITING_PAYMENT, trip.TripStatus);
        Assert.Equal(BookingStatus.DriverAssigned, trip.Booking.BookingStatus);
        Assert.Null(trip.CompletedAt);
        Assert.Equal(UtcNow, trip.EndedAt);
        Assert.Equal(14m, trip.ActualDistanceKm);
        Assert.NotNull(trip.ActualDurationMinutes);
        Assert.Equal(72_000m, trip.ActualFare);
        Assert.Equal(62_000m, trip.FinalFare);
        Assert.True(trip.DestinationReached);
        Assert.Equal(TripEndReason.NORMAL_COMPLETION, trip.EndReason);
        Assert.Equal(TripTerminationCategory.STANDARD, trip.TerminationCategory);
        Assert.Equal(2, fixture.Promotion.CurrentUsageCount);
        Assert.Single(trip.Booking.BookingPromotions);
        Assert.Equal(DriverWorkStatus.Busy, driver.WorkStatus);
        Assert.Null(fixture.Redis.DriverStatusValue);
        Assert.DoesNotContain(fixture.TripLiveKey, fixture.Redis.RemovedKeys);
        Assert.DoesNotContain(fixture.DriverActiveTripKey, fixture.Redis.RemovedKeys);
        Assert.DoesNotContain(RedisKeys.TripTrackingPath(fixture.TripId), fixture.Redis.RemovedKeys);
        var notification = Assert.Single(fixture.Realtime.TripStatusNotifications);
        Assert.Equal(fixture.TripId, notification.TripId);
        Assert.Equal(trip.BookingId, notification.BookingId);
        Assert.Equal(fixture.CustomerId, notification.CustomerId);
        Assert.Equal(fixture.DriverId, notification.DriverId);
        Assert.Equal(TripStatus.WAITING_PAYMENT, notification.TripStatus);
        Assert.Equal(BookingStatus.DriverAssigned, notification.BookingStatus);
        Assert.Empty(fixture.Realtime.BookingStatusNotifications);
        Assert.Single(fixture.Realtime.TripPaymentPendingNotifications);
    }

    [Fact]
    public async Task EndTrip_TransitionsDirectlyToPaymentWithoutCustomerApproval()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.IN_PROGRESS);

        await fixture.Service.EndTripAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);

        var trip = await fixture.DbContext.Trips
            .SingleAsync(x => x.Id == fixture.TripId);
        Assert.Equal(TripStatus.WAITING_PAYMENT, trip.TripStatus);
        Assert.Equal(UtcNow, trip.EndedAt);
    }

    [Fact]
    public async Task EndTrip_WithPendingQrPrepayment_WaitsForProviderVerification()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.IN_PROGRESS,
            pricingSnapshotV1: true);
        fixture.Redis.SetTripTrackingSnapshot(
            CreateV1RouteSnapshot(fixture.TripId, 43.252, -126.453, 14_000));
        fixture.DbContext.Payments.Add(new Payment
        {
            TripId = fixture.TripId,
            PaymentMethod = PaymentMethod.QR,
            TransactionReference = $"{fixture.TripId}456",
            Amount = 62_000m,
            Currency = "VND",
            PaymentStatus = PaymentStatus.Pending,
            CreatedAt = UtcNow
        });
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Service.EndTripAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);

        var trip = await fixture.DbContext.Trips.Include(x => x.Payments).SingleAsync();
        Assert.Equal(TripStatus.WAITING_PAYMENT, trip.TripStatus);
        Assert.Equal(PaymentStatus.Pending, Assert.Single(trip.Payments).PaymentStatus);
        Assert.Empty(await fixture.DbContext.WalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task EndTrip_WithVerifiedQrPrepayment_ReconcilesAndAdvances()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.IN_PROGRESS,
            pricingSnapshotV1: true);
        fixture.Redis.SetTripTrackingSnapshot(
            CreateV1RouteSnapshot(fixture.TripId, 43.252, -126.453, 14_000));
        await fixture.AddSuccessfulPaymentAsync(62_000m);

        await fixture.Service.EndTripAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);

        var trip = await fixture.DbContext.Trips.SingleAsync();
        var reconciliation = await fixture.DbContext.SafetyPaymentReconciliations.SingleAsync();
        Assert.Equal(TripStatus.WAITING_RETURN_CONFIRM, trip.TripStatus);
        Assert.Equal(SafetyPaymentReconciliationStatus.PAID, reconciliation.Status);
        Assert.Equal(0m, reconciliation.RemainingPayableAmount);
    }

    [Fact]
    public async Task EndTrip_CanContinue_CompletesAndReturnsDriverOnline()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.IN_PROGRESS,
            pricingSnapshotV1: true);
        fixture.Redis.SetTripTrackingSnapshot(
            CreateV1RouteSnapshot(fixture.TripId, 43.252, -126.453, 14_000));

        await fixture.Service.EndTripAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None,
            TripEndReason.NORMAL_COMPLETION,
            canContinueWorking: true);
        await fixture.AddSuccessfulPaymentAsync(62_000m);
        await fixture.Service.AdvanceAfterSuccessfulPaymentAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);
        await fixture.Service.ConfirmReturnByCustomerAsync(
            fixture.CustomerId,
            fixture.TripId,
            vehicleReturnedConfirmed: true,
            CancellationToken.None,
            ratingScore: 5);

        var trip = await fixture.DbContext.Trips.SingleAsync();
        var driver = await fixture.DbContext.DriverProfiles.SingleAsync();
        Assert.Equal(TripStatus.COMPLETED, trip.TripStatus);
        Assert.Equal(DriverWorkStatus.Online, driver.WorkStatus);
        Assert.Equal(DriverWorkStatus.Online.ToString(), fixture.Redis.DriverStatusValue);
    }

    [Fact]
    public async Task EndTrip_CannotContinue_PersistsOfflineThroughCompletionAndRemovesMatchingEligibility()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.IN_PROGRESS,
            pricingSnapshotV1: true);
        fixture.Redis.SetTripTrackingSnapshot(
            CreateV1RouteSnapshot(fixture.TripId, 43.252, -126.453, 14_000));
        await fixture.Redis.SetAsync(
            RedisKeys.DriverOnline(fixture.DriverId),
            "1",
            TimeSpan.FromMinutes(5));
        await fixture.Redis.SetAsync(
            RedisKeys.DriverStatus(fixture.DriverId),
            DriverWorkStatus.Busy.ToString(),
            TimeSpan.FromMinutes(5));

        await fixture.Service.EndTripAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None,
            TripEndReason.NORMAL_COMPLETION,
            canContinueWorking: false);

        var endedTrip = await fixture.DbContext.Trips.SingleAsync();
        var offlineDriver = await fixture.DbContext.DriverProfiles.SingleAsync();
        Assert.Equal(TripStatus.WAITING_PAYMENT, endedTrip.TripStatus);
        Assert.Equal(TripEndReason.NORMAL_COMPLETION, endedTrip.EndReason);
        Assert.Equal(DriverWorkStatus.Offline, offlineDriver.WorkStatus);
        Assert.Null(fixture.Redis.DriverStatusValue);
        Assert.Contains(RedisKeys.DriverOnline(fixture.DriverId), fixture.Redis.RemovedKeys);
        Assert.Contains(RedisKeys.DriverStatus(fixture.DriverId), fixture.Redis.RemovedKeys);
        Assert.Contains(
            (RedisKeys.OnlineDriversGeo, fixture.DriverId.ToString()),
            fixture.Redis.GeoRemovedMembers);
        Assert.DoesNotContain(fixture.DriverActiveTripKey, fixture.Redis.RemovedKeys);

        await fixture.AddSuccessfulPaymentAsync(62_000m);
        await fixture.Service.AdvanceAfterSuccessfulPaymentAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);
        await fixture.Service.ConfirmReturnByCustomerAsync(
            fixture.CustomerId,
            fixture.TripId,
            vehicleReturnedConfirmed: true,
            CancellationToken.None,
            ratingScore: 5);

        var completedTrip = await fixture.DbContext.Trips.SingleAsync();
        var completedDriver = await fixture.DbContext.DriverProfiles.SingleAsync();
        Assert.Equal(TripStatus.COMPLETED, completedTrip.TripStatus);
        Assert.Equal(DriverWorkStatus.Offline, completedDriver.WorkStatus);
        Assert.Null(fixture.Redis.DriverStatusValue);
        Assert.Contains(fixture.DriverActiveTripKey, fixture.Redis.RemovedKeys);
    }

    [Fact]
    public async Task EndTrip_V1HourlyBooking_CompletesWithoutDestinationGeofence()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.IN_PROGRESS,
            pricingSnapshotV1: true,
            isHourlyBooking: true);

        await fixture.Service.EndTripAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);

        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking)
            .SingleAsync(x => x.Id == fixture.TripId);
        Assert.Equal(TripStatus.WAITING_PAYMENT, trip.TripStatus);
        Assert.Equal(trip.Booking.EstimatedFare, trip.ActualFare);
        Assert.Equal(62_000m, trip.FinalFare);
        Assert.Null(trip.DestinationReached);
        Assert.Equal(TripEndReason.NORMAL_COMPLETION, trip.EndReason);
    }

    [Fact]
    public async Task UpdateDriverTripStatus_WaitingPayment_RequiresEndWorkflow()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.IN_PROGRESS,
            pricingSnapshotV1: true);

        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            fixture.Service.UpdateDriverTripStatusAsync(
                fixture.DriverId,
                fixture.TripId,
                TripStatus.WAITING_PAYMENT,
                CancellationToken.None));

        Assert.Equal("trip.end_workflow_required", exception.Code);
        var trip = await fixture.DbContext.Trips.SingleAsync(x => x.Id == fixture.TripId);
        Assert.Equal(TripStatus.IN_PROGRESS, trip.TripStatus);
        Assert.Null(trip.ActualFare);
        Assert.Null(trip.FinalFare);
    }

    [Fact]
    public async Task UpdateDriverTripStatus_CannotReopenWaitingReturnConfirmation()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.WAITING_RETURN_CONFIRM);

        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            fixture.Service.UpdateDriverTripStatusAsync(
                fixture.DriverId,
                fixture.TripId,
                TripStatus.IN_PROGRESS,
                CancellationToken.None));

        Assert.Equal("trip.invalid_status_transition", exception.Code);
        var trip = await fixture.DbContext.Trips.SingleAsync(x => x.Id == fixture.TripId);
        Assert.Equal(TripStatus.WAITING_RETURN_CONFIRM, trip.TripStatus);
    }

    [Theory]
    [InlineData(TripEndReason.SYSTEM_ERROR, "trip.end_reason_not_allowed")]
    [InlineData(TripEndReason.VEHICLE_SAFETY_ISSUE, "trip.safety_termination_required")]
    [InlineData(TripEndReason.SAFETY_TERMINATION, "trip.safety_termination_required")]
    public async Task EndTrip_DriverCannotChooseRestrictedFinancialReason(
        TripEndReason reason,
        string expectedCode)
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.IN_PROGRESS,
            pricingSnapshotV1: true);

        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            fixture.Service.EndTripAsync(
                fixture.DriverId,
                fixture.TripId,
                CancellationToken.None,
                reason));

        Assert.Equal(expectedCode, exception.Code);
        Assert.Equal(
            TripStatus.IN_PROGRESS,
            (await fixture.DbContext.Trips.SingleAsync()).TripStatus);
    }

    [Fact]
    public async Task EndTrip_DriverUnableToContinue_EndsOperationallyWithoutStaffAndGoesOffline()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.IN_PROGRESS,
            pricingSnapshotV1: true);

        await fixture.Service.EndTripAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None,
            TripEndReason.DRIVER_UNABLE_TO_CONTINUE,
            canContinueWorking: false);

        var trip = await fixture.DbContext.Trips.SingleAsync();
        var driver = await fixture.DbContext.DriverProfiles.SingleAsync();
        Assert.Equal(TripStatus.WAITING_PAYMENT, trip.TripStatus);
        Assert.Equal(0m, trip.ActualFare);
        Assert.Equal(0m, trip.FinalFare);
        Assert.Equal(TripEndReason.DRIVER_UNABLE_TO_CONTINUE, trip.EndReason);
        Assert.Equal(TripTerminationCategory.STANDARD, trip.TerminationCategory);
        Assert.Equal(DriverWorkStatus.Offline, driver.WorkStatus);
        Assert.Empty(await fixture.DbContext.TripEndReconciliationRequests.ToListAsync());
    }

    [Fact]
    public async Task EndTrip_StartedByMistake_EndsOperationallyWhileFareAwaitsStaff()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.IN_PROGRESS,
            pricingSnapshotV1: true);

        var direct = await Assert.ThrowsAsync<BookingException>(() =>
            fixture.Service.EndTripAsync(
                fixture.DriverId,
                fixture.TripId,
                CancellationToken.None,
                TripEndReason.STARTED_BY_MISTAKE));
        Assert.Equal("trip.end_reconciliation_required", direct.Code);

        var request = await fixture.Service.RequestEndTripReconciliationAsync(
            fixture.DriverId,
            fixture.TripId,
            TripEndReason.STARTED_BY_MISTAKE,
            canContinueWorking: false,
            cancellationToken: CancellationToken.None);
        var pendingTrip = await fixture.DbContext.Trips.SingleAsync();
        var offlineDriver = await fixture.DbContext.DriverProfiles.SingleAsync();
        Assert.Equal(TripStatus.WAITING_PAYMENT, pendingTrip.TripStatus);
        Assert.Equal(UtcNow, pendingTrip.EndedAt);
        Assert.Null(pendingTrip.ActualFare);
        Assert.Null(pendingTrip.FinalFare);
        Assert.Equal(DriverWorkStatus.Offline, offlineDriver.WorkStatus);
        Assert.Equal(TripEndReconciliationStatus.PENDING, request.Status);

        var paymentException = await Assert.ThrowsAsync<BookingException>(() =>
            fixture.CreatePaymentService().GetDriverTripPaymentStatusAsync(
                fixture.DriverId,
                fixture.TripId,
                CancellationToken.None));
        Assert.Equal("trip.end_reconciliation_pending", paymentException.Code);

        await fixture.Service.ResolveEndTripReconciliationAsync(
            Guid.NewGuid(),
            fixture.TripId,
            request.RequestId,
            approved: true,
            resolutionNote: "Reviewed by staff",
            cancellationToken: CancellationToken.None);

        var trip = await fixture.DbContext.Trips.SingleAsync();
        Assert.Equal(TripStatus.WAITING_PAYMENT, trip.TripStatus);
        Assert.Equal(0m, trip.ActualFare);
        Assert.Equal(0m, trip.FinalFare);
        Assert.Equal(TripEndReason.STARTED_BY_MISTAKE, trip.EndReason);
        Assert.Equal(TripTerminationCategory.STANDARD, trip.TerminationCategory);
    }

    [Fact]
    public async Task EndTrip_RejectedExceptionalRequest_CanBeResubmittedWithoutReactivatingTrip()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.IN_PROGRESS,
            pricingSnapshotV1: true);
        var first = await fixture.Service.RequestEndTripReconciliationAsync(
            fixture.DriverId,
            fixture.TripId,
            TripEndReason.STARTED_BY_MISTAKE,
            canContinueWorking: true,
            cancellationToken: CancellationToken.None);
        await fixture.Service.ResolveEndTripReconciliationAsync(
            Guid.NewGuid(),
            fixture.TripId,
            first.RequestId,
            approved: false,
            resolutionNote: "Insufficient evidence",
            cancellationToken: CancellationToken.None);

        var paymentException = await Assert.ThrowsAsync<BookingException>(() =>
            fixture.CreatePaymentService().GetDriverTripPaymentStatusAsync(
                fixture.DriverId,
                fixture.TripId,
                CancellationToken.None));
        var second = await fixture.Service.RequestEndTripReconciliationAsync(
            fixture.DriverId,
            fixture.TripId,
            TripEndReason.STARTED_BY_MISTAKE,
            canContinueWorking: true,
            cancellationToken: CancellationToken.None);

        var trip = await fixture.DbContext.Trips.SingleAsync();
        Assert.Equal("trip.end_reconciliation_pending", paymentException.Code);
        Assert.NotEqual(first.RequestId, second.RequestId);
        Assert.Equal(TripEndReconciliationStatus.PENDING, second.Status);
        Assert.Equal(TripStatus.WAITING_PAYMENT, trip.TripStatus);
        Assert.Null(trip.ActualFare);
        Assert.Null(trip.FinalFare);
    }

    [Fact]
    public async Task EndTrip_WhenCustomerStopsAtZeroProgress_ChargesSnapshotMinimum()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.IN_PROGRESS,
            pricingSnapshotV1: true);

        await fixture.Service.EndTripAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None,
            TripEndReason.CUSTOMER_REQUESTED_STOP);
        var endedTrip = await fixture.DbContext.Trips.SingleAsync();
        Assert.Equal(TripStatus.WAITING_PAYMENT, endedTrip.TripStatus);
        Assert.Empty(await fixture.DbContext.TripEndReconciliationRequests.ToListAsync());
        var paymentResult = await fixture.CreatePaymentService().ConfirmCashPaymentAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);
        await fixture.Service.ConfirmReturnByCustomerAsync(
            fixture.CustomerId,
            fixture.TripId,
            vehicleReturnedConfirmed: true,
            CancellationToken.None,
            ratingScore: 5);
        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking)
            .Include(x => x.Payments)
            .SingleAsync(x => x.Id == fixture.TripId);
        Assert.Equal(0m, trip.ActualDistanceKm);
        Assert.Equal(30_000m, trip.ActualFare);
        Assert.Equal(20_000m, trip.FinalFare);
        Assert.Equal(PaymentStatus.Success, paymentResult.PaymentStatus);
        Assert.Equal(20_000m, paymentResult.Amount);
        Assert.NotNull(paymentResult.PaymentId);
        Assert.Single(trip.Payments);
        Assert.Equal(TripStatus.COMPLETED, trip.TripStatus);
        Assert.NotEqual(trip.Booking.EstimatedDistanceKm, trip.ActualDistanceKm);
        Assert.NotEqual(trip.Booking.EstimatedFare, trip.FinalFare);
    }

    [Fact]
    public async Task CustomerEarlyStop_AfterTripStarted_PreservesAcceptedLongPickupCompensation()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.IN_PROGRESS,
            pricingSnapshotV1: true);
        fixture.DbContext.BookingDriverOffers.Add(new BookingDriverOffer
        {
            BookingId = (await fixture.DbContext.Trips.SingleAsync()).BookingId,
            DriverId = fixture.DriverId,
            OfferStatus = DriverOfferStatus.CustomerConfirmed,
            OfferedAt = UtcNow.AddMinutes(-15),
            ConfirmedAt = UtcNow.AddMinutes(-10),
            ExpiresAt = UtcNow.AddMinutes(5),
            PickupDistanceKm = 7m,
            LongPickupCompensation = 6_000m
        });
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Service.EndTripAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None,
            TripEndReason.CUSTOMER_REQUESTED_STOP);
        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking).ThenInclude(x => x.BookingPromotions)
            .SingleAsync();
        var settlement = await fixture.FinancialSettlementService.GetOrCreateAsync(
            trip, false, CancellationToken.None);

        Assert.Equal(6_000m, settlement.LongPickupCompensation);
        Assert.Equal(settlement.DriverPayout, settlement.DriverEarning);
    }

    [Fact]
    public async Task LongPickupCompensation_IsNotEarnedBeforeFirstTripStart()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.ARRIVED,
            pricingSnapshotV1: true);
        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking).ThenInclude(x => x.BookingPromotions)
            .SingleAsync();
        fixture.DbContext.BookingDriverOffers.Add(new BookingDriverOffer
        {
            BookingId = trip.BookingId,
            DriverId = fixture.DriverId,
            OfferStatus = DriverOfferStatus.CustomerConfirmed,
            OfferedAt = UtcNow.AddMinutes(-15),
            ConfirmedAt = UtcNow.AddMinutes(-10),
            ExpiresAt = UtcNow.AddMinutes(5),
            PickupDistanceKm = 7m,
            LongPickupCompensation = 6_000m
        });
        trip.TripStatus = TripStatus.WAITING_PAYMENT;
        trip.TerminationCategory = TripTerminationCategory.STANDARD;
        trip.EndReason = TripEndReason.NORMAL_COMPLETION;
        trip.ActualFare = trip.Booking.EstimatedFare;
        trip.FinalFare = trip.Booking.EstimatedFare - 10_000m;
        await fixture.DbContext.SaveChangesAsync();

        var settlement = await fixture.FinancialSettlementService.GetOrCreateAsync(
            trip, false, CancellationToken.None);

        Assert.Null(trip.StartedAt);
        Assert.Equal(0m, settlement.LongPickupCompensation);
    }

    [Fact]
    public async Task EndTrip_WhenCustomerEndsEarly_UsesMonotonicPlannedProgress()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.IN_PROGRESS,
            pricingSnapshotV1: true);
        fixture.Redis.SetTripTrackingSnapshot(
            CreateV1RouteSnapshot(fixture.TripId, 38.5, -120.2, 2_000));
        await fixture.Redis.SetAsync(
            RedisKeys.TripPlannedRouteProgress(fixture.TripId),
            "0.5",
            TimeSpan.FromHours(1));

        await fixture.Service.EndTripAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None,
            TripEndReason.CUSTOMER_REQUESTED_STOP);
        await fixture.AddSuccessfulPaymentAsync(26_000m);
        await fixture.Service.AdvanceAfterSuccessfulPaymentAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);
        await fixture.Service.ConfirmReturnByCustomerAsync(
            fixture.CustomerId,
            fixture.TripId,
            vehicleReturnedConfirmed: true,
            CancellationToken.None,
            ratingScore: 5);

        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking)
            .SingleAsync(x => x.Id == fixture.TripId);

        Assert.Equal(2m, trip.ActualDistanceKm);
        Assert.Equal(36_000m, trip.ActualFare);
        Assert.Equal(26_000m, trip.FinalFare);
        Assert.Equal(0.5m, trip.PlannedRouteProgress);
        Assert.False(trip.DestinationReached);
        Assert.Equal(TripStatus.COMPLETED, trip.TripStatus);
        Assert.NotEqual(trip.Booking.EstimatedFare, trip.FinalFare);
    }

    [Fact]
    public async Task EndTrip_V1CustomerRequestedStop_WithLongDistance_UsesApprovedGrossAndComponentAllocation()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.IN_PROGRESS,
            pricingSnapshotV1: true,
            estimatedFare: 100_000m,
            longDistanceComponent: 20_000m);
        fixture.Redis.SetTripTrackingSnapshot(
            CreateV1RouteSnapshot(fixture.TripId, 38.5, -120.2, 2_000));
        await fixture.Redis.SetAsync(
            RedisKeys.TripPlannedRouteProgress(fixture.TripId),
            "0.5",
            TimeSpan.FromHours(1));

        await fixture.Service.EndTripAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None,
            TripEndReason.CUSTOMER_REQUESTED_STOP);

        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking)
            .SingleAsync(x => x.Id == fixture.TripId);

        Assert.Equal(2m, trip.ActualDistanceKm);
        Assert.Equal(0.5m, trip.PlannedRouteProgress);
        Assert.Equal(50_000m, trip.ActualFare);
        Assert.Equal(40_000m, trip.FinalFare);
        Assert.Equal(20_000m, trip.Booking.LongDistanceComponent);
        Assert.Equal(TripEndReason.CUSTOMER_REQUESTED_STOP, trip.EndReason);
        Assert.Empty(await fixture.DbContext.TripEndReconciliationRequests.ToListAsync());
    }

    [Fact]
    public async Task StartTrip_SeedsTrackingFromCurrentDriverLocation()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.ARRIVED);
        fixture.Redis.SetDriverLocation(
            fixture.DriverId,
            new DriverLocationCache(
                fixture.DriverId,
                10.762622,
                106.660172,
                UtcNow.AddSeconds(-5)));

        await fixture.Service.UpdateDriverTripStatusAsync(
            fixture.DriverId,
            fixture.TripId,
            TripStatus.IN_PROGRESS,
            CancellationToken.None);

        var point = Assert.Single(fixture.Redis.RecordedTrackingPoints);
        Assert.Equal(fixture.TripId, point.TripId);
        Assert.Equal(10.762622, point.Latitude);
        Assert.Equal(106.660172, point.Longitude);
    }

    [Fact]
    public async Task StartTrip_AfterRiskProtectionRolloutWithoutPass_IsRejected()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.ARRIVED,
            riskProtectionEnabled: true);

        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            fixture.Service.UpdateDriverTripStatusAsync(
                fixture.DriverId,
                fixture.TripId,
                TripStatus.IN_PROGRESS,
                CancellationToken.None));

        Assert.Equal("pretrip.pass_required", exception.Code);
        var trip = await fixture.DbContext.Trips.SingleAsync(x => x.Id == fixture.TripId);
        Assert.Equal(TripStatus.ARRIVED, trip.TripStatus);
        Assert.Null(trip.StartedAt);
        Assert.Empty(await fixture.DbContext.TripProtectionCoverages.ToListAsync());
    }

    [Fact]
    public async Task StartTrip_WhenLatestAttemptIsFail_IsRejected()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.ARRIVED,
            riskProtectionEnabled: true);
        var checkService = new PreTripVehicleCheckService(
            fixture.DbContext,
            fixture.RiskProtectionPolicyProvider,
            new DateTimeProviderFake(UtcNow));
        await checkService.CreateAsync(
            fixture.DriverId,
            fixture.TripId,
            PassedPreTripCheck(),
            null,
            CancellationToken.None);
        await checkService.CreateAsync(
            fixture.DriverId,
            fixture.TripId,
            FailedPreTripCheck(),
            null,
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            fixture.Service.UpdateDriverTripStatusAsync(
                fixture.DriverId,
                fixture.TripId,
                TripStatus.IN_PROGRESS,
                CancellationToken.None));

        Assert.Equal("pretrip.pass_required", exception.Code);
        Assert.Empty(await fixture.DbContext.TripProtectionCoverages.ToListAsync());
    }

    [Fact]
    public async Task StartTrip_AfterFailThenPass_ActivatesCoverageWithoutRequiringInsurance()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.ARRIVED,
            riskProtectionEnabled: true);
        var checkService = new PreTripVehicleCheckService(
            fixture.DbContext,
            fixture.RiskProtectionPolicyProvider,
            new DateTimeProviderFake(UtcNow));
        await checkService.CreateAsync(
            fixture.DriverId,
            fixture.TripId,
            FailedPreTripCheck(),
            null,
            CancellationToken.None);
        var pass = await checkService.CreateAsync(
            fixture.DriverId,
            fixture.TripId,
            PassedPreTripCheck(),
            null,
            CancellationToken.None);

        await fixture.Service.UpdateDriverTripStatusAsync(
            fixture.DriverId,
            fixture.TripId,
            TripStatus.IN_PROGRESS,
            CancellationToken.None);

        var trip = await fixture.DbContext.Trips.SingleAsync(x => x.Id == fixture.TripId);
        var coverage = await fixture.DbContext.TripProtectionCoverages.SingleAsync();
        Assert.Equal(TripStatus.IN_PROGRESS, trip.TripStatus);
        Assert.Equal(UtcNow, trip.StartedAt);
        Assert.Equal(pass.Id, coverage.PreTripVehicleCheckId);
        Assert.Equal(20_000_000m, coverage.ProtectionLimit);
        Assert.Equal(UtcNow, coverage.ActivatedAtUtc);
        Assert.Null(coverage.VehicleInsurancePolicyId);
    }

    [Fact]
    public async Task StartTrip_WithVerifiedPhysicalDamageInsurance_SnapshotsPolicyValues()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.ARRIVED,
            riskProtectionEnabled: true);
        var vehicleId = await fixture.DbContext.Trips
            .Where(x => x.Id == fixture.TripId)
            .Select(x => x.Booking.VehicleId)
            .SingleAsync();
        var insurance = new VehicleInsurancePolicy
        {
            VehicleId = vehicleId,
            InsuranceType = VehicleInsuranceType.PHYSICAL_DAMAGE,
            Provider = "Safe Insurer",
            PolicyNumber = "POLICY-001",
            EffectiveFromUtc = UtcNow.AddDays(-1),
            ExpiresAtUtc = UtcNow.AddDays(30),
            CoverageAmount = 15_000_000m,
            Deductible = 500_000m,
            VerificationStatus = InsuranceVerificationStatus.VERIFIED,
            CreatedAtUtc = UtcNow
        };
        var higherCoverageMandatoryTpl = new VehicleInsurancePolicy
        {
            VehicleId = vehicleId,
            InsuranceType = VehicleInsuranceType.MANDATORY_TPL,
            Provider = "Mandatory TPL Insurer",
            PolicyNumber = "TPL-001",
            EffectiveFromUtc = UtcNow.AddDays(-1),
            ExpiresAtUtc = UtcNow.AddDays(30),
            CoverageAmount = 100_000_000m,
            Deductible = 0m,
            VerificationStatus = InsuranceVerificationStatus.VERIFIED,
            CreatedAtUtc = UtcNow
        };
        fixture.DbContext.VehicleInsurancePolicies.AddRange(insurance, higherCoverageMandatoryTpl);
        await fixture.DbContext.SaveChangesAsync();
        var checkService = new PreTripVehicleCheckService(
            fixture.DbContext,
            fixture.RiskProtectionPolicyProvider,
            new DateTimeProviderFake(UtcNow));
        await checkService.CreateAsync(
            fixture.DriverId,
            fixture.TripId,
            PassedPreTripCheck(),
            null,
            CancellationToken.None);

        await fixture.Service.UpdateDriverTripStatusAsync(
            fixture.DriverId,
            fixture.TripId,
            TripStatus.IN_PROGRESS,
            CancellationToken.None);

        var coverage = await fixture.DbContext.TripProtectionCoverages.SingleAsync();
        Assert.Equal(insurance.Id, coverage.VehicleInsurancePolicyId);
        Assert.Equal("Safe Insurer", coverage.InsuranceProviderSnapshot);
        Assert.Equal("POLICY-001", coverage.PolicyNumberSnapshot);
        Assert.Equal(15_000_000m, coverage.InsuranceCoverageSnapshot);
        Assert.Equal(500_000m, coverage.InsuranceDeductibleSnapshot);

        insurance.Provider = "Changed Insurer";
        insurance.PolicyNumber = "CHANGED-POLICY";
        insurance.CoverageAmount = 1m;
        insurance.Deductible = 0m;
        await fixture.DbContext.SaveChangesAsync();
        fixture.DbContext.ChangeTracker.Clear();

        var persistedSnapshot = await fixture.DbContext.TripProtectionCoverages
            .AsNoTracking()
            .SingleAsync(x => x.TripId == fixture.TripId);
        Assert.Equal("Safe Insurer", persistedSnapshot.InsuranceProviderSnapshot);
        Assert.Equal("POLICY-001", persistedSnapshot.PolicyNumberSnapshot);
        Assert.Equal(15_000_000m, persistedSnapshot.InsuranceCoverageSnapshot);
        Assert.Equal(500_000m, persistedSnapshot.InsuranceDeductibleSnapshot);
    }

    [Fact]
    public async Task InProgressStatusRetry_DoesNotBackfillHistoricalCoverage()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.IN_PROGRESS,
            riskProtectionEnabled: true);

        await fixture.Service.UpdateDriverTripStatusAsync(
            fixture.DriverId,
            fixture.TripId,
            TripStatus.IN_PROGRESS,
            CancellationToken.None);

        Assert.Empty(await fixture.DbContext.TripProtectionCoverages.ToListAsync());
    }

    [Fact]
    public async Task ConcurrentStart_WhenCoverageWinnerAlreadyCommitted_IsIdempotent()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.ARRIVED,
            riskProtectionEnabled: true,
            simulateConcurrentStartWinner: true);

        await fixture.Service.UpdateDriverTripStatusAsync(
            fixture.DriverId,
            fixture.TripId,
            TripStatus.IN_PROGRESS,
            CancellationToken.None);

        var trip = await fixture.DbContext.Trips.AsNoTracking()
            .SingleAsync(x => x.Id == fixture.TripId);
        Assert.Equal(TripStatus.IN_PROGRESS, trip.TripStatus);
        Assert.Equal(UtcNow, trip.StartedAt);
        Assert.Single(await fixture.DbContext.TripProtectionCoverages.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task ConfirmReturnByCustomer_AfterPayment_RatesAndCompletesTrip()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.WAITING_RETURN_CONFIRM);
        fixture.Redis.SetTripTrackingSnapshot(
            CreateTripTrackingSnapshot(fixture.TripId, 5_200));
        await fixture.AddSuccessfulPaymentAsync(62_000m);
        await fixture.Service.ConfirmReturnByCustomerAsync(
            fixture.CustomerId,
            fixture.TripId,
            vehicleReturnedConfirmed: true,
            CancellationToken.None,
            ratingScore: 5,
            comment: "Safe trip");
        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
                    .ThenInclude(x => x.Promotion)
            .Include(x => x.Payments)
            .Include(x => x.ReturnConfirmations)
            .Include(x => x.Rating)
            .SingleAsync(x => x.Id == fixture.TripId);
        var confirmation = Assert.Single(trip.ReturnConfirmations);
        Assert.Equal(TripStatus.COMPLETED, trip.TripStatus);
        Assert.Equal(BookingStatus.Completed, trip.Booking.BookingStatus);
        Assert.Equal(UtcNow, trip.CompletedAt);
        Assert.Equal(3, fixture.Promotion.CurrentUsageCount);
        var payment = Assert.Single(trip.Payments);
        Assert.Equal(PaymentMethod.QR, payment.PaymentMethod);
        Assert.Equal(PaymentStatus.Success, payment.PaymentStatus);
        Assert.Equal(62_000m, payment.Amount);
        Assert.Equal("VND", payment.Currency);
        Assert.Equal(fixture.DriverId, confirmation.DriverId);
        Assert.Equal(fixture.CustomerId, confirmation.ConfirmedByUserId);
        Assert.Equal(HandoverStatus.CustomerConfirmed, confirmation.HandoverStatus);
        Assert.Equal(UtcNow, confirmation.ConfirmedAt);
        Assert.Empty(confirmation.Evidence);
        Assert.NotNull(trip.Rating);
        Assert.Equal(5, trip.Rating.RatingScore);
        Assert.Equal("Safe trip", trip.Rating.Comment);
        Assert.Equal(fixture.CustomerId, trip.Rating.CustomerId);
        Assert.Equal(fixture.DriverId, trip.Rating.DriverId);
        Assert.Collection(
            fixture.Realtime.TripStatusNotifications,
            notification =>
            {
                Assert.Equal(fixture.TripId, notification.TripId);
                Assert.Equal(trip.BookingId, notification.BookingId);
                Assert.Equal(fixture.CustomerId, notification.CustomerId);
                Assert.Equal(fixture.DriverId, notification.DriverId);
                Assert.Equal(TripStatus.RETURN_CONFIRMED, notification.TripStatus);
                Assert.Equal(BookingStatus.DriverAssigned, notification.BookingStatus);
            },
            notification =>
            {
                Assert.Equal(TripStatus.COMPLETED, notification.TripStatus);
                Assert.Equal(BookingStatus.Completed, notification.BookingStatus);
            });
        Assert.Empty(fixture.Realtime.TripPaymentPendingNotifications);
        Assert.Single(fixture.Realtime.BookingStatusNotifications);
        Assert.Contains(fixture.TripLiveKey, fixture.Redis.RemovedKeys);
        Assert.Contains(fixture.DriverActiveTripKey, fixture.Redis.RemovedKeys);
        var wallet = await fixture.DbContext.DriverWallets.SingleAsync();
        var payout = await fixture.DbContext.WalletTransactions.SingleAsync();
        Assert.Equal(50_400m, wallet.CurrentBalance);
        Assert.Equal(50_400m, payout.Amount);
        Assert.Equal(WalletTransactionType.Income, payout.TransactionType);
    }

    [Fact]
    public async Task ConfirmReturnByCustomer_WhenReturnWasPersisted_ResumesCompletionWithoutDuplicates()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.WAITING_RETURN_CONFIRM);
        await fixture.AddSuccessfulPaymentAsync(62_000m);
        fixture.Realtime.FailOnceForTripStatus = TripStatus.RETURN_CONFIRMED;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.ConfirmReturnByCustomerAsync(
                fixture.CustomerId,
                fixture.TripId,
                vehicleReturnedConfirmed: true,
                CancellationToken.None,
                ratingScore: 5,
                comment: "Safe trip"));

        Assert.Equal(
            TripStatus.RETURN_CONFIRMED,
            (await fixture.DbContext.Trips.SingleAsync(x => x.Id == fixture.TripId)).TripStatus);

        await fixture.Service.ConfirmReturnByCustomerAsync(
            fixture.CustomerId,
            fixture.TripId,
            vehicleReturnedConfirmed: true,
            CancellationToken.None,
            ratingScore: 5,
            comment: "Safe trip");

        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking)
            .Include(x => x.ReturnConfirmations)
            .Include(x => x.Rating)
            .SingleAsync(x => x.Id == fixture.TripId);
        Assert.Equal(TripStatus.COMPLETED, trip.TripStatus);
        Assert.Equal(BookingStatus.Completed, trip.Booking.BookingStatus);
        Assert.Single(trip.ReturnConfirmations);
        Assert.NotNull(trip.Rating);
        Assert.Equal("Safe trip", trip.Rating.Comment);
        Assert.Equal(3, fixture.Promotion.CurrentUsageCount);
    }

    [Fact]
    public async Task ConfirmReturnByCustomer_CannotResumeAfterTripEnded()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.WAITING_RETURN_CONFIRM);
        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            fixture.Service.ConfirmReturnByCustomerAsync(
                fixture.CustomerId,
                fixture.TripId,
                vehicleReturnedConfirmed: false,
                CancellationToken.None));
        var trip = await fixture.DbContext.Trips
            .Include(x => x.ReturnConfirmations)
            .SingleAsync(x => x.Id == fixture.TripId);
        Assert.Equal("trip.return_confirmation_required", exception.Code);
        Assert.Equal(TripStatus.WAITING_RETURN_CONFIRM, trip.TripStatus);
        Assert.Empty(trip.ReturnConfirmations);
        Assert.Empty(fixture.Realtime.TripStatusNotifications);
    }

    [Fact]
    public async Task ConfirmReturnByCustomer_BeforePayment_IsRejected()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.WAITING_RETURN_CONFIRM);
        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            fixture.Service.ConfirmReturnByCustomerAsync(
                fixture.CustomerId,
                fixture.TripId,
                vehicleReturnedConfirmed: true,
                CancellationToken.None,
                ratingScore: 5));

        var trip = await fixture.DbContext.Trips
            .Include(x => x.ReturnConfirmations)
            .Include(x => x.Rating)
            .SingleAsync(x => x.Id == fixture.TripId);

        Assert.Equal("payment.required_before_return_confirmation", exception.Code);
        Assert.Equal(TripStatus.WAITING_RETURN_CONFIRM, trip.TripStatus);
        Assert.Empty(trip.ReturnConfirmations);
        Assert.Null(trip.Rating);
    }

    [Fact]
    public async Task ConfirmReturnByDriver_AfterPayment_CompletesWithoutCustomerRating()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.WAITING_RETURN_CONFIRM);
        await fixture.AddSuccessfulPaymentAsync(62_000m);
        await using var evidenceStream = new MemoryStream([0xFF, 0xD8, 0xFF]);

        await fixture.Service.ConfirmReturnByDriverAsync(
            fixture.DriverId,
            fixture.TripId,
            [new ReturnEvidenceItem(evidenceStream, "return.jpg", "image/jpeg", 3)],
            note: "Driver confirmed for customer",
            CancellationToken.None);

        var trip = await fixture.DbContext.Trips
            .Include(item => item.ReturnConfirmations)
                .ThenInclude(item => item.Evidence)
            .Include(item => item.Rating)
            .SingleAsync(item => item.Id == fixture.TripId);
        var confirmation = Assert.Single(trip.ReturnConfirmations);

        Assert.Equal(TripStatus.COMPLETED, trip.TripStatus);
        Assert.Equal(HandoverStatus.DriverConfirmed, confirmation.HandoverStatus);
        Assert.Single(confirmation.Evidence);
        Assert.Null(trip.Rating);
    }

    [Fact]
    public async Task ConfirmReturnByDriver_WhenReturnWasPersisted_ResumesWithoutDeletingEvidence()
    {
        var storage = new TrackingTripReturnEvidenceStorage();
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.WAITING_RETURN_CONFIRM,
            returnEvidenceStorage: storage);
        await fixture.AddSuccessfulPaymentAsync(62_000m);
        fixture.Realtime.FailOnceForTripStatus = TripStatus.RETURN_CONFIRMED;
        await using var firstEvidence = new MemoryStream([0xFF, 0xD8, 0xFF]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.ConfirmReturnByDriverAsync(
                fixture.DriverId,
                fixture.TripId,
                [new ReturnEvidenceItem(firstEvidence, "return.jpg", "image/jpeg", 3)],
                note: "Driver confirmed for customer",
                CancellationToken.None));

        Assert.Equal(
            TripStatus.RETURN_CONFIRMED,
            (await fixture.DbContext.Trips.SingleAsync(x => x.Id == fixture.TripId)).TripStatus);
        Assert.Equal(1, storage.SaveCalls);
        Assert.Empty(storage.DeletedPublicIds);

        await using var retryEvidence = new MemoryStream([0xFF, 0xD8, 0xFF]);
        await fixture.Service.ConfirmReturnByDriverAsync(
            fixture.DriverId,
            fixture.TripId,
            [new ReturnEvidenceItem(retryEvidence, "return.jpg", "image/jpeg", 3)],
            note: "Driver confirmed for customer",
            CancellationToken.None);

        var trip = await fixture.DbContext.Trips
            .Include(x => x.ReturnConfirmations)
                .ThenInclude(x => x.Evidence)
            .SingleAsync(x => x.Id == fixture.TripId);
        Assert.Equal(TripStatus.COMPLETED, trip.TripStatus);
        Assert.Equal(1, storage.SaveCalls);
        Assert.Empty(storage.DeletedPublicIds);
        Assert.Single(Assert.Single(trip.ReturnConfirmations).Evidence);
    }

    [Theory]
    [InlineData(FileSafetyScanStatus.ThreatDetected, "trip.return_evidence_malware_detected", 400)]
    [InlineData(FileSafetyScanStatus.ScannerUnavailable, "trip.return_evidence_scanner_unavailable", 503)]
    public async Task ConfirmReturnByDriver_UnsafeScannerOutcome_DoesNotUploadOrPersist(
        FileSafetyScanStatus status,
        string expectedCode,
        int expectedStatus)
    {
        var storage = new TrackingTripReturnEvidenceStorage();
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.WAITING_RETURN_CONFIRM,
            evidenceFileValidator: TestEvidenceValidation.Create(new TestFileSafetyScanner(status)),
            returnEvidenceStorage: storage);
        await fixture.AddSuccessfulPaymentAsync(62_000m);
        await using var evidenceStream = new MemoryStream([0xFF, 0xD8, 0xFF]);

        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            fixture.Service.ConfirmReturnByDriverAsync(
                fixture.DriverId,
                fixture.TripId,
                [new ReturnEvidenceItem(evidenceStream, "return.jpg", "image/jpeg", 3)],
                null,
                CancellationToken.None));

        Assert.Equal(expectedCode, exception.Code);
        Assert.Equal(expectedStatus, exception.StatusCode);
        Assert.Equal(0, storage.SaveCalls);
        Assert.Empty(fixture.DbContext.TripReturnConfirmations);
    }

    [Fact]
    public async Task ConfirmReturnByDriver_WhenSecondUploadFails_CleansFirstUpload()
    {
        var storage = new TrackingTripReturnEvidenceStorage { FailOnSaveCall = 2 };
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.WAITING_RETURN_CONFIRM,
            returnEvidenceStorage: storage);
        await fixture.AddSuccessfulPaymentAsync(62_000m);
        await using var first = new MemoryStream([0xFF, 0xD8, 0xFF, 1]);
        await using var second = new MemoryStream([0xFF, 0xD8, 0xFF, 2]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Service.ConfirmReturnByDriverAsync(
                fixture.DriverId,
                fixture.TripId,
                [
                    new ReturnEvidenceItem(first, "first.jpg", "image/jpeg", 4),
                    new ReturnEvidenceItem(second, "second.jpg", "image/jpeg", 4)
                ],
                null,
                CancellationToken.None));

        Assert.Equal(2, storage.SaveCalls);
        Assert.Equal(["return-1"], storage.DeletedPublicIds);
        Assert.Empty(fixture.DbContext.TripReturnConfirmations);
    }


    [Fact]
    public async Task ConfirmCashPayment_WhenWalletIsMissingOrInsufficient_IsRejected()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.WAITING_PAYMENT);
        var paymentService = fixture.CreatePaymentService();

        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            paymentService.ConfirmCashPaymentAsync(
                fixture.DriverId,
                fixture.TripId,
                CancellationToken.None));

        var trip = await fixture.DbContext.Trips
            .Include(x => x.Payments)
            .SingleAsync(x => x.Id == fixture.TripId);

        Assert.Equal("payment.insufficient_driver_wallet", exception.Code);
        Assert.Equal(TripStatus.WAITING_PAYMENT, trip.TripStatus);
        Assert.Empty(trip.Payments);
    }

    [Fact]
    public async Task CompleteTrip_AfterReturnConfirmed_CompletesAndIncrementsPromotionUsage()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.RETURN_CONFIRMED);
        fixture.DbContext.DriverWallets.Add(new DriverWallet
        {
            DriverId = fixture.DriverId,
            CurrentBalance = 100_000m
        });
        fixture.DbContext.Payments.Add(new Payment
        {
            TripId = fixture.TripId,
            PaymentMethod = PaymentMethod.CASH,
            Amount = 62_000m,
            Currency = "VND",
            PaymentStatus = PaymentStatus.Success,
            PaidAt = UtcNow,
            CreatedAt = UtcNow
        });
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Service.CompleteTripAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);

        await fixture.Service.CompleteTripAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);

        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
                    .ThenInclude(x => x.Promotion)
            .SingleAsync(x => x.Id == fixture.TripId);

        Assert.Equal(TripStatus.COMPLETED, trip.TripStatus);
        Assert.Equal(BookingStatus.Completed, trip.Booking.BookingStatus);
        Assert.Equal(UtcNow, trip.CompletedAt);
        Assert.Equal(3, fixture.Promotion.CurrentUsageCount);
        var notification = Assert.Single(fixture.Realtime.TripStatusNotifications);
        Assert.Equal(TripStatus.COMPLETED, notification.TripStatus);
        Assert.Equal(BookingStatus.Completed, notification.BookingStatus);
        Assert.Empty(fixture.Realtime.TripPaymentPendingNotifications);
        Assert.Empty(fixture.Realtime.TripPaymentSucceededNotifications);
        Assert.Single(fixture.Realtime.BookingStatusNotifications);
        Assert.NotNull((await fixture.DbContext.TripFinancialSettlements.SingleAsync()).SettledAtUtc);
        Assert.Single(await fixture.DbContext.WalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task CompleteTrip_AfterReturnConfirmedWithZeroPay_UsesSettledSnapshotWithoutPaymentRow()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.RETURN_CONFIRMED);
        var policyId = await fixture.DbContext.RiskProtectionPolicyVersions
            .Select(x => x.Id)
            .SingleAsync();
        fixture.DbContext.TripFinancialSettlements.Add(new TripFinancialSettlement
        {
            TripId = fixture.TripId,
            PolicyVersionId = policyId,
            CommissionBase = 10_000m,
            PromotionExpense = 10_000m,
            CustomerPayableAmount = 0m,
            PlatformCommissionRate = .30m,
            GrossPlatformCommission = 3_000m,
            DriverEarning = 7_000m,
            NetPlatformCommission = -7_000m,
            RiskReserveRate = 0m,
            RiskContribution = 0m,
            NetOperatingRevenue = -7_000m,
            SettledAtUtc = UtcNow,
            CreatedAtUtc = UtcNow
        });
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Service.CompleteTripAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);

        Assert.Equal(
            TripStatus.COMPLETED,
            (await fixture.DbContext.Trips.SingleAsync(x => x.Id == fixture.TripId)).TripStatus);
        Assert.Empty(await fixture.DbContext.Payments.ToListAsync());
    }

    [Fact]
    public async Task ConfirmCashPayment_WhenPromotionCoversFare_CreditsSubsidyWithoutZeroPaymentRow()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.WAITING_PAYMENT);
        var policyId = await fixture.DbContext.RiskProtectionPolicyVersions
            .Select(x => x.Id)
            .SingleAsync();
        fixture.DbContext.TripFinancialSettlements.Add(new TripFinancialSettlement
        {
            TripId = fixture.TripId,
            PolicyVersionId = policyId,
            CommissionBase = 10_000m,
            PromotionExpense = 10_000m,
            CustomerPayableAmount = 0m,
            PlatformCommissionRate = .30m,
            GrossPlatformCommission = 3_000m,
            DriverEarning = 7_000m,
            NetPlatformCommission = -7_000m,
            RiskReserveRate = 0m,
            RiskContribution = 0m,
            NetOperatingRevenue = -7_000m,
            CreatedAtUtc = UtcNow
        });
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.CreatePaymentService().ConfirmCashPaymentAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);

        Assert.Equal(PaymentStatus.Success, result.PaymentStatus);
        Assert.Equal(TripStatus.WAITING_RETURN_CONFIRM, result.TripStatus);
        Assert.Empty(await fixture.DbContext.Payments.ToListAsync());
        Assert.Equal(7_000m, (await fixture.DbContext.DriverWallets.SingleAsync()).CurrentBalance);
        var subsidy = await fixture.DbContext.WalletTransactions.SingleAsync();
        Assert.Equal(WalletTransactionType.Bonus, subsidy.TransactionType);
        Assert.Equal(7_000m, subsidy.Amount);
    }

    [Fact]
    public async Task CashPayment_WhenPromotionExceedsCommission_DriverIncomeDoesNotDoubleCountSubsidy()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.WAITING_PAYMENT);
        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
            .SingleAsync(x => x.Id == fixture.TripId);
        trip.ActualFare = 100_000m;
        trip.FinalFare = 60_000m;
        trip.Booking.BookingPromotions.Single().DiscountAmount = 40_000m;
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.CreatePaymentService().ConfirmCashPaymentAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);

        var wallet = await new DriverQueryService(
                fixture.DbContext,
                null!,
                null!,
                fixture.CommissionCalculator,
                Options.Create(new DriverCompensationOptions()))
            .GetWalletAsync(
                fixture.DriverId,
                SafeRide.Application.Features.Drivers.DTOs.WalletPeriod.Week,
                0,
                10,
                CancellationToken.None);

        Assert.Equal(70_000m, result.DriverShare);
        Assert.Equal(-10_000m, result.PlatformShare);
        Assert.Equal(70_000m, wallet.Income.Total);
        Assert.Equal(10_000m, (await fixture.DbContext.DriverWallets.SingleAsync()).CurrentBalance);
        var subsidy = await fixture.DbContext.WalletTransactions.SingleAsync();
        Assert.Equal(WalletTransactionType.Bonus, subsidy.TransactionType);
        Assert.Equal(10_000m, subsidy.Amount);
    }

    [Fact]
    public async Task OpenOffer_IsLongPickupUsesDistanceThresholdEvenWhenCompensationRateIsZero()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.ACCEPTED);
        var trip = await fixture.DbContext.Trips.Include(x => x.Booking).SingleAsync();
        trip.Booking.BookingStatus = BookingStatus.Searching;
        fixture.DbContext.BookingDriverOffers.Add(new BookingDriverOffer
        {
            BookingId = trip.BookingId,
            DriverId = fixture.DriverId,
            OfferStatus = DriverOfferStatus.Sent,
            OfferedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(1),
            PickupDistanceKm = 7m,
            LongPickupCompensation = 0m
        });
        await fixture.DbContext.SaveChangesAsync();

        var requests = await new DriverQueryService(
            fixture.DbContext,
            null!,
            null!,
            fixture.CommissionCalculator,
            Options.Create(new DriverCompensationOptions
            {
                LongPickupThresholdKm = 5,
                LongPickupRatePerKm = 0m
            }))
            .GetOpenTripRequestsAsync(fixture.DriverId, CancellationToken.None);

        Assert.True(Assert.Single(requests).IsLongPickup);
    }

    [Fact]
    public async Task CashZeroPay_WhenPromotionExceedsActualFare_PreservesFullExpenseAndDriverEarning()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.WAITING_PAYMENT);
        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
            .SingleAsync(x => x.Id == fixture.TripId);
        trip.ActualFare = 100_000m;
        trip.FinalFare = 0m;
        trip.Booking.BookingPromotions.Single().DiscountAmount = 120_000m;
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.CreatePaymentService().ConfirmCashPaymentAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);

        var settlement = await fixture.DbContext.TripFinancialSettlements.SingleAsync();
        var wallet = await new DriverQueryService(
                fixture.DbContext,
                null!,
                null!,
                fixture.CommissionCalculator,
                Options.Create(new DriverCompensationOptions()))
            .GetWalletAsync(
                fixture.DriverId,
                SafeRide.Application.Features.Drivers.DTOs.WalletPeriod.Week,
                0,
                10,
                CancellationToken.None);

        Assert.Equal(PaymentStatus.Success, result.PaymentStatus);
        Assert.Equal(0m, settlement.CustomerPayableAmount);
        Assert.Equal(120_000m, settlement.PromotionExpense);
        Assert.Equal(70_000m, settlement.DriverEarning);
        Assert.Equal(-90_000m, settlement.NetPlatformCommission);
        Assert.Equal(0m, settlement.RiskContribution);
        Assert.Equal(70_000m, wallet.Income.Total);
        Assert.Equal(70_000m, (await fixture.DbContext.DriverWallets.SingleAsync()).CurrentBalance);
        var subsidy = await fixture.DbContext.WalletTransactions.SingleAsync();
        Assert.Equal(WalletSettlementEffect.CashPromotionSubsidy, subsidy.SettlementEffect);
        Assert.Equal(70_000m, subsidy.Amount);
        Assert.Empty(await fixture.DbContext.Payments.ToListAsync());
    }

    [Fact]
    public async Task CustomerZeroPrepayment_DefersSnapshotAndDriverPayoutUntilTripEnds()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.DRIVER_ARRIVING,
            estimatedFare: 10_000m);
        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
            .SingleAsync(x => x.Id == fixture.TripId);
        var paymentService = fixture.CreatePaymentService();
        var prepayment = await paymentService.CreateQrPaymentAsync(
            fixture.CustomerId,
            fixture.TripId,
            returnUrl: null,
            cancelUrl: null,
            CancellationToken.None);

        Assert.Equal(PaymentStatus.Pending, prepayment.PaymentStatus);
        Assert.Equal("PAYMENT_AFTER_TRIP", prepayment.OrderCode);
        Assert.Empty(await fixture.DbContext.Payments.ToListAsync());
        Assert.Empty(await fixture.DbContext.TripFinancialSettlements.ToListAsync());
        Assert.Empty(await fixture.DbContext.DriverWallets.ToListAsync());

        trip.TripStatus = TripStatus.WAITING_PAYMENT;
        trip.ActualFare = 10_000m;
        trip.FinalFare = 0m;
        trip.EndedAt = UtcNow;
        await fixture.DbContext.SaveChangesAsync();

        var completed = await paymentService.CreateDriverQrPaymentAsync(
            fixture.DriverId,
            fixture.TripId,
            returnUrl: null,
            cancelUrl: null,
            CancellationToken.None);

        Assert.Equal(PaymentStatus.Success, completed.PaymentStatus);
        Assert.Equal(TripStatus.WAITING_RETURN_CONFIRM, completed.TripStatus);
        Assert.Empty(await fixture.DbContext.Payments.ToListAsync());
        var settlement = await fixture.DbContext.TripFinancialSettlements.SingleAsync();
        Assert.Equal(7_000m, settlement.DriverEarning);
        Assert.NotNull(settlement.SettledAtUtc);
        Assert.Equal(7_000m, (await fixture.DbContext.DriverWallets.SingleAsync()).CurrentBalance);
    }

    [Fact]
    public async Task PrepaidQrPayment_BeforeTrip_DoesNotCompleteTripOrCreditDriverWallet()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.DRIVER_ARRIVING);
        fixture.DbContext.Payments.Add(new Payment
        {
            TripId = fixture.TripId,
            PaymentMethod = PaymentMethod.QR,
            TransactionReference = "123456789",
            Amount = 62_000m,
            Currency = "VND",
            PaymentStatus = PaymentStatus.Success,
            PaidAt = UtcNow,
            CreatedAt = UtcNow
        });
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.CreatePaymentService().GetTripPaymentStatusAsync(
            fixture.CustomerId,
            fixture.TripId,
            CancellationToken.None);

        Assert.Equal(PaymentStatus.Success, result.PaymentStatus);
        Assert.Equal(TripStatus.DRIVER_ARRIVING, result.TripStatus);
        Assert.Equal(
            TripStatus.DRIVER_ARRIVING,
            (await fixture.DbContext.Trips.SingleAsync(x => x.Id == fixture.TripId)).TripStatus);
        Assert.Empty(await fixture.DbContext.DriverWallets.ToListAsync());
        Assert.Empty(await fixture.DbContext.WalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task CashPayment_AfterTripEnds_WaitsForReturnConfirmation()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.WAITING_RETURN_CONFIRM);
        fixture.DbContext.DriverWallets.Add(new DriverWallet
        {
            DriverId = fixture.DriverId,
            CurrentBalance = 100_000m
        });
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.CreatePaymentService().ConfirmCashPaymentAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);

        Assert.Equal(PaymentStatus.Success, result.PaymentStatus);
        Assert.Equal(TripStatus.WAITING_RETURN_CONFIRM, result.TripStatus);

        await fixture.Service.ConfirmReturnByCustomerAsync(
            fixture.CustomerId,
            fixture.TripId,
            vehicleReturnedConfirmed: true,
            CancellationToken.None,
            ratingScore: 5);

        Assert.Equal(
            TripStatus.COMPLETED,
            (await fixture.DbContext.Trips.SingleAsync(x => x.Id == fixture.TripId)).TripStatus);
    }

    [Fact]
    public async Task ConfirmReturnByCustomer_WithInvalidRating_DoesNotOpenPayment()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.WAITING_RETURN_CONFIRM);

        var exception = await Assert.ThrowsAsync<RatingException>(() =>
            fixture.Service.ConfirmReturnByCustomerAsync(
                fixture.CustomerId,
                fixture.TripId,
                vehicleReturnedConfirmed: true,
                CancellationToken.None,
                ratingScore: 6));

        var trip = await fixture.DbContext.Trips
            .Include(x => x.ReturnConfirmations)
            .Include(x => x.Payments)
            .Include(x => x.Rating)
            .SingleAsync(x => x.Id == fixture.TripId);

        Assert.Equal("rating.invalid_score", exception.Code);
        Assert.Equal(TripStatus.WAITING_RETURN_CONFIRM, trip.TripStatus);
        Assert.Empty(trip.ReturnConfirmations);
        Assert.Empty(trip.Payments);
        Assert.Null(trip.Rating);
    }

    [Fact]
    public async Task CreateCustomerQrPayment_AfterTrip_IsRejected()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.WAITING_RETURN_CONFIRM);

        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            fixture.CreatePaymentService().CreateQrPaymentAsync(
                fixture.CustomerId,
                fixture.TripId,
                returnUrl: null,
                cancelUrl: null,
                CancellationToken.None));

        Assert.Equal("payment.prepayment_window_closed", exception.Code);
        Assert.Equal(409, exception.StatusCode);
        Assert.Empty(await fixture.DbContext.Payments.ToListAsync());
    }

    [Fact]
    public async Task GetDriverPaymentStatus_BeforeMethodSelection_ReturnsFinalFare()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.WAITING_RETURN_CONFIRM);

        var result = await fixture.CreatePaymentService().GetDriverTripPaymentStatusAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);

        Assert.Equal(62_000m, result.Amount);
        Assert.Equal(62_000m, result.FinalFare);
        Assert.Equal(PaymentStatus.Pending, result.PaymentStatus);
        Assert.Null(result.PaymentMethod);
        Assert.Empty(await fixture.DbContext.Payments.ToListAsync());
    }

    [Fact]
    public async Task QrUnderpayment_RemainsWaitingUntilSuccessfulPaymentsCoverFinalPayable()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.WAITING_PAYMENT);
        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking).ThenInclude(x => x.BookingPromotions)
            .Include(x => x.Payments)
            .SingleAsync();
        var settlement = await fixture.FinancialSettlementService.GetOrCreateAsync(
            trip, false, CancellationToken.None);
        await fixture.AddSuccessfulPaymentAsync(settlement.CustomerPayableAmount - 10_000m);

        var pending = await fixture.CreatePaymentService().GetDriverTripPaymentStatusAsync(
            fixture.DriverId, fixture.TripId, CancellationToken.None);

        Assert.Equal(TripStatus.WAITING_PAYMENT, pending.TripStatus);
        Assert.Equal(PaymentStatus.Pending, pending.PaymentStatus);
        Assert.Equal(10_000m, pending.RemainingPayableAmount);
        Assert.Empty(await fixture.DbContext.WalletTransactions.ToListAsync());

        fixture.DbContext.Payments.Add(new Payment
        {
            TripId = fixture.TripId,
            PaymentMethod = PaymentMethod.QR,
            TransactionReference = $"{fixture.TripId}124",
            Amount = 10_000m,
            Currency = "VND",
            PaymentStatus = PaymentStatus.Success,
            PaidAt = UtcNow.AddSeconds(1),
            CreatedAt = UtcNow.AddSeconds(1)
        });
        await fixture.DbContext.SaveChangesAsync();

        var paid = await fixture.CreatePaymentService().GetDriverTripPaymentStatusAsync(
            fixture.DriverId, fixture.TripId, CancellationToken.None);
        Assert.Equal(TripStatus.WAITING_RETURN_CONFIRM, paid.TripStatus);
        Assert.Equal(0m, paid.RemainingPayableAmount);
        Assert.Equal(settlement.CustomerPayableAmount, paid.SuccessfulPaymentAmount);
        Assert.Single(await fixture.DbContext.WalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task QrOverpayment_PersistsRefundObligationBeforeLifecycleAdvance()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.WAITING_PAYMENT);
        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking).ThenInclude(x => x.BookingPromotions)
            .Include(x => x.Payments)
            .SingleAsync();
        var settlement = await fixture.FinancialSettlementService.GetOrCreateAsync(
            trip, false, CancellationToken.None);
        await fixture.AddSuccessfulPaymentAsync(settlement.CustomerPayableAmount + 10_000m);

        var result = await fixture.CreatePaymentService().GetDriverTripPaymentStatusAsync(
            fixture.DriverId, fixture.TripId, CancellationToken.None);

        Assert.Equal(TripStatus.WAITING_RETURN_CONFIRM, result.TripStatus);
        Assert.Equal(10_000m, result.RefundObligationAmount);
        Assert.Equal(SafetyPaymentReconciliationStatus.REFUND_PENDING, result.ReconciliationStatus);
        var refund = await fixture.DbContext.ManualPaymentRefunds.SingleAsync();
        Assert.Equal(10_000m, refund.Amount);
        Assert.Equal(ManualRefundStatus.REFUND_PENDING, refund.Status);
        Assert.Single(await fixture.DbContext.WalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task GetDriverPaymentStatus_ComponentAwareSettlement_ReportsGrossFareAsOriginalFare()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.WAITING_RETURN_CONFIRM,
            pricingSnapshotV1: true,
            estimatedFare: 100_000m,
            longDistanceComponent: 20_000m);
        var trip = await fixture.DbContext.Trips.SingleAsync(x => x.Id == fixture.TripId);
        trip.ActualFare = 100_000m;
        trip.FinalFare = 90_000m;
        trip.EndReason = TripEndReason.NORMAL_COMPLETION;
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.CreatePaymentService().GetDriverTripPaymentStatusAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);

        Assert.Equal(100_000m, result.OriginalFare);
        Assert.Equal(90_000m, result.FinalFare);
        Assert.Equal(80_000m, (await fixture.DbContext.TripFinancialSettlements.SingleAsync()).CommissionBase);
    }

    [Fact]
    public async Task StartDriverPayment_ReturnsFareAndNotifiesCustomerToWait()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.WAITING_RETURN_CONFIRM);

        var result = await fixture.CreatePaymentService().StartDriverPaymentAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);

        var notification = Assert.Single(fixture.Realtime.TripPaymentPendingNotifications);
        Assert.Equal(62_000m, result.Amount);
        Assert.Equal(PaymentStatus.Pending, result.PaymentStatus);
        Assert.Equal(fixture.CustomerId, notification.CustomerId);
        Assert.Equal(fixture.DriverId, notification.DriverId);
        Assert.Equal(62_000m, notification.Amount);
        Assert.Equal(TripStatus.WAITING_RETURN_CONFIRM, notification.TripStatus);
        Assert.Null(notification.PaymentId);
        Assert.Null(notification.PaymentMethod);
        Assert.Empty(await fixture.DbContext.Payments.ToListAsync());
    }

    [Fact]
    public async Task PrepaidQrPayment_AfterTripEnds_CreditsWalletAndCompletesOnlyOnce()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.WAITING_PAYMENT);
        fixture.DbContext.Payments.Add(new Payment
        {
            TripId = fixture.TripId,
            PaymentMethod = PaymentMethod.QR,
            TransactionReference = "123456789",
            Amount = 62_000m,
            Currency = "VND",
            PaymentStatus = PaymentStatus.Success,
            PaidAt = UtcNow,
            CreatedAt = UtcNow
        });
        await fixture.DbContext.SaveChangesAsync();

        var paymentService = fixture.CreatePaymentService();
        var result = await paymentService.GetDriverTripPaymentStatusAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);

        Assert.Equal(TripStatus.WAITING_RETURN_CONFIRM, result.TripStatus);
        var wallet = await fixture.DbContext.DriverWallets.SingleAsync();
        var payout = await fixture.DbContext.WalletTransactions.SingleAsync();
        Assert.Equal(WalletTransactionType.Income, payout.TransactionType);
        Assert.Equal(wallet.Id, payout.WalletId);
        Assert.Equal(50_400m, wallet.CurrentBalance);

        await paymentService.GetDriverTripPaymentStatusAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);

        Assert.Single(await fixture.DbContext.WalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task ConfirmCashPayment_WaitsForReturnAndPublishesPaymentSucceeded()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.WAITING_PAYMENT);
        fixture.DbContext.Payments.Add(new Payment
        {
            TripId = fixture.TripId,
            PaymentMethod = PaymentMethod.CASH,
            Amount = 62_000m,
            Currency = "VND",
            PaymentStatus = PaymentStatus.Pending,
            CreatedAt = UtcNow
        });
        fixture.DbContext.DriverWallets.Add(new DriverWallet
        {
            DriverId = fixture.DriverId,
            CurrentBalance = 100_000m
        });
        await fixture.DbContext.SaveChangesAsync();

        var paymentService = fixture.CreatePaymentService();
        var result = await paymentService.ConfirmCashPaymentAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);
        var replay = await paymentService.ConfirmCashPaymentAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);

        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
                    .ThenInclude(x => x.Promotion)
            .Include(x => x.Payments)
            .SingleAsync(x => x.Id == fixture.TripId);
        var payment = Assert.Single(trip.Payments);

        Assert.Equal(PaymentStatus.Success, result.PaymentStatus);
        Assert.Equal(PaymentStatus.Success, replay.PaymentStatus);
        Assert.Equal(TripStatus.WAITING_RETURN_CONFIRM, result.TripStatus);
        Assert.Equal(TripStatus.WAITING_RETURN_CONFIRM, trip.TripStatus);
        Assert.Equal(BookingStatus.DriverAssigned, trip.Booking.BookingStatus);
        Assert.Equal(2, fixture.Promotion.CurrentUsageCount);
        Assert.Equal(PaymentStatus.Success, payment.PaymentStatus);
        Assert.Equal(PaymentMethod.CASH, payment.PaymentMethod);

        var succeeded = Assert.Single(fixture.Realtime.TripPaymentSucceededNotifications);
        Assert.Equal(fixture.TripId, succeeded.TripId);
        Assert.Equal(trip.BookingId, succeeded.BookingId);
        Assert.Equal(fixture.CustomerId, succeeded.CustomerId);
        Assert.Equal(fixture.DriverId, succeeded.DriverId);
        Assert.Equal(payment.Id, succeeded.PaymentId);
        Assert.Equal(PaymentMethod.CASH, succeeded.PaymentMethod);
        Assert.Equal(PaymentStatus.Success, succeeded.PaymentStatus);
        Assert.Equal(62_000m, succeeded.Amount);
        Assert.Equal("VND", succeeded.Currency);
        Assert.Equal(TripStatus.WAITING_RETURN_CONFIRM, succeeded.TripStatus);
        Assert.Equal(BookingStatus.DriverAssigned, succeeded.BookingStatus);
        Assert.Equal("Thanh toán đã hoàn tất.", succeeded.Message);

        var wallet = await new DriverQueryService(
                fixture.DbContext,
                null!,
                null!,
                fixture.CommissionCalculator,
                Options.Create(new DriverCompensationOptions()))
            .GetWalletAsync(
                fixture.DriverId,
                SafeRide.Application.Features.Drivers.DTOs.WalletPeriod.Week,
                0,
                10,
                CancellationToken.None);

        Assert.Equal(result.DriverShare, wallet.Income.Total);
        var cashReceipt = Assert.Single(wallet.RecentTransactions, x =>
            x.TripId == fixture.TripId
            && x.Type == WalletTransactionType.Income);
        Assert.Equal(payment.Amount, cashReceipt.Amount);
        Assert.True(cashReceipt.IsCredit);
        Assert.Contains("Đã nhận tiền mặt", cashReceipt.Title);

        var platformFee = Assert.Single(wallet.RecentTransactions, x =>
            x.TripId == fixture.TripId
            && x.Type == WalletTransactionType.Penalty);
        Assert.Equal(result.PlatformShare, platformFee.Amount);
        Assert.False(platformFee.IsCredit);
        Assert.Single(await fixture.DbContext.TripFinancialSettlements.ToListAsync());
        Assert.Single(await fixture.DbContext.WalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task SafetyTerminate_BeforeTripStarts_CancelsWithoutFareOrPromotionUsage()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.ARRIVED);

        await fixture.Service.SafetyTerminateAsync(
            fixture.DriverId,
            isStaff: false,
            fixture.TripId,
            "Phanh không đảm bảo an toàn",
            CancellationToken.None);

        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
            .SingleAsync(x => x.Id == fixture.TripId);
        var driver = await fixture.DbContext.DriverProfiles
            .SingleAsync(x => x.DriverId == fixture.DriverId);

        Assert.Equal(TripStatus.CANCELLED, trip.TripStatus);
        Assert.Equal(TripTerminationCategory.SAFETY, trip.TerminationCategory);
        Assert.Equal("Phanh không đảm bảo an toàn", trip.SafetyTerminationReason);
        Assert.Equal(UtcNow, trip.SafetyTerminatedAt);
        Assert.Null(trip.ActualDistanceKm);
        Assert.Null(trip.ActualDurationMinutes);
        Assert.Null(trip.ActualFare);
        Assert.Null(trip.FinalFare);
        Assert.Empty(trip.Booking.BookingPromotions);
        Assert.Equal(2, fixture.Promotion.CurrentUsageCount);
        Assert.Equal(DriverWorkStatus.Online, driver.WorkStatus);
        Assert.Empty(await fixture.DbContext.Payments.ToListAsync());
        Assert.Empty(await fixture.DbContext.TripFinancialSettlements.ToListAsync());
        Assert.Empty(await fixture.DbContext.RiskFundTransactions.ToListAsync());
        Assert.Contains(RedisKeys.TripTrackingPath(fixture.TripId), fixture.Redis.RemovedKeys);
    }

    [Fact]
    public async Task SafetyTerminate_AfterPaymentStage_RejectsBeforeMutatingTrip()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.WAITING_RETURN_CONFIRM);

        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            fixture.Service.SafetyTerminateAsync(
                fixture.DriverId,
                isStaff: false,
                fixture.TripId,
                "Nguy cơ an toàn",
                CancellationToken.None));

        Assert.Equal("trip.safety_termination_invalid_status", exception.Code);
        var trip = await fixture.DbContext.Trips.SingleAsync(x => x.Id == fixture.TripId);
        Assert.Equal(TripStatus.WAITING_RETURN_CONFIRM, trip.TripStatus);
        Assert.Empty(await fixture.DbContext.SafetyTerminationEvidence.ToListAsync());
    }

    [Fact]
    public async Task SafetyTerminate_WithMultipleEvidence_PersistsEveryImage()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.ARRIVED);
        var evidence = new[]
        {
            new StoredSafetyTerminationEvidence(
                "https://storage.test/safety/first.jpg",
                "safety/first",
                "first.jpg",
                "image/jpeg",
                101),
            new StoredSafetyTerminationEvidence(
                "https://storage.test/safety/second.png",
                "safety/second",
                "second.png",
                "image/png",
                202)
        };

        await fixture.Service.SafetyTerminateAsync(
            fixture.DriverId,
            isStaff: false,
            fixture.TripId,
            "Nguy cơ an toàn",
            evidence,
            CancellationToken.None);

        var persisted = await fixture.DbContext.SafetyTerminationEvidence
            .OrderBy(item => item.Id)
            .ToListAsync();
        Assert.Equal(2, persisted.Count);
        Assert.Collection(
            persisted,
            first =>
            {
                Assert.Equal("first.jpg", first.OriginalFileName);
                Assert.Equal(fixture.DriverId, first.UploadedByUserId);
            },
            second =>
            {
                Assert.Equal("second.png", second.OriginalFileName);
                Assert.Equal(fixture.DriverId, second.UploadedByUserId);
            });
    }

    [Fact]
    public async Task SafetyTerminate_AfterTripStarts_FinalizesPartialFareWithoutPromotionOrContribution()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.IN_PROGRESS,
            riskProtectionEnabled: true);
        fixture.Redis.SetTripTrackingSnapshot(CreateTripTrackingSnapshot(fixture.TripId, 5_200));

        await fixture.Service.SafetyTerminateAsync(
            fixture.DriverId,
            isStaff: false,
            fixture.TripId,
            "Khách hàng có hành vi không an toàn",
            CancellationToken.None);

        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
            .SingleAsync(x => x.Id == fixture.TripId);

        Assert.Equal(TripStatus.CANCELLED, trip.TripStatus);
        Assert.Equal(TripTerminationCategory.SAFETY, trip.TerminationCategory);
        Assert.Equal(TripEndReason.SAFETY_TERMINATION, trip.EndReason);
        Assert.Equal(5.2m, trip.ActualDistanceKm);
        Assert.Equal(72_000m, trip.ActualFare);
        Assert.Equal(72_000m, trip.FinalFare);
        Assert.Equal(UtcNow, trip.EndedAt);
        Assert.Empty(trip.Booking.BookingPromotions);
        Assert.Equal(2, fixture.Promotion.CurrentUsageCount);
        var settlement = await fixture.DbContext.TripFinancialSettlements.SingleAsync();
        Assert.Equal(72_000m, settlement.CustomerPayableAmount);
        Assert.Equal(0m, settlement.PromotionExpense);
        Assert.Equal(0m, settlement.RiskContribution);
        Assert.False(settlement.IsRiskContributionEligible);
        Assert.Empty(await fixture.DbContext.RiskFundTransactions.ToListAsync());
    }

    [Fact]
    public async Task SafetyTerminatedTrip_CashPaymentSettlesPartialFareAndKeepsTripCancelled()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.IN_PROGRESS,
            riskProtectionEnabled: true);
        fixture.Redis.SetTripTrackingSnapshot(CreateTripTrackingSnapshot(fixture.TripId, 5_200));
        fixture.DbContext.DriverWallets.Add(new DriverWallet
        {
            DriverId = fixture.DriverId,
            CurrentBalance = 100_000m
        });
        await fixture.DbContext.SaveChangesAsync();
        await fixture.Service.SafetyTerminateAsync(
            fixture.DriverId,
            isStaff: false,
            fixture.TripId,
            "Kết thúc chuyến do rủi ro an toàn",
            CancellationToken.None);

        var result = await fixture.CreatePaymentService().ConfirmCashPaymentAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);

        var trip = await fixture.DbContext.Trips.SingleAsync(x => x.Id == fixture.TripId);
        var settlement = await fixture.DbContext.TripFinancialSettlements.SingleAsync();
        var payment = await fixture.DbContext.Payments.SingleAsync();
        var walletTransaction = await fixture.DbContext.WalletTransactions.SingleAsync();

        Assert.Equal(PaymentStatus.Success, result.PaymentStatus);
        Assert.Equal(TripStatus.CANCELLED, result.TripStatus);
        Assert.Equal(TripStatus.CANCELLED, trip.TripStatus);
        Assert.Equal(72_000m, payment.Amount);
        Assert.Equal(PaymentMethod.CASH, payment.PaymentMethod);
        Assert.Equal(0m, settlement.PromotionExpense);
        Assert.Equal(72_000m, settlement.CustomerPayableAmount);
        Assert.Equal(50_400m, settlement.DriverEarning);
        Assert.Equal(21_600m, settlement.NetPlatformCommission);
        Assert.Equal(0m, settlement.RiskContribution);
        Assert.False(settlement.IsRiskContributionEligible);
        Assert.NotNull(settlement.SettledAtUtc);
        Assert.Equal(78_400m, (await fixture.DbContext.DriverWallets.SingleAsync()).CurrentBalance);
        Assert.Equal(WalletTransactionType.Penalty, walletTransaction.TransactionType);
        Assert.Equal(21_600m, walletTransaction.Amount);
        Assert.Empty(await fixture.DbContext.RiskFundTransactions.ToListAsync());
    }

    [Fact]
    public async Task SafetyTerminatedTrip_QrPaymentCreditsDriverAndKeepsTripCancelled()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.IN_PROGRESS,
            riskProtectionEnabled: true);
        fixture.Redis.SetTripTrackingSnapshot(CreateTripTrackingSnapshot(fixture.TripId, 5_200));
        await fixture.Service.SafetyTerminateAsync(
            fixture.DriverId,
            isStaff: false,
            fixture.TripId,
            "Kết thúc chuyến do rủi ro an toàn",
            CancellationToken.None);

        var result = await fixture.CreatePaymentService().ConfirmDemoQrPaymentAsync(
            new DemoQrPaymentWebhookRequest(
                fixture.TripId,
                OrderCode: null,
                Amount: 72_000m),
            CancellationToken.None);

        var trip = await fixture.DbContext.Trips.SingleAsync(x => x.Id == fixture.TripId);
        var settlement = await fixture.DbContext.TripFinancialSettlements.SingleAsync();

        Assert.Equal(PaymentStatus.Success, result.PaymentStatus);
        Assert.Equal(TripStatus.CANCELLED, result.TripStatus);
        Assert.Equal(TripStatus.CANCELLED, trip.TripStatus);
        Assert.Equal(0m, settlement.PromotionExpense);
        Assert.Equal(50_400m, settlement.DriverEarning);
        Assert.Equal(0m, settlement.RiskContribution);
        Assert.NotNull(settlement.SettledAtUtc);
        Assert.Equal(50_400m, (await fixture.DbContext.DriverWallets.SingleAsync()).CurrentBalance);
        Assert.Equal(
            WalletTransactionType.Income,
            (await fixture.DbContext.WalletTransactions.SingleAsync()).TransactionType);
        Assert.Empty(await fixture.DbContext.RiskFundTransactions.ToListAsync());
    }

    [Theory]
    [InlineData(20_000, 52_000, 0, SafetyPaymentReconciliationStatus.PAYMENT_PENDING, 20_000)]
    [InlineData(72_000, 0, 0, SafetyPaymentReconciliationStatus.PAID, 50_400)]
    [InlineData(90_000, 0, 18_000, SafetyPaymentReconciliationStatus.REFUND_PENDING, 50_400)]
    public async Task SafetyTerminate_ReconcilesSuccessfulPrepaymentAgainstPartialPayable(
        decimal prepaid,
        decimal expectedRemaining,
        decimal expectedRefund,
        SafetyPaymentReconciliationStatus expectedStatus,
        decimal expectedDriverCredit)
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.IN_PROGRESS,
            riskProtectionEnabled: true);
        fixture.Redis.SetTripTrackingSnapshot(CreateTripTrackingSnapshot(fixture.TripId, 5_200));
        await fixture.AddSuccessfulPaymentAsync(prepaid);

        await fixture.Service.SafetyTerminateAsync(
            fixture.DriverId, false, fixture.TripId,
            "Nguy cơ an toàn cần dừng chuyến", CancellationToken.None);

        var trip = await fixture.DbContext.Trips.SingleAsync(x => x.Id == fixture.TripId);
        var reconciliation = await fixture.DbContext.SafetyPaymentReconciliations
            .Include(x => x.Refund)
            .SingleAsync(x => x.TripId == fixture.TripId);
        var settlement = await fixture.DbContext.TripFinancialSettlements.SingleAsync();

        Assert.Equal(TripStatus.CANCELLED, trip.TripStatus);
        Assert.Equal(72_000m, reconciliation.CustomerPayableAmount);
        Assert.Equal(prepaid, reconciliation.SuccessfulPaymentAmount);
        Assert.Equal(expectedRemaining, reconciliation.RemainingPayableAmount);
        Assert.Equal(expectedRefund, reconciliation.RefundObligationAmount);
        Assert.Equal(expectedStatus, reconciliation.Status);
        Assert.Equal(expectedDriverCredit, reconciliation.DriverCreditedAmount);
        Assert.True(reconciliation.DriverCreditedAmount <= settlement.DriverEarning);
        Assert.Equal(expectedRefund > 0, reconciliation.Refund is not null);
        Assert.Empty(await fixture.DbContext.RiskFundTransactions.ToListAsync());
    }

    [Fact]
    public async Task SafetyTerminate_BeforeStart_WithSuccessfulQr_CreatesRefundPendingWithoutSettlement()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.ARRIVED);
        await fixture.AddSuccessfulPaymentAsync(100_000m);

        await fixture.Service.SafetyTerminateAsync(
            fixture.DriverId, false, fixture.TripId,
            "Xe không còn bảo đảm an toàn", CancellationToken.None);

        var reconciliation = await fixture.DbContext.SafetyPaymentReconciliations
            .Include(x => x.Refund)
            .SingleAsync();
        Assert.Equal(SafetyPaymentReconciliationStatus.REFUND_PENDING, reconciliation.Status);
        Assert.Equal(0m, reconciliation.CustomerPayableAmount);
        Assert.Equal(100_000m, reconciliation.RefundObligationAmount);
        Assert.Equal(ManualRefundStatus.REFUND_PENDING, reconciliation.Refund!.Status);
        Assert.Empty(await fixture.DbContext.TripFinancialSettlements.ToListAsync());
        Assert.Empty(await fixture.DbContext.WalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task SafetyTerminate_CancelsPendingQr_AndKeepsFullPartialPayable()
    {
        using var fixture = await TripStatusFixture.CreateAsync(
            TripStatus.IN_PROGRESS,
            pricingSnapshotV1: true);
        fixture.Redis.SetTripTrackingSnapshot(
            CreateTripTrackingSnapshot(fixture.TripId, 5_200));
        fixture.DbContext.Payments.Add(new Payment
        {
            TripId = fixture.TripId,
            PaymentMethod = PaymentMethod.QR,
            TransactionReference = $"{fixture.TripId}999",
            Amount = 100_000m,
            PaymentStatus = PaymentStatus.Pending,
            CreatedAt = UtcNow
        });
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Service.SafetyTerminateAsync(
            fixture.DriverId, false, fixture.TripId,
            "Dừng chuyến vì an toàn", CancellationToken.None);

        Assert.Equal(
            PaymentStatus.Cancelled,
            (await fixture.DbContext.Payments.SingleAsync()).PaymentStatus);
        var reconciliation = await fixture.DbContext.SafetyPaymentReconciliations.SingleAsync();
        Assert.Equal(72_000m, reconciliation.RemainingPayableAmount);
        Assert.Equal(SafetyPaymentReconciliationStatus.PAYMENT_PENDING, reconciliation.Status);
    }

    [Fact]
    public async Task ManualRefund_RequiresEvidence_AndSameCallbackIsIdempotent()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.ARRIVED);
        await fixture.AddSuccessfulPaymentAsync(100_000m);
        await fixture.Service.SafetyTerminateAsync(
            fixture.DriverId, false, fixture.TripId,
            "Dừng chuyến vì an toàn", CancellationToken.None);
        var refund = await fixture.DbContext.ManualPaymentRefunds.SingleAsync();
        var service = new SafetyPaymentReconciliationService(
            fixture.DbContext,
            fixture.FinancialSettlementService,
            new DateTimeProviderFake(UtcNow));
        var request = new ManualRefundConfirmationRequest(
            "STAFF-REFUND-001",
            "https://evidence.test/refund-001.pdf",
            "refund-callback-001",
            Convert.ToBase64String(refund.RowVersion));

        var first = await service.ConfirmManualRefundAsync(
            Guid.NewGuid(), refund.Id, request, CancellationToken.None);
        var replay = await service.ConfirmManualRefundAsync(
            Guid.NewGuid(), refund.Id, request, CancellationToken.None);

        Assert.Equal(SafetyPaymentReconciliationStatus.REFUNDED, first.Status);
        Assert.Equal(ManualRefundStatus.REFUNDED, first.RefundStatus);
        Assert.Equal(first, replay);
        var stored = await fixture.DbContext.ManualPaymentRefunds.SingleAsync();
        Assert.NotNull(stored.RefundedByUserId);
        Assert.Equal("STAFF-REFUND-001", stored.PaymentReference);
        Assert.Equal("https://evidence.test/refund-001.pdf", stored.EvidenceUrl);
    }

    [Fact]
    public async Task CancelTrip_RemovesPromotionWithoutIncrementingUsageAndReleasesDriver()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.ACCEPTED);

        await fixture.Service.UpdateDriverTripStatusAsync(
            fixture.DriverId,
            fixture.TripId,
            TripStatus.CANCELLED,
            CancellationToken.None);

        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
            .SingleAsync(x => x.Id == fixture.TripId);
        var driver = await fixture.DbContext.DriverProfiles
            .SingleAsync(x => x.DriverId == fixture.DriverId);

        Assert.Equal(TripStatus.CANCELLED, trip.TripStatus);
        Assert.Equal(BookingStatus.Cancelled, trip.Booking.BookingStatus);
        Assert.Equal(fixture.DriverId, trip.CancelledByUserId);
        Assert.Equal(2, fixture.Promotion.CurrentUsageCount);
        Assert.Empty(trip.Booking.BookingPromotions);
        Assert.Equal(DriverWorkStatus.Online, driver.WorkStatus);
        Assert.Equal(DriverWorkStatus.Online.ToString(), fixture.Redis.DriverStatusValue);
        Assert.Contains(fixture.TripLiveKey, fixture.Redis.RemovedKeys);
        Assert.Contains(fixture.DriverActiveTripKey, fixture.Redis.RemovedKeys);
    }

    [Fact]
    public async Task EndTrip_WhenTripNotInProgress_RejectsInvalidTransition()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.ACCEPTED);
        var exception = await Assert.ThrowsAsync<BookingException>(
            () => fixture.Service.EndTripAsync(
                fixture.DriverId,
                fixture.TripId,
                CancellationToken.None));

        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
                    .ThenInclude(x => x.Promotion)
            .SingleAsync(x => x.Id == fixture.TripId);

        Assert.Equal("trip.invalid_status_transition", exception.Code);
        Assert.Equal(TripStatus.ACCEPTED, trip.TripStatus);
        Assert.Equal(BookingStatus.DriverAssigned, trip.Booking.BookingStatus);
        Assert.Equal(2, fixture.Promotion.CurrentUsageCount);
        Assert.Empty(fixture.Realtime.TripStatusNotifications);
        Assert.Empty(fixture.Redis.RemovedKeys);
    }

    private static PreTripVehicleCheckRequest PassedPreTripCheck() => new(
        true,
        true,
        true,
        true,
        true,
        true,
        true,
        null,
        "Đã kiểm tra đầy đủ",
        null);

    private static PreTripVehicleCheckRequest FailedPreTripCheck() => new(
        false,
        true,
        true,
        true,
        true,
        true,
        true,
        VehicleFaultType.BRAKE_FAILURE,
        "Phanh không đạt",
        null);

    private static TripTrackingSnapshot CreateTripTrackingSnapshot(
        long tripId,
        double distanceMeters)
    {
        var firstPoint = new TripTrackingPoint(
            tripId,
            10.762622,
            106.660172,
            new DateTimeOffset(UtcNow.AddMinutes(-5)).ToUnixTimeMilliseconds(),
            new DateTimeOffset(UtcNow.AddMinutes(-5)).ToUnixTimeMilliseconds(),
            UtcNow.AddMinutes(-5));
        var lastPoint = new TripTrackingPoint(
            tripId,
            10.818797,
            106.651856,
            new DateTimeOffset(UtcNow).ToUnixTimeMilliseconds(),
            new DateTimeOffset(UtcNow).ToUnixTimeMilliseconds(),
            UtcNow);

        return new TripTrackingSnapshot(
            [firstPoint, lastPoint],
            distanceMeters,
            firstPoint,
            lastPoint,
            UtcNow.AddMinutes(-5),
            UtcNow);
    }

    private static TripTrackingSnapshot CreateV1RouteSnapshot(
        long tripId,
        double lastLatitude,
        double lastLongitude,
        double distanceMeters)
    {
        var firstPoint = new TripTrackingPoint(
            tripId,
            38.5,
            -120.2,
            new DateTimeOffset(UtcNow.AddMinutes(-5)).ToUnixTimeMilliseconds(),
            new DateTimeOffset(UtcNow.AddMinutes(-5)).ToUnixTimeMilliseconds(),
            UtcNow.AddMinutes(-5));
        var lastPoint = new TripTrackingPoint(
            tripId,
            lastLatitude,
            lastLongitude,
            new DateTimeOffset(UtcNow).ToUnixTimeMilliseconds(),
            new DateTimeOffset(UtcNow).ToUnixTimeMilliseconds(),
            UtcNow);
        return new TripTrackingSnapshot(
            [firstPoint, lastPoint],
            distanceMeters,
            firstPoint,
            lastPoint,
            UtcNow.AddMinutes(-5),
            UtcNow);
    }

    private sealed class TripStatusFixture : IDisposable
    {
        private TripStatusFixture(
            ApplicationDbContext dbContext,
            TrackingRedisService redis,
            RealtimeNotificationServiceFake realtime,
            TripStatusService service,
            ITripFinancialSettlementService financialSettlementService,
            IRiskProtectionPolicyProvider riskProtectionPolicyProvider,
            ITripCommissionCalculator commissionCalculator,
            Guid customerId,
            Guid driverId,
            long tripId,
            Promotion promotion)
        {
            DbContext = dbContext;
            Redis = redis;
            Realtime = realtime;
            Service = service;
            FinancialSettlementService = financialSettlementService;
            RiskProtectionPolicyProvider = riskProtectionPolicyProvider;
            CommissionCalculator = commissionCalculator;
            CustomerId = customerId;
            DriverId = driverId;
            TripId = tripId;
            Promotion = promotion;
        }

        public ApplicationDbContext DbContext { get; }
        public TrackingRedisService Redis { get; }
        public RealtimeNotificationServiceFake Realtime { get; }
        public TripStatusService Service { get; }
        public ITripFinancialSettlementService FinancialSettlementService { get; }
        public IRiskProtectionPolicyProvider RiskProtectionPolicyProvider { get; }
        public ITripCommissionCalculator CommissionCalculator { get; }
        public Guid CustomerId { get; }
        public Guid DriverId { get; }
        public long TripId { get; }
        public Promotion Promotion { get; }
        public string TripLiveKey => RedisKeys.TripLive(TripId);
        public string DriverActiveTripKey => RedisKeys.DriverActiveTrip(DriverId);

        public PayOsPaymentService CreatePaymentService()
        {
            return new PayOsPaymentService(
                new HttpClient(),
                DbContext,
                Service,
                Realtime,
                new TripPaymentSettlementService(FinancialSettlementService),
                FinancialSettlementService,
                RiskProtectionPolicyProvider,
                CommissionCalculator,
                Options.Create(new PayOsOptions()));
        }

        public async Task AddSuccessfulPaymentAsync(decimal amount)
        {
            DbContext.Payments.Add(new Payment
            {
                TripId = TripId,
                PaymentMethod = PaymentMethod.QR,
                TransactionReference = $"{TripId}123",
                Amount = amount,
                Currency = "VND",
                PaymentStatus = PaymentStatus.Success,
                PaidAt = UtcNow,
                CreatedAt = UtcNow
            });
            await DbContext.SaveChangesAsync();
        }

        public static async Task<TripStatusFixture> CreateAsync(
            TripStatus initialTripStatus,
            bool riskProtectionEnabled = false,
            bool simulateConcurrentStartWinner = false,
            IEvidenceFileValidator? evidenceFileValidator = null,
            ITripReturnEvidenceStorage? returnEvidenceStorage = null,
            bool pricingSnapshotV1 = false,
            bool isHourlyBooking = false,
            decimal estimatedFare = 72_000m,
            decimal longDistanceComponent = 0m)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"trip-status-{Guid.NewGuid():N}")
                .Options;
            var dbContext = new ApplicationDbContext(options);

            var customerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            var booking = SeedTripGraph(
                dbContext,
                customerId,
                driverId,
                initialTripStatus,
                riskProtectionEnabled,
                pricingSnapshotV1,
                isHourlyBooking,
                estimatedFare,
                longDistanceComponent);
            await dbContext.SaveChangesAsync();

            var redis = new TrackingRedisService();
            var realtime = new RealtimeNotificationServiceFake();
            var commissionCalculator = new TripCommissionCalculator();
            var policyProvider = new RiskProtectionPolicyProvider(dbContext);
            var riskFundLedger = new RiskFundLedgerService(dbContext);
            var financialSettlementService = new TripFinancialSettlementService(
                dbContext,
                commissionCalculator,
                policyProvider,
                riskFundLedger);
            var tripPaymentSettlementService = new TripPaymentSettlementService(financialSettlementService);
            IPreTripVehicleCheckService preTripVehicleCheckService;
            if (simulateConcurrentStartWinner)
            {
                var check = new PreTripVehicleCheck
                {
                    TripId = booking.Trip!.Id,
                    DriverId = driverId,
                    BrakeResponsePassed = true,
                    FrontRearLightsPassed = true,
                    TurnSignalsPassed = true,
                    VisibleTiresPassed = true,
                    DashboardWarningPassed = true,
                    WindshieldVisibilityPassed = true,
                    NoMajorVisibleIssue = true,
                    Result = PreTripCheckResult.PASS,
                    CheckedAtUtc = UtcNow
                };
                dbContext.PreTripVehicleChecks.Add(check);
                await dbContext.SaveChangesAsync();
                var policyId = await dbContext.RiskProtectionPolicyVersions
                    .Select(x => x.Id)
                    .SingleAsync();
                preTripVehicleCheckService = new ConcurrentStartWinningPreTripService(
                    dbContext,
                    policyId,
                    check.Id);
            }
            else
            {
                preTripVehicleCheckService = new PreTripVehicleCheckService(
                    dbContext,
                    policyProvider,
                    new DateTimeProviderFake(UtcNow));
            }
            var service = new TripStatusService(
                dbContext,
                new DateTimeProviderFake(UtcNow),
                redis,
                realtime,
                returnEvidenceStorage ?? new NoOpTripReturnEvidenceStorage(),
                evidenceFileValidator ?? TestEvidenceValidation.Create(),
                new TripSharingServiceFake(),
                new OptionsMonitorFake<TripTrackingOptions>(new TripTrackingOptions()),
                new NoOpMapRoutingService(),
                new TripFareFinalizationService(
                    new FareEstimationService(),
                    Options.Create(new DriverCompensationOptions
                    {
                        DestinationReachedThresholdMeters = 250
                    })),
                tripPaymentSettlementService,
                preTripVehicleCheckService,
                financialSettlementService,
                new AccountBanEvaluationServiceFake(),
                NullLogger<TripStatusService>.Instance);

            return new TripStatusFixture(
                dbContext,
                redis,
                realtime,
                service,
                financialSettlementService,
                policyProvider,
                commissionCalculator,
                customerId,
                driverId,
                booking.Trip!.Id,
                booking.BookingPromotions.Single().Promotion);
        }

        public void Dispose()
        {
            DbContext.Dispose();
        }

        private static Booking SeedTripGraph(
            ApplicationDbContext dbContext,
            Guid customerId,
            Guid driverId,
            TripStatus initialTripStatus,
            bool riskProtectionEnabled,
            bool pricingSnapshotV1,
            bool isHourlyBooking,
            decimal estimatedFare,
            decimal longDistanceComponent)
        {
            var customer = new AspNetUser
            {
                Id = customerId,
                UserName = "customer@example.test",
                Email = "customer@example.test",
                FullName = "Customer",
                IsActive = true,
                CreatedAt = UtcNow
            };
            var driverUser = new AspNetUser
            {
                Id = driverId,
                UserName = "driver@example.test",
                Email = "driver@example.test",
                FullName = "Driver",
                IsActive = true,
                CreatedAt = UtcNow
            };
            var serviceType = new ServiceType
            {
                ServiceName = "Ride"
            };
            var pricingRule = new PricingRule
            {
                ServiceType = serviceType,
                VehicleClass = RequiredLicenseClass.A1,
                BaseFare = 20_000m,
                MinFare = 30_000m,
                PricePerKm = isHourlyBooking ? null : 10_000m,
                PricePerHour = isHourlyBooking ? 52_000m : null,
                IsActive = true,
                CreatedAt = UtcNow
            };
            var vehicle = new Vehicle
            {
                OwnerUserId = customerId,
                OwnerUser = customer,
                PlateNumber = "29A1-12345",
                BrandModel = "Honda Vision",
                RequiredLicenseClass = RequiredLicenseClass.A1,
                VehicleType = VehicleType.Motorbike,
                EngineType = EngineType.ICE,
                TransmissionType = TransmissionType.None,
                EngineCapacityCc = 110,
                CreatedAt = UtcNow
            };
            var promotion = new Promotion
            {
                PromotionCode = "SAFE10",
                DiscountType = DiscountType.Fixed,
                DiscountValue = 10_000m,
                StartDate = UtcNow.AddDays(-1),
                EndDate = UtcNow.AddDays(1),
                MaxUsageCount = 100,
                CurrentUsageCount = 2,
                MinimumOrderValue = 0,
                MaximumDiscountValue = 10_000m,
                UsageLimitPerUser = 1,
                IsActive = true
            };
            var booking = new Booking
            {
                CustomerId = customerId,
                Customer = customer,
                Vehicle = vehicle,
                ServiceType = serviceType,
                BookingType = BookingType.Now,
                BookingStatus = BookingStatus.DriverAssigned,
                PricingRule = pricingRule,
                PickupAddress = "Pickup",
                PickupLocation = new Point(106.660172, 10.762622) { SRID = 4326 },
                DestinationAddress = isHourlyBooking ? null : "Destination",
                DestinationLocation = isHourlyBooking
                    ? null
                    : new Point(106.651856, 10.818797) { SRID = 4326 },
                EstimatedDistanceKm = isHourlyBooking ? 0m : 5.2m,
                EstimatedDurationMinutes = isHourlyBooking ? 60 : 30,
                EstimatedFare = estimatedFare,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            };
            if (pricingSnapshotV1)
            {
                booking.PickupLocation = new Point(-120.2, 38.5) { SRID = 4326 };
                booking.DestinationAddress = isHourlyBooking ? null : "Destination";
                booking.DestinationLocation = isHourlyBooking
                    ? null
                    : new Point(-126.453, 43.252) { SRID = 4326 };
                booking.RoutePolyline = isHourlyBooking
                    ? null
                    : "_p~iF~ps|U_ulLnnqC_mqNvxq`@";
                booking.PricingSnapshotVersion = Booking.CurrentPricingSnapshotVersion;
                booking.AcceptedBaseFare = 20_000m;
                booking.AcceptedMinimumServiceFare = 30_000m;
                booking.AcceptedPricePerKm = isHourlyBooking ? null : 10_000m;
                booking.AcceptedPricePerHour = isHourlyBooking ? 52_000m : null;
                booking.AcceptedSurgeMultiplier = 1m;
                booking.SurgeEvaluationTime = UtcNow;
                booking.NormalFare = estimatedFare - longDistanceComponent;
                booking.SurgedFare = estimatedFare - longDistanceComponent;
                booking.SurgeAmount = 0m;
                booking.AcceptedLongDistanceThresholdKm = 15m;
                booking.AcceptedLongDistanceOptInThresholdKm = 15m;
                booking.AcceptedMaximumTripDistanceKm = 50m;
                booking.AcceptedLongDistanceRatePerKm = 3_000m;
                booking.LongDistanceComponent = longDistanceComponent;
            }
            booking.BookingPromotions.Add(new BookingPromotion
            {
                Booking = booking,
                Promotion = promotion,
                DiscountAmount = 10_000m,
                CreatedAt = UtcNow
            });
            booking.Trip = new Trip
            {
                Booking = booking,
                DriverId = driverId,
                TripStatus = initialTripStatus,
                DriverAssignedAt = UtcNow.AddMinutes(-10),
                StartedAt = initialTripStatus == TripStatus.IN_PROGRESS
                    ? UtcNow.AddMinutes(-5)
                    : null,
                CreatedAt = UtcNow.AddMinutes(-10)
            };
            var driver = new DriverProfile
            {
                DriverId = driverId,
                Driver = driverUser,
                IdentityCardNumber = "123456789",
                WorkStatus = DriverWorkStatus.Busy,
                LastActiveAt = UtcNow.AddMinutes(-1),
                CreatedAt = UtcNow.AddDays(-1)
            };

            dbContext.AspNetUsers.AddRange(customer, driverUser);
            dbContext.DriverProfiles.Add(driver);
            dbContext.Bookings.Add(booking);
            dbContext.RiskProtectionPolicyVersions.Add(new RiskProtectionPolicyVersion
            {
                EffectiveFromUtc = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                BasePlatformCommissionRate = 0.30m,
                RiskReserveRate = riskProtectionEnabled ? 0.10m : 0m,
                DefaultProtectionLimit = riskProtectionEnabled ? 20_000_000m : 0m,
                DriverOrdinaryNegligenceRate = 0m,
                DriverOrdinaryNegligenceCap = 0m,
                DriverGrossNegligenceRate = 0m,
                DriverGrossNegligenceCap = 0m,
                MockInsuranceCoverageLimit = 0m,
                ClaimAutoApprovalThreshold = 0m,
                RiskFundEnabled = riskProtectionEnabled,
                ChangeReason = riskProtectionEnabled
                    ? "Integration test risk protection rollout"
                    : "Integration test legacy policy",
                CreatedAtUtc = UtcNow
            });

            return booking;
        }
    }

    private sealed class ConcurrentStartWinningPreTripService(
        ApplicationDbContext dbContext,
        long policyVersionId,
        long checkId) : IPreTripVehicleCheckService
    {
        public Task EnsureCanCreateAsync(
            Guid driverId,
            long tripId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PreTripVehicleCheckResponse> CreateAsync(
            Guid driverId,
            long tripId,
            PreTripVehicleCheckRequest request,
            StoredPreTripVehicleCheckEvidence? evidence,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<PreTripVehicleCheckResponse>> GetAsync(
            Guid userId,
            bool isManagement,
            long tripId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public async Task EnsureCanStartAndActivateCoverageAsync(
            Guid driverId,
            Trip trip,
            DateTime startedAtUtc,
            CancellationToken cancellationToken)
        {
            trip.TripStatus = TripStatus.IN_PROGRESS;
            trip.StartedAt = startedAtUtc;
            dbContext.TripProtectionCoverages.Add(new TripProtectionCoverage
            {
                TripId = trip.Id,
                PolicyVersionId = policyVersionId,
                PreTripVehicleCheckId = checkId,
                ProtectionLimit = 20_000_000m,
                ActivatedAtUtc = startedAtUtc
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new DbUpdateException(
                "Violation of UNIQUE KEY constraint 'IX_TripProtectionCoverages_TripId'.");
        }
    }

    private sealed class DateTimeProviderFake : IDateTimeProvider
    {
        public DateTimeProviderFake(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }

    private sealed class AccountBanEvaluationServiceFake : IAccountBanEvaluationService
    {
        public List<long> EvaluatedRatingIds { get; } = [];

        public Task EvaluateRatingAsync(
            long ratingId,
            CancellationToken cancellationToken)
        {
            EvaluatedRatingIds.Add(ratingId);
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingRedisService : IRedisService
    {
        private TripTrackingSnapshot _tripTrackingSnapshot = new([], 0, null, null, null, null);
        private readonly Dictionary<string, string> _values = [];

        public List<string> RemovedKeys { get; } = [];
        public List<string> SetKeys { get; } = [];
        public List<(string Key, string Member)> GeoRemovedMembers { get; } = [];
        public List<TripTrackingPoint> RecordedTrackingPoints { get; } = [];
        public string? DriverStatusValue { get; private set; }

        public void SetTripTrackingSnapshot(TripTrackingSnapshot snapshot)
        {
            _tripTrackingSnapshot = snapshot;
        }

        public void SetDriverLocation(Guid driverId, DriverLocationCache location)
        {
            _values[RedisKeys.DriverLocation(driverId)] =
                System.Text.Json.JsonSerializer.Serialize(location);
        }

        public Task SetAsync(
            string key,
            string value,
            TimeSpan expiration)
        {
            _values[key] = value;
            SetKeys.Add(key);
            if (key.StartsWith("sr:driver:status:", StringComparison.Ordinal))
            {
                DriverStatusValue = value;
            }

            return Task.CompletedTask;
        }

        public Task<bool> SetIfNotExistsAsync(
            string key,
            string value,
            TimeSpan expiration) =>
            Task.FromResult(true);

        public Task<bool> TryAcquireDistributedLockAsync(
            string key,
            string value,
            TimeSpan expiration) =>
            Task.FromResult(true);

        public Task<string?> GetAsync(string key) =>
            Task.FromResult(_values.GetValueOrDefault(key));

        public Task<IReadOnlyDictionary<string, string?>> GetManyAsync(
            IReadOnlyCollection<string> keys) =>
            Task.FromResult<IReadOnlyDictionary<string, string?>>(
                keys
                    .Distinct(StringComparer.Ordinal)
                    .ToDictionary(key => key, _ => (string?)null));

        public Task RemoveAsync(string key)
        {
            _values.Remove(key);
            RemovedKeys.Add(key);
            if (key.StartsWith("sr:driver:status:", StringComparison.Ordinal))
            {
                DriverStatusValue = null;
            }
            return Task.CompletedTask;
        }

        public Task ExpireAsync(
            string key,
            TimeSpan expiration,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ListRightPushTrimAndExpireAsync(
            string key,
            string value,
            int maxLength,
            TimeSpan expiration,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<string>> ListRangeAsync(
            string key,
            long start = 0,
            long stop = -1,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<long> IncrementAsync(
            string key,
            TimeSpan expiration) =>
            Task.FromResult(1L);

        public Task GeoAddAsync(
            string key,
            double longitude,
            double latitude,
            string member) =>
            Task.CompletedTask;

        public Task GeoRemoveAsync(
            string key,
            string member,
            CancellationToken cancellationToken = default)
        {
            GeoRemovedMembers.Add((key, member));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> GeoRadiusAsync(
            string key,
            double longitude,
            double latitude,
            double radiusKm,
            int count) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<OtpVerificationResult> VerifyAndConsumeOtpAsync(
            string otpKey,
            string attemptsKey,
            string expectedHash,
            int maxAttempts) =>
            Task.FromResult(OtpVerificationResult.Missing);

        public Task<TripTrackingUpdateResult> RecordTripTrackingPointAsync(
            TripTrackingPoint point,
            TripTrackingWriteOptions options,
            CancellationToken cancellationToken = default)
        {
            RecordedTrackingPoints.Add(point);
            return Task.FromResult(
                new TripTrackingUpdateResult(true, true, 0, 0, "accepted"));
        }

        public Task<TripTrackingSnapshot> GetTripTrackingSnapshotAsync(
            long tripId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_tripTrackingSnapshot);

        public Task RemoveTripTrackingAsync(
            long tripId,
            CancellationToken cancellationToken = default)
        {
            RemovedKeys.AddRange(RedisKeys.TripTrackingKeys(tripId));
            return Task.CompletedTask;
        }
    }

    private sealed class RealtimeNotificationServiceFake
        : IRealtimeNotificationService
    {
        public TripStatus? FailOnceForTripStatus { get; set; }
        public List<TripStatusChangedEvent> TripStatusNotifications { get; } = [];
        public List<TripPaymentPendingEvent> TripPaymentPendingNotifications { get; } = [];
        public List<TripPaymentSucceededEvent> TripPaymentSucceededNotifications { get; } = [];
        public List<BookingStatusChangedEvent> BookingStatusNotifications { get; } = [];

        public Task PublishBookingStatusChangedAsync(
            BookingStatusChangedEvent notification,
            CancellationToken cancellationToken = default)
        {
            BookingStatusNotifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task PublishTripStatusChangedAsync(
            TripStatusChangedEvent notification,
            CancellationToken cancellationToken = default)
        {
            TripStatusNotifications.Add(notification);
            if (FailOnceForTripStatus == notification.TripStatus)
            {
                FailOnceForTripStatus = null;
                throw new InvalidOperationException("Simulated realtime failure after persistence.");
            }
            return Task.CompletedTask;
        }

        public Task PublishTripPaymentPendingAsync(
            TripPaymentPendingEvent notification,
            CancellationToken cancellationToken = default)
        {
            TripPaymentPendingNotifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task PublishTripPaymentSucceededAsync(
            TripPaymentSucceededEvent notification,
            CancellationToken cancellationToken = default)
        {
            TripPaymentSucceededNotifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task PublishSOSTriggeredAsync(
            SOSTriggeredEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishBookingSearchingStartedAsync(
            BookingSearchingStartedEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishTripCreatedAsync(
            TripCreatedEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishBookingDriverAssignedAsync(
            BookingDriverAssignedEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishDriverLocationUpdatedAsync(
            DriverLocationUpdatedEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishDriverOfferCreatedAsync(
            DriverOfferCreatedEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishDriverOfferReceivedAsync(
            DriverOfferReceivedEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishDriverOfferRejectedAsync(
            DriverOfferRejectedEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishDriverOfferAcceptedAsync(
            DriverOfferAcceptedEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishDriverOfferExpiredAsync(
            DriverOfferExpiredEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishDriverOfferCancelledAsync(
            DriverOfferCancelledEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishDriverMatchedAsync(
            DriverMatchedEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishCustomerConfirmedDriverOfferAsync(
            CustomerConfirmedDriverOfferEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishBookingSearchRadiusExpandedAsync(
            BookingSearchRadiusExpandedEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishBookingExpiredAsync(
            BookingExpiredEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class OptionsMonitorFake<TOptions>(TOptions currentValue) : IOptionsMonitor<TOptions>
    {
        public TOptions CurrentValue { get; } = currentValue;
        public TOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
    }
    private sealed class NoOpTripReturnEvidenceStorage : ITripReturnEvidenceStorage
    {
        public Task<StoredReturnEvidenceFile> SaveAsync(
            long tripId,
            int displayOrder,
            string originalFileName,
            string contentType,
            Stream content,
            CancellationToken cancellationToken)
        {
            // Test stub: returns a fake URL; no real upload is performed.
            return Task.FromResult(new StoredReturnEvidenceFile(
                $"https://fake.cloudinary.com/trip-{tripId}/photo-{displayOrder}.jpg",
                $"fake-public-id-{displayOrder}",
                originalFileName,
                contentType,
                0L));
        }
    }

    private sealed class TrackingTripReturnEvidenceStorage : ITripReturnEvidenceStorage
    {
        public int SaveCalls { get; private set; }
        public int? FailOnSaveCall { get; init; }
        public List<string> DeletedPublicIds { get; } = [];

        public Task<StoredReturnEvidenceFile> SaveAsync(
            long tripId,
            int displayOrder,
            string originalFileName,
            string contentType,
            Stream content,
            CancellationToken cancellationToken)
        {
            SaveCalls++;
            if (SaveCalls == FailOnSaveCall)
                throw new InvalidOperationException("Simulated storage failure.");
            return Task.FromResult(new StoredReturnEvidenceFile(
                $"https://storage.test/return-{displayOrder}.jpg",
                $"return-{displayOrder}",
                originalFileName,
                contentType,
                content.Length));
        }

        public Task DeleteAsync(string publicId, CancellationToken cancellationToken)
        {
            DeletedPublicIds.Add(publicId);
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpMapRoutingService : IMapRoutingService
    {
        public Task<RouteEstimateResult> GetRouteEstimateAsync(
            RouteEstimateRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new RouteEstimateResult
            {
                Provider = MapProvider.Auto,
                DistanceMeters = 0,
                DurationSeconds = 0
            });
        }
    }
}
