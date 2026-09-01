using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Infrastructure.Services;

public sealed class TripCustomerNoShowReminderService : ITripCustomerNoShowReminderService
{
    private const string ReminderTitle = "Tài xế đã đến điểm đón";
    private const string ReminderContent = "Tài xế đã đến điểm đón. Vui lòng ra điểm đón để bắt đầu chuyến đi.";
    private readonly ApplicationDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public TripCustomerNoShowReminderService(ApplicationDbContext dbContext, IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<bool> RecordIfNeededAsync(long tripId, CancellationToken cancellationToken)
    {
        var trip = await _dbContext.Trips
            .Include(x => x.Booking)
            .FirstOrDefaultAsync(x => x.Id == tripId, cancellationToken);
        if (trip is null || trip.TripStatus != TripStatus.ARRIVED
            || trip.ArrivedAt is null || trip.StartedAt is not null
            || trip.CustomerNoShowReminderSentAt is not null
            || trip.Booking.BookingStatus is BookingStatus.Cancelled or BookingStatus.Completed)
            return false;

        var now = _dateTimeProvider.UtcNow;
        _dbContext.Notifications.Add(new Notification
        {
            UserId = trip.Booking.CustomerId,
            Title = ReminderTitle,
            Content = ReminderContent,
            NotificationType = "CustomerNoShowReminder",
            ReferenceId = trip.Id,
            SentAt = now
        });
        trip.CustomerNoShowReminderSentAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
