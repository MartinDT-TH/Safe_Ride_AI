using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Infrastructure.Services;

public sealed class MatchingPolicyProvider : IMatchingPolicyProvider
{
    private readonly IOptionsMonitor<MatchingOptions> _options;

    public MatchingPolicyProvider(IOptionsMonitor<MatchingOptions> options)
    {
        _options = options;
    }

    public MatchingOptions Current => _options.CurrentValue;

    public DateTime? GetMatchingStartedAt(Booking booking)
    {
        if (booking.BookingStatus != BookingStatus.Searching
            && booking.BookingStatus != BookingStatus.DriverAssigned
            && booking.BookingStatus != BookingStatus.Expired)
        {
            return null;
        }

        if (booking.BookingType == BookingType.Now)
        {
            return booking.CreatedAt;
        }

        if (booking.BookingStatus is BookingStatus.Searching
            or BookingStatus.DriverAssigned
            or BookingStatus.Expired)
        {
            return booking.UpdatedAt;
        }

        return null;
    }

    public BookingMatchingSnapshot GetSnapshot(Booking booking, DateTime utcNow)
    {
        var startedAt = GetMatchingStartedAt(booking);
        if (!startedAt.HasValue)
        {
            return new BookingMatchingSnapshot(null, null, null, null, false);
        }

        var options = Current;
        var elapsed = utcNow - startedAt.Value;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        var isExpanded = elapsed >= TimeSpan.FromMinutes(options.ExpandAfterMinutes);
        var radius = isExpanded
            ? options.ExpandedRadiusKm
            : options.InitialRadiusKm;
        var expiresAt = startedAt.Value.AddMinutes(options.BookingExpireAfterMinutes);
        var remainingSeconds = Math.Max(
            0,
            (int)Math.Ceiling((expiresAt - utcNow).TotalSeconds));

        var message = booking.BookingStatus == BookingStatus.Searching
            ? isExpanded
                ? $"Đang mở rộng phạm vi tìm kiếm tài xế trong bán kính {options.ExpandedRadiusKm:0.#}km."
                : $"SafeRide đang tìm tài xế gần bạn trong bán kính {options.InitialRadiusKm:0.#}km."
            : null;

        return new BookingMatchingSnapshot(
            radius,
            expiresAt,
            remainingSeconds,
            message,
            isExpanded);
    }
}
