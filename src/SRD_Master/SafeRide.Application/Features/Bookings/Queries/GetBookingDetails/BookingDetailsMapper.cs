using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Features.Bookings.DTOs;
using SafeRide.Application.Features.Trips.DTOs;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Bookings.Queries.GetBookingDetails;

internal static class BookingDetailsMapper
{
    public static async Task<BookingDetailsDto> ToDtoAsync(
        Booking booking,
        IBookingRepository repository,
        IMapRoutingService mapRoutingService,
        IMatchingPolicyProvider matchingPolicyProvider,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var driverOffer = await repository.GetLatestBookingDriverOfferAsync(
            booking.BookingId,
            cancellationToken);
        var arrivalPolyline = await GetArrivalPolylineAsync(
            booking,
            driverOffer,
            repository,
            mapRoutingService,
            cancellationToken);
        var price = BookingPriceMapper.FromBooking(booking);
        var safetyTerminated = booking.Trip is
        {
            TripStatus: TripStatus.CANCELLED,
            TerminationCategory: TripTerminationCategory.SAFETY
        };
        var originalFare = safetyTerminated
            ? booking.Trip!.ActualFare ?? 0m
            : price.OriginalFare;
        var promotionCode = safetyTerminated ? null : price.PromotionCode;
        var discountAmount = safetyTerminated ? 0m : price.DiscountAmount;
        var finalFare = safetyTerminated
            ? booking.Trip!.FinalFare ?? booking.Trip.ActualFare ?? 0m
            : price.FinalFare;
        var matchingSnapshot = matchingPolicyProvider.GetSnapshot(booking, utcNow);
        var matchingMessage = driverOffer?.OfferStatus == DriverOfferStatus.DriverAccepted
            ? "Tài xế phù hợp đã sẵn sàng."
            : matchingSnapshot.MatchingMessage;

        return new BookingDetailsDto(
            booking.BookingId,
            booking.BookingType,
            booking.BookingStatus,
            booking.ScheduledAt,
            (double)(booking.EstimatedDistanceKm ?? 0m),
            booking.EstimatedDurationMinutes ?? 0,
            booking.EstimatedFare,
            originalFare,
            promotionCode,
            discountAmount,
            finalFare,
            booking.RoutePolyline,
            arrivalPolyline,
            "Loaded booking details successfully.",
            driverOffer,
            new BookingLocationDto(
                booking.PickupAddress,
                booking.PickupLocation.Y,
                booking.PickupLocation.X),
            booking.DestinationLocation is null
                ? null
                : new BookingLocationDto(
                    booking.DestinationAddress ?? string.Empty,
                    booking.DestinationLocation.Y,
                    booking.DestinationLocation.X),
            new BookingVehicleSummaryDto(
                booking.Vehicle.Id,
                booking.Vehicle.BrandModel,
                booking.Vehicle.PlateNumber,
                booking.Vehicle.Color ?? string.Empty,
                booking.Vehicle.VehicleType == VehicleType.Motorbike),
            booking.Trip?.Id,
            booking.Trip?.TripStatus,
            MapReturnConfirmation(booking.Trip),
            matchingSnapshot.CurrentSearchRadiusKm,
            matchingSnapshot.ExpiresAt,
            matchingSnapshot.EstimatedRemainingSeconds,
            matchingMessage,
            MapPayment(booking.Trip, finalFare),
            ActualDistanceKm: booking.Trip?.ActualDistanceKm is null
                ? null
                : (double)booking.Trip.ActualDistanceKm.Value,
            ActualDurationMinutes: booking.Trip?.ActualDurationMinutes,
            ActualEncodedPolyline: booking.Trip?.RoutePolyline,
            TripEndedAt: booking.Trip?.EndedAt,
            TerminationCategory: booking.Trip?.TerminationCategory,
            SafetyTerminationReason: booking.Trip?.SafetyTerminationReason,
            SafetyTerminatedAt: booking.Trip?.SafetyTerminatedAt);
    }

