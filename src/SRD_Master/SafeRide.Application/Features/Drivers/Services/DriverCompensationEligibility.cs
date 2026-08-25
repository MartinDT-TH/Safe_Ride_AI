using SafeRide.Application.Common.Models;
using SafeRide.Domain.Entities;

namespace SafeRide.Application.Features.Drivers.Services;

/// <summary>
/// Pure Phase 3 eligibility rules.  Redis proximity is deliberately not an input:
/// pickup eligibility is based only on the authoritative routing snapshot persisted
/// with an offer, while trip distance is the immutable booking estimate.
/// </summary>
public static class DriverCompensationEligibility
{
    public static bool IsHourlyBooking(Booking booking) =>
        booking.AcceptedPricePerHour is > 0m;

    public static bool RequiresLongDistanceOptIn(
        Booking booking,
        DriverCompensationOptions options) =>
        !IsHourlyBooking(booking)
        && booking.EstimatedDistanceKm is > 0m
        && booking.EstimatedDistanceKm.Value > (decimal)options.LongDistanceOptInThresholdKm;

    public static bool ExceedsMaximumTripDistance(
        Booking booking,
        DriverCompensationOptions options) =>
        !IsHourlyBooking(booking)
        && booking.EstimatedDistanceKm is > 0m
        && booking.EstimatedDistanceKm.Value > (decimal)options.MaximumTripDistanceKm;

    public static bool RequiresLongPickupOptIn(
        decimal pickupDistanceKm,
        DriverCompensationOptions options) =>
        pickupDistanceKm > (decimal)options.LongPickupOptInThresholdKm;

    public static decimal CalculateLongPickupCompensation(
        decimal pickupDistanceKm,
        DriverCompensationOptions options)
    {
        var eligibleDistance = Math.Max(
            0m,
            pickupDistanceKm - (decimal)options.LongPickupThresholdKm);
        return decimal.Round(
            eligibleDistance * options.LongPickupRatePerKm,
            0,
            MidpointRounding.AwayFromZero);
    }
}
