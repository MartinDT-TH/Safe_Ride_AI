using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.AdminNotifications;
using SafeRide.Application.Features.Notifications;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class AdminNotificationManagementService
    : IAdminNotificationManagementService
{
    private static readonly HashSet<string> SupportedTypes =
    [
        "Promotion",
        "System Update",
        "Warning"
    ];

    private const string CustomerRole = "Customer";
    private const string DriverRole = "Driver";

    private readonly ApplicationDbContext _db;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ISystemNotificationDeliveryService _notificationDeliveryService;
    private readonly ILogger<AdminNotificationManagementService> _logger;

    public AdminNotificationManagementService(
        ApplicationDbContext db,
        IDateTimeProvider dateTimeProvider,
        ISystemNotificationDeliveryService notificationDeliveryService,
        ILogger<AdminNotificationManagementService> logger)
    {
        _db = db;
        _dateTimeProvider = dateTimeProvider;
        _notificationDeliveryService = notificationDeliveryService;
        _logger = logger;
    }

    public async Task<AdminNotificationPagedResult> GetNotificationsAsync(
        AdminNotificationListFilter filter,
        CancellationToken cancellationToken)
    {
        var page = filter.Page <= 0 ? 1 : filter.Page;
        var pageSize = filter.PageSize <= 0 ? 10 : Math.Min(filter.PageSize, 50);

        var baseQuery = _db.AdminNotifications.AsNoTracking();
        baseQuery = ApplySearch(baseQuery, filter.Search);
        baseQuery = ApplyTypeFilter(baseQuery, filter.Type);
        baseQuery = ApplyAudienceFilter(baseQuery, filter.Audience);

        var counts = new AdminNotificationCountsResponse(
            await baseQuery.CountAsync(cancellationToken),
            await baseQuery.CountAsync(x => x.Status == AdminNotificationStatus.Pending, cancellationToken),
            await baseQuery.CountAsync(x => x.Status == AdminNotificationStatus.Approved, cancellationToken),
            await baseQuery.CountAsync(x => x.Status == AdminNotificationStatus.Rejected, cancellationToken));

        var filteredQuery = ApplyStatusFilter(baseQuery, filter.Status);
        var totalItems = await filteredQuery.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var currentPage = Math.Min(page, totalPages);
        var skip = (currentPage - 1) * pageSize;
        var pagedQuery = filteredQuery
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(pageSize);

        var items = await ProjectAdminNotifications(pagedQuery)
            .ToListAsync(cancellationToken);

        return new AdminNotificationPagedResult(
            items,
            counts,
            currentPage,
            pageSize,
            totalItems,
            totalPages);
    }

    public async Task<AdminNotificationResponse> CreateNotificationAsync(
        Guid createdBy,
        string title,
        string content,
        string notificationType,
        NotificationAudience targetAudience,
        CancellationToken cancellationToken)
    {
        await EnsureUserExistsAsync(
            createdBy,
            "notification.admin_not_found",
            "Không tìm thấy quản trị viên tạo thông báo.",
            cancellationToken);

        var normalizedType = NormalizeNotificationType(notificationType);
        var now = _dateTimeProvider.UtcNow;
        var adminNotification = new AdminNotification
        {
            Title = NormalizeTitle(title),
            Content = NormalizeContent(content),
            NotificationType = normalizedType,
            TargetAudience = targetAudience,
            Status = AdminNotificationStatus.Pending,
            CreatedBy = createdBy,
            CreatedAt = now
        };

        _db.AdminNotifications.Add(adminNotification);
        await _db.SaveChangesAsync(cancellationToken);

        return await LoadAdminNotificationAsync(adminNotification.Id, cancellationToken);
    }

    public async Task<AdminNotificationResponse> ApproveNotificationAsync(
        long notificationId,
        Guid approvedBy,
        CancellationToken cancellationToken)
    {
        await EnsureUserExistsAsync(
            approvedBy,
            "notification.admin_not_found",
            "Không tìm thấy quản trị viên duyệt thông báo.",
            cancellationToken);

        var adminNotification = await _db.AdminNotifications
            .FirstOrDefaultAsync(x => x.Id == notificationId, cancellationToken)
            ?? throw new NotificationException(
                "notification.not_found",
                "Không tìm thấy thông báo cần duyệt.",
                StatusCodes.Status404NotFound);

        if (adminNotification.Status != AdminNotificationStatus.Pending)
        {
            throw new NotificationException(
                "notification.invalid_approval_state",
                "Chỉ có thể duyệt thông báo đang chờ xử lý.",
                StatusCodes.Status400BadRequest);
        }

        var recipientIds = await LoadRecipientIdsAsync(
            adminNotification.TargetAudience,
            cancellationToken);
        if (recipientIds.Count == 0)
        {
            throw new NotificationException(
                "notification.no_recipients",
                "Không tìm thấy người nhận phù hợp cho thông báo này.",
                StatusCodes.Status400BadRequest);
        }

        var now = _dateTimeProvider.UtcNow;
        adminNotification.Status = AdminNotificationStatus.Approved;
        adminNotification.ApprovedBy = approvedBy;
        adminNotification.ApprovedAt = now;
        adminNotification.RejectedBy = null;
        adminNotification.RejectedAt = null;
        adminNotification.RejectedReason = null;

        var deliveredNotifications = recipientIds
            .Select(recipientId => new Notification
            {
                UserId = recipientId,
                Title = adminNotification.Title,
                Content = adminNotification.Content,
                NotificationType = adminNotification.NotificationType,
                IsRead = false,
                SentAt = now
            })
            .ToList();

        _db.Notifications.AddRange(deliveredNotifications);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var realtimeEvents = deliveredNotifications
                .Select(x => new UserNotificationRealtimeEvent(
                    x.UserId,
                    x.Id,
                    x.Title,
                    x.Content,
                    x.NotificationType,
                    x.SentAt))
                .ToArray();

            await _notificationDeliveryService.PublishUserNotificationsAsync(
                realtimeEvents,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to push realtime admin notification {NotificationId} after approval.",
                adminNotification.Id);
        }

        return await LoadAdminNotificationAsync(adminNotification.Id, cancellationToken);
    }

    public async Task<AdminNotificationResponse> RejectNotificationAsync(
        long notificationId,
        Guid rejectedBy,
        string rejectionReason,
        CancellationToken cancellationToken)
    {
        await EnsureUserExistsAsync(
            rejectedBy,
            "notification.admin_not_found",
            "Không tìm thấy quản trị viên từ chối thông báo.",
            cancellationToken);

        var adminNotification = await _db.AdminNotifications
            .FirstOrDefaultAsync(x => x.Id == notificationId, cancellationToken)
            ?? throw new NotificationException(
                "notification.not_found",
                "Không tìm thấy thông báo cần từ chối.",
                StatusCodes.Status404NotFound);

        if (adminNotification.Status != AdminNotificationStatus.Pending)
        {
            throw new NotificationException(
                "notification.invalid_rejection_state",
                "Chỉ có thể từ chối thông báo đang chờ xử lý.",
                StatusCodes.Status400BadRequest);
        }

        var normalizedReason = NormalizeRejectionReason(rejectionReason);
        var now = _dateTimeProvider.UtcNow;
        adminNotification.Status = AdminNotificationStatus.Rejected;
        adminNotification.RejectedBy = rejectedBy;
        adminNotification.RejectedAt = now;
        adminNotification.RejectedReason = normalizedReason;
        adminNotification.ApprovedBy = null;
        adminNotification.ApprovedAt = null;

        await _db.SaveChangesAsync(cancellationToken);

        return await LoadAdminNotificationAsync(adminNotification.Id, cancellationToken);
    }

    private async Task EnsureUserExistsAsync(
        Guid userId,
        string code,
        string message,
        CancellationToken cancellationToken)
    {
        var exists = await _db.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId, cancellationToken);
        if (!exists)
        {
            throw new NotificationException(code, message, StatusCodes.Status404NotFound);
        }
    }

    private async Task<AdminNotificationResponse> LoadAdminNotificationAsync(
        long notificationId,
        CancellationToken cancellationToken)
    {
        return await ProjectAdminNotifications(
                _db.AdminNotifications
                    .AsNoTracking()
                    .Where(x => x.Id == notificationId))
            .SingleAsync(cancellationToken);
    }

    private IQueryable<AdminNotificationResponse> ProjectAdminNotifications(
        IQueryable<AdminNotification> query)
    {
        return from notification in query
               join createdBy in _db.Users.AsNoTracking()
                    on notification.CreatedBy equals createdBy.Id
               join approvedBy in _db.Users.AsNoTracking()
                    on notification.ApprovedBy equals approvedBy.Id into approvedJoin
               from approvedBy in approvedJoin.DefaultIfEmpty()
               join rejectedBy in _db.Users.AsNoTracking()
                    on notification.RejectedBy equals rejectedBy.Id into rejectedJoin
               from rejectedBy in rejectedJoin.DefaultIfEmpty()
               select new AdminNotificationResponse(
                   notification.Id,
                   notification.Title,
                   notification.Content,
                   notification.NotificationType,
                   notification.TargetAudience.ToString(),
                   notification.Status.ToString(),
                   notification.CreatedBy,
                   DisplayName(createdBy),
                   notification.CreatedAt,
                   notification.ApprovedBy,
                   approvedBy == null ? null : DisplayName(approvedBy),
                   notification.ApprovedAt,
                   notification.RejectedBy,
                   rejectedBy == null ? null : DisplayName(rejectedBy),
                   notification.RejectedAt,
                   notification.RejectedReason);
    }

    private async Task<List<Guid>> LoadRecipientIdsAsync(
        NotificationAudience audience,
        CancellationToken cancellationToken)
    {
        var roleNames = audience switch
        {
            NotificationAudience.Customer => new[] { CustomerRole },
            NotificationAudience.Driver => new[] { DriverRole },
            _ => new[] { CustomerRole, DriverRole }
        };

        return await (
            from user in _db.Users.AsNoTracking()
            join userRole in _db.Set<IdentityUserRole<Guid>>().AsNoTracking()
                on user.Id equals userRole.UserId
            join role in _db.AspNetRoles.AsNoTracking()
                on userRole.RoleId equals role.Id
            where user.IsActive
                && role.Name != null
                && roleNames.Contains(role.Name)
            select user.Id)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<AdminNotification> ApplySearch(
        IQueryable<AdminNotification> query,
        string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var pattern = $"%{search.Trim()}%";
        return query.Where(x =>
            EF.Functions.Like(x.Title, pattern)
            || EF.Functions.Like(x.Content, pattern)
            || EF.Functions.Like(x.NotificationType, pattern));
    }

    private static IQueryable<AdminNotification> ApplyTypeFilter(
        IQueryable<AdminNotification> query,
        string? notificationType)
    {
        if (IsAllFilter(notificationType))
        {
            return query;
        }

        return query.Where(x => x.NotificationType == notificationType!.Trim());
    }

    private static IQueryable<AdminNotification> ApplyAudienceFilter(
        IQueryable<AdminNotification> query,
        string? audience)
    {
        if (IsAllFilter(audience))
        {
            return query;
        }

        if (!Enum.TryParse<NotificationAudience>(audience, true, out var parsedAudience))
        {
            return query.Where(_ => false);
        }

        return query.Where(x => x.TargetAudience == parsedAudience);
    }

    private static IQueryable<AdminNotification> ApplyStatusFilter(
        IQueryable<AdminNotification> query,
        string? status)
    {
        if (IsAllFilter(status))
        {
            return query;
        }

        if (!Enum.TryParse<AdminNotificationStatus>(status, true, out var parsedStatus))
        {
            return query.Where(_ => false);
        }

        return query.Where(x => x.Status == parsedStatus);
    }

    private static string NormalizeNotificationType(string notificationType)
    {
        var normalized = notificationType?.Trim() ?? string.Empty;
        var supportedType = SupportedTypes
            .FirstOrDefault(x => x.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (supportedType is not null)
        {
            return supportedType;
        }

        throw new NotificationException(
            "notification.invalid_type",
            "Loại thông báo không hợp lệ.",
            StatusCodes.Status400BadRequest);
    }

    private static string NormalizeTitle(string title)
    {
        var normalized = title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new NotificationException(
                "notification.title_required",
                "Vui lòng nhập tiêu đề thông báo.",
                StatusCodes.Status400BadRequest);
        }

        if (normalized.Length > 40)
        {
            throw new NotificationException(
                "notification.title_too_long",
                "Tiêu đề thông báo không được vượt quá 40 ký tự.",
                StatusCodes.Status400BadRequest);
        }

        return normalized;
    }

    private static string NormalizeContent(string content)
    {
        var normalized = content?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new NotificationException(
                "notification.content_required",
                "Vui lòng nhập nội dung thông báo.",
                StatusCodes.Status400BadRequest);
        }

        if (normalized.Length > 140)
        {
            throw new NotificationException(
                "notification.content_too_long",
                "Nội dung thông báo không được vượt quá 140 ký tự.",
                StatusCodes.Status400BadRequest);
        }

        return normalized;
    }

    private static string NormalizeRejectionReason(string rejectionReason)
    {
        var normalized = rejectionReason?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new NotificationException(
                "notification.rejection_reason_required",
                "Vui lòng nhập lý do từ chối thông báo.",
                StatusCodes.Status400BadRequest);
        }

        if (normalized.Length > 255)
        {
            throw new NotificationException(
                "notification.rejection_reason_too_long",
                "Lý do từ chối không được vượt quá 255 ký tự.",
                StatusCodes.Status400BadRequest);
        }

        return normalized;
    }

    private static bool IsAllFilter(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "all", StringComparison.OrdinalIgnoreCase);
    }

    private static string DisplayName(AspNetUser user)
    {
        return user.FullName
            ?? user.Email
            ?? user.PhoneNumber
            ?? user.Id.ToString();
    }
}