    private static TripPaymentSummaryDto? MapPayment(Trip? trip, decimal finalFare)
    {
        if (trip is null
            || trip.TripStatus is TripStatus.CANCELLED
                && trip.TerminationCategory != TripTerminationCategory.SAFETY)
        {
            return null;
        }

        var reconciliation = trip.SafetyPaymentReconciliation;
        var requiresAdditionalPayment = reconciliation?.RemainingPayableAmount > 0m;
        var payment = trip.Payments
            .OrderByDescending(x => requiresAdditionalPayment
                ? x.PaymentStatus == PaymentStatus.Pending
                : x.PaymentStatus == PaymentStatus.Success)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefault();
        var zeroFarePaymentCompleted = trip.FinalFare is <= 0m
            && trip.TripStatus is TripStatus.WAITING_RETURN_CONFIRM
                or TripStatus.RETURN_CONFIRMED
                or TripStatus.COMPLETED;
        var status = requiresAdditionalPayment
            ? PaymentStatus.Pending
            : payment?.PaymentStatus
                ?? (zeroFarePaymentCompleted ? PaymentStatus.Success : PaymentStatus.Pending);
        var amount = requiresAdditionalPayment
            ? reconciliation!.RemainingPayableAmount
            : payment?.Amount ?? finalFare;

        return new TripPaymentSummaryDto(
            payment?.Id,
            payment?.PaymentMethod,
            status,
            amount,
            payment?.Currency ?? "VND",
            payment?.PaidAt,
            zeroFarePaymentCompleted
                ? "Chuyáº¿n Ä‘i khÃ´ng phÃ¡t sinh phÃ­ thanh toÃ¡n."
                : BuildPaymentMessage(trip.TripStatus, status, reconciliation?.Status),
            reconciliation?.SuccessfulPaymentAmount ?? 0m,
            reconciliation?.RemainingPayableAmount ?? 0m,
            reconciliation?.RefundObligationAmount ?? 0m,
            reconciliation?.Status,
            reconciliation?.Refund?.Status);
    }

    private static string BuildPaymentMessage(
        TripStatus tripStatus,
        PaymentStatus paymentStatus,
        SafetyPaymentReconciliationStatus? reconciliationStatus = null)
    {
        if (reconciliationStatus == SafetyPaymentReconciliationStatus.REFUND_PENDING)
            return "Khoản hoàn tiền đang chờ Nhân viên xử lý và cung cấp bằng chứng.";
        if (reconciliationStatus == SafetyPaymentReconciliationStatus.REFUNDED)
            return "Khoản hoàn tiền đã được xác nhận bằng chứng.";
        if (paymentStatus == PaymentStatus.Success || tripStatus == TripStatus.COMPLETED)
        {
            return "Thanh toán đã hoàn tất.";
        }

        if (tripStatus is TripStatus.ACCEPTED
            or TripStatus.DRIVER_ARRIVING
            or TripStatus.ARRIVED)
        {
            return "Bạn có thể thanh toán trước bằng PayOS hoặc thanh toán sau chuyến đi.";
        }

        return "Vui lòng thanh toán cho tài xế để hoàn tất chuyến đi.";
    }

    private static TripReturnConfirmationSummaryDto? MapReturnConfirmation(Trip? trip)
    {
        var confirmation = trip?.ReturnConfirmations
            .OrderByDescending(returnConfirmation => returnConfirmation.ConfirmedAt)
            .ThenByDescending(returnConfirmation => returnConfirmation.Id)
            .FirstOrDefault();
        if (confirmation is null)
        {
            return null;
        }

        return new TripReturnConfirmationSummaryDto(
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
                .ToList());
    }

    private static async Task<string?> GetArrivalPolylineAsync(
        Booking booking,
        BookingDriverOfferDto? driverOffer,
        IBookingRepository repository,
        IMapRoutingService mapRoutingService,
        CancellationToken cancellationToken)
    {
        if (booking.BookingStatus != BookingStatus.DriverAssigned
            || booking.Trip is null
            || driverOffer is null
            || booking.Trip.TripStatus is TripStatus.IN_PROGRESS
                or TripStatus.COMPLETED
                or TripStatus.CANCELLED)
        {
            return null;
        }

        var driverLocation = await repository.GetDriverLocationAsync(
            driverOffer.DriverId,
            cancellationToken);
        if (driverLocation is null)
        {
            return null;
        }

        try
        {
            var route = await mapRoutingService.GetRouteEstimateAsync(
                new RouteEstimateRequest
                {
                    Origin = driverLocation,
                    Destination = new LocationPoint(
                        booking.PickupLocation.Y,
                        booking.PickupLocation.X),
                    Provider = MapProvider.Auto,
                    TravelMode = MapTravelMode.Car,
                    IncludePolyline = true,
                    RequestSource = "DriverArrival"
                },
                cancellationToken);

            return route.EncodedPolyline;
        }
        catch
        {
            return null;
        }
    }
}
