using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Exceptions;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Features.Bookings;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class CustomerBookingPrivilegeService : ICustomerBookingPrivilegeService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IOptionsMonitor<CustomerNoShowOptions> _options;

    public CustomerBookingPrivilegeService(
        ApplicationDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        IOptionsMonitor<CustomerNoShowOptions> options)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _options = options;
    }

    public async Task<CustomerBookingPrivilege> RecalculateAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.UtcNow;
        var options = _options.CurrentValue;
        var windowStart = now.AddDays(-options.BehaviorWindowDays);
        var verifiedEvents = await _dbContext.CustomerBehaviorEvents
            .Where(x => x.CustomerId == customerId
                && x.EventType == CustomerBehaviorEventType.VERIFIED_NO_SHOW
                && x.Status != CustomerBehaviorEventStatus.REVERSED
                && x.Status != CustomerBehaviorEventStatus.EXEMPTED
                && (x.VerifiedAt ?? x.CreatedAt) >= windowStart)
            .OrderBy(x => x.VerifiedAt ?? x.CreatedAt)
            .ToListAsync(cancellationToken);

        var completedTripCount = await _dbContext.Trips
            .Where(x => x.Booking.CustomerId == customerId
                && x.TripStatus == TripStatus.COMPLETED
                && x.CompletedAt.HasValue
                && x.CompletedAt >= windowStart)
            .CountAsync(cancellationToken);
        var verifiedNoShowCount = verifiedEvents.Count;
        var eligibleBookingCount = completedTripCount + verifiedNoShowCount;
        var noShowRate = eligibleBookingCount == 0
            ? 0m
            : (decimal)verifiedNoShowCount / eligibleBookingCount;

        var latestCompletedAt = await _dbContext.Trips
            .Where(x => x.Booking.CustomerId == customerId
                && x.TripStatus == TripStatus.COMPLETED
                && x.CompletedAt.HasValue)
            .MaxAsync(x => (DateTime?)x.CompletedAt, cancellationToken);
        var streakEvents = latestCompletedAt.HasValue
            ? verifiedEvents.Where(x => (x.VerifiedAt ?? x.CreatedAt) > latestCompletedAt.Value).ToList()
            : verifiedEvents;
        var streak = streakEvents.Count;
        var latestNoShowAt = verifiedEvents.LastOrDefault()?.VerifiedAt
            ?? verifiedEvents.LastOrDefault()?.CreatedAt;
        var hasTwoInSevenDays = HasRecentStreak(streakEvents, 2, 7);
        var hasThreeInFourteenDays = HasRecentStreak(streakEvents, 3, 14);

        var privilege = await _dbContext.CustomerBookingPrivileges
            .SingleOrDefaultAsync(x => x.CustomerId == customerId, cancellationToken);
        var previousLevel = privilege?.RestrictionLevel ?? CustomerBehaviorRestrictionLevel.NORMAL;
        privilege ??= new CustomerBookingPrivilege { CustomerId = customerId };
        var level = ResolveLevel(
            verifiedNoShowCount,
            noShowRate,
            eligibleBookingCount,
            hasTwoInSevenDays,
            hasThreeInFourteenDays,
            previousLevel,
            streakEvents);

        privilege.VerifiedNoShowCount = verifiedNoShowCount;
        privilege.EligibleBookingCount = eligibleBookingCount;
        privilege.NoShowRate = noShowRate;
        privilege.ConsecutiveNoShowStreak = streak;
        privilege.LastNoShowAt = latestNoShowAt;
        privilege.RestrictionLevel = level;
        privilege.ScheduledBookingAllowed = level is not (
            CustomerBehaviorRestrictionLevel.SCHEDULE_RISK
            or CustomerBehaviorRestrictionLevel.PERSISTENT_ABUSE
            or CustomerBehaviorRestrictionLevel.STAFF_REVIEW
            or CustomerBehaviorRestrictionLevel.TEMP_RESTRICTED);
        privilege.ScheduledRestrictedUntil = privilege.ScheduledBookingAllowed
            ? null
            : now.AddDays(level == CustomerBehaviorRestrictionLevel.PERSISTENT_ABUSE
                ? options.ScheduleRestrictionDaysPersistent
                : options.ScheduleRestrictionDaysFirst);
        privilege.InstantBookingAllowed = level != CustomerBehaviorRestrictionLevel.PERSISTENT_ABUSE;
        privilege.BookingCooldownUntil = level == CustomerBehaviorRestrictionLevel.PERSISTENT_ABUSE
            && latestNoShowAt.HasValue
            ? now.AddHours(options.InstantCooldownHoursPersistent)
            : null;
        privilege.UnderStaffReview = level == CustomerBehaviorRestrictionLevel.STAFF_REVIEW;
        privilege.UpdatedAt = now;

        if (_dbContext.Entry(privilege).State == EntityState.Detached)
            _dbContext.CustomerBookingPrivileges.Add(privilege);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return privilege;
    }

    public async Task EnsureCanCreateAsync(
        Guid customerId,
        BookingType bookingType,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var privilege = await _dbContext.CustomerBookingPrivileges
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.CustomerId == customerId, cancellationToken);
        if (privilege is null)
            return;

        if (bookingType == BookingType.Scheduled
            && !privilege.ScheduledBookingAllowed
            && privilege.ScheduledRestrictedUntil > utcNow)
        {
            throw new BookingException(
                "booking.scheduled_restricted",
                $"Tính năng đặt lịch đang bị hạn chế đến {privilege.ScheduledRestrictedUntil:dd/MM/yyyy HH:mm} do nhiều lần không xuất hiện.",
                409);
        }

        if (bookingType == BookingType.Now
            && !privilege.InstantBookingAllowed
            && privilege.BookingCooldownUntil > utcNow)
        {
            throw new BookingException(
                "booking.instant_cooldown",
                $"Bạn cần chờ đến {privilege.BookingCooldownUntil:dd/MM/yyyy HH:mm} trước khi tạo chuyến mới.",
                409);
        }
    }

    private static CustomerBehaviorRestrictionLevel ResolveLevel(
        int noShowCount,
        decimal noShowRate,
        int eligibleBookingCount,
        bool hasTwoInSevenDays,
        bool hasThreeInFourteenDays,
        CustomerBehaviorRestrictionLevel previousLevel,
        IReadOnlyList<CustomerBehaviorEvent> streakEvents)
    {
        if (noShowCount >= 5 && noShowRate >= .50m)
            return CustomerBehaviorRestrictionLevel.STAFF_REVIEW;
        var persistentAfterSchedule = previousLevel == CustomerBehaviorRestrictionLevel.SCHEDULE_RISK
            && HasRecentStreak(streakEvents, 2, 14);
        if ((noShowCount >= 4 && noShowRate >= .40m) || persistentAfterSchedule)
            return CustomerBehaviorRestrictionLevel.PERSISTENT_ABUSE;
        if ((noShowCount >= 3 && noShowRate >= .30m && eligibleBookingCount >= 5)
            || hasThreeInFourteenDays)
            return CustomerBehaviorRestrictionLevel.SCHEDULE_RISK;
        if (noShowCount >= 2 || hasTwoInSevenDays)
            return CustomerBehaviorRestrictionLevel.WARNING;
        if (noShowCount == 1)
            return CustomerBehaviorRestrictionLevel.REMINDER;
        return CustomerBehaviorRestrictionLevel.NORMAL;
    }

    private static bool HasRecentStreak(
        IReadOnlyList<CustomerBehaviorEvent> events,
        int length,
        int days)
    {
        if (events.Count < length)
            return false;
        for (var i = length - 1; i < events.Count; i++)
        {
            var first = events[i - length + 1].VerifiedAt ?? events[i - length + 1].CreatedAt;
            var last = events[i].VerifiedAt ?? events[i].CreatedAt;
            if (last - first <= TimeSpan.FromDays(days))
                return true;
        }
        return false;
    }
}
