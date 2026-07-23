using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Notifications;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class UserNotificationService
    : IUserNotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UserNotificationService(
        ApplicationDbContext db,
        IDateTimeProvider dateTimeProvider)
    {
        _db = db;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<UserNotificationsPageResponse> GetNotificationsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var currentPage = page <= 0 ? 1 : page;
        var normalizedPageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 50);

        var query = _db.Notifications
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        var totalItems = await query.CountAsync(cancellationToken);
        var unreadCount = await query.CountAsync(x => !x.IsRead, cancellationToken);
        var totalPages = totalItems == 0
            ? 1
            : (int)Math.Ceiling(totalItems / (double)normalizedPageSize);
        currentPage = Math.Min(currentPage, totalPages);

        var items = await query
            .OrderByDescending(x => x.SentAt)
            .Skip((currentPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new UserNotificationResponse(
                x.Id,
                x.Title,
                x.Content,
                x.NotificationType,
                x.IsRead,
                x.SentAt,
                x.ReadAt))
            .ToListAsync(cancellationToken);

        return new UserNotificationsPageResponse(
            items,
            currentPage,
            normalizedPageSize,
            totalItems,
            totalPages,
            unreadCount);
    }

    public async Task<UserNotificationResponse> MarkAsReadAsync(
        Guid userId,
        long notificationId,
        CancellationToken cancellationToken)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(
                x => x.Id == notificationId && x.UserId == userId,
                cancellationToken)
            ?? throw new NotificationException(
                "notification.not_found",
                "Không tìm thấy thông báo.",
                StatusCodes.Status404NotFound);

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = _dateTimeProvider.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new UserNotificationResponse(
            notification.Id,
            notification.Title,
            notification.Content,
            notification.NotificationType,
            notification.IsRead,
            notification.SentAt,
            notification.ReadAt);
    }
}
