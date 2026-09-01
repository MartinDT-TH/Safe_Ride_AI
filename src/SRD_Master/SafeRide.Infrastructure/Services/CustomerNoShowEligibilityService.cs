using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Features.Bookings;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class CustomerNoShowEligibilityService : ICustomerNoShowEligibilityService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IOptionsMonitor<CustomerNoShowOptions> _options;

    public CustomerNoShowEligibilityService(ApplicationDbContext dbContext, IDateTimeProvider dateTimeProvider, IOptionsMonitor<CustomerNoShowOptions> options)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _options = options;
    }

    public async Task<CustomerNoShowEligibilityResponse> GetAsync(Guid driverId, long tripId, CancellationToken cancellationToken)
    {
        var trip = await _dbContext.Trips.AsNoTracking()
            .Include(x => x.Booking)
            .FirstOrDefaultAsync(x => x.Id == tripId, cancellationToken);
        if (trip is null)
            throw new BookingException("trip.not_found", "Không tìm thấy chuyến đi.", 404);
        if (trip.DriverId != driverId)
            throw new BookingException("trip.driver_not_assigned", "Tài xế không được phân công cho chuyến đi này.", 403);

        var now = _dateTimeProvider.UtcNow;
        var waitMinutes = _options.CurrentValue.NoShowWaitMinutes;
        var waitSatisfiedAt = trip.ArrivedAt?.AddMinutes(waitMinutes);
        var existing = await _dbContext.CustomerBehaviorEvents.AsNoTracking()
            .AnyAsync(x => x.TripId == tripId
                && x.EventType == CustomerBehaviorEventType.VERIFIED_NO_SHOW
                && x.Status != CustomerBehaviorEventStatus.REVERSED, cancellationToken);
        long? remaining = waitSatisfiedAt is null
            ? null
            : Math.Max(0, (long)Math.Ceiling((waitSatisfiedAt.Value - now).TotalSeconds));

        var reasonCode = "ELIGIBLE";
        var reasonMessage = "Có thể báo khách không xuất hiện.";
        if (trip.TripStatus != TripStatus.ARRIVED || trip.ArrivedAt is null)
            (reasonCode, reasonMessage) = ("NOT_ARRIVED", "Chuyến đi chưa ở trạng thái đã đến điểm đón.");
        else if (trip.ArrivalLocationVerifiedAt is null)
            (reasonCode, reasonMessage) = ("ARRIVAL_NOT_GPS_VERIFIED", "Chưa xác minh GPS tại điểm đón.");
        else if (waitSatisfiedAt > now)
            (reasonCode, reasonMessage) = ("WAIT_TIME_NOT_SATISFIED", "Chưa đủ thời gian chờ khách.");
        else if (trip.CustomerNoShowReminderSentAt is null)
            (reasonCode, reasonMessage) = ("REMINDER_NOT_SENT", "Chưa gửi nhắc nhở cho khách.");
        else if (trip.StartedAt is not null || trip.TripStatus is TripStatus.IN_PROGRESS or TripStatus.COMPLETED or TripStatus.CANCELLED)
            (reasonCode, reasonMessage) = ("TRIP_ALREADY_STARTED", "Chuyến đi đã bắt đầu hoặc đã kết thúc.");
        else if (existing)
            (reasonCode, reasonMessage) = ("ALREADY_REPORTED", "Chuyến đi đã được báo khách không xuất hiện.");

        var canReport = reasonCode == "ELIGIBLE";
        return new CustomerNoShowEligibilityResponse(tripId, canReport, reasonCode, reasonMessage,
            trip.TripStatus, trip.ArrivedAt, trip.ArrivalLocationVerifiedAt, waitMinutes,
            waitSatisfiedAt, now, remaining, trip.CustomerNoShowReminderSentAt is not null,
            trip.CustomerNoShowReminderSentAt, existing);
    }
}
