using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Auth;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Authentication;

namespace SafeRide.Infrastructure.Services;

public sealed class TripContinuationAccessService : ITripContinuationAccessService
{
    private readonly ITripSessionQueryService _tripSessionQueryService;
    private readonly IOptionsMonitor<TripContinuationOptions> _options;

    public TripContinuationAccessService(
        ITripSessionQueryService tripSessionQueryService,
        IOptionsMonitor<TripContinuationOptions> options)
    {
        _tripSessionQueryService = tripSessionQueryService;
        _options = options;
    }

    public async Task<bool> IsAllowedAsync(
        ClaimsPrincipal user,
        TripContinuationOperation operation,
        long? tripId = null,
        long? bookingId = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsContinuationSession(user))
        {
            return true;
        }

        if (!TryGetUserId(user, out var userId)
            || !TryGetContinuationTripId(user, out var continuationTripId))
        {
            return false;
        }

        if (tripId.HasValue && tripId.Value != continuationTripId)
        {
            return false;
        }

        var trip = await LoadTripAsync(
            userId,
            continuationTripId,
            tripId,
            bookingId,
            cancellationToken);
        if (trip is null || trip.TripId != continuationTripId)
        {
            return false;
        }

        if (trip.CustomerId != userId && trip.DriverId != userId)
        {
            return false;
        }

        if (bookingId.HasValue && trip.BookingId != bookingId.Value)
        {
            return false;
        }

        return operation switch
        {
            TripContinuationOperation.ActiveTripRead => true,
            TripContinuationOperation.BookingRead => IsActive(trip) || IsRatingGrace(trip),
            TripContinuationOperation.BookingCancel => IsActive(trip),
            TripContinuationOperation.DriverLocation => trip.DriverId == userId && IsActive(trip),
            TripContinuationOperation.TripStatusUpdate => trip.DriverId == userId && IsActive(trip),
            TripContinuationOperation.TripReturnConfirmation => IsActive(trip),
            TripContinuationOperation.TripPayment => IsActive(trip),
            TripContinuationOperation.TripRating => trip.CustomerId == userId && IsRatingGrace(trip),
            TripContinuationOperation.SignalRJoinTrip => IsActive(trip),
            TripContinuationOperation.SignalRJoinBooking => IsActive(trip),
            _ => false
        };
    }

    public static bool IsContinuationSession(ClaimsPrincipal user)
    {
        return string.Equals(
            user.FindFirstValue(AuthClaimTypes.SessionMode),
            AuthSessionModes.TripContinuation,
            StringComparison.Ordinal);
    }

    private async Task<TripSessionInfo?> LoadTripAsync(
        Guid userId,
        long continuationTripId,
        long? tripId,
        long? bookingId,
        CancellationToken cancellationToken)
    {
        if (bookingId.HasValue)
        {
            return await _tripSessionQueryService.GetTripForBookingForUserAsync(
                userId,
                bookingId.Value,
                cancellationToken);
        }

        return await _tripSessionQueryService.GetTripForUserAsync(
            userId,
            tripId ?? continuationTripId,
            cancellationToken);
    }

    private bool IsRatingGrace(TripSessionInfo trip)
    {
        if (trip.TripStatus != TripStatus.COMPLETED || !trip.CompletedAt.HasValue)
        {
            return false;
        }

        return trip.CompletedAt.Value.AddMinutes(
            _options.CurrentValue.PostCompletionRatingGraceMinutes) >= DateTime.UtcNow;
    }

    private static bool IsActive(TripSessionInfo trip)
    {
        return trip.TripStatus is not TripStatus.COMPLETED and not TripStatus.CANCELLED;
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        return Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }

    private static bool TryGetContinuationTripId(
        ClaimsPrincipal user,
        out long tripId)
    {
        return long.TryParse(
            user.FindFirstValue(AuthClaimTypes.ContinuationTripId),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out tripId);
    }
}
