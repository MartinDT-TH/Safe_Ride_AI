using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.AdminNotifications;
using SafeRide.Application.Features.Notifications;
using SafeRide.Application.Features.StaffNotifications;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class StaffNotificationRequestService : IStaffNotificationRequestService
{
    private readonly ApplicationDbContext _db;
    private readonly IAdminNotificationManagementService _adminNotificationManagementService;

    public StaffNotificationRequestService(
        ApplicationDbContext db,
        IAdminNotificationManagementService adminNotificationManagementService)
    {
        _db = db;
        _adminNotificationManagementService = adminNotificationManagementService;
    }

    public async Task<StaffNotificationRequestPagedResult> GetRequestsAsync(
        StaffNotificationRequestListFilter filter,
        CancellationToken cancellationToken)
    {
        await EnsureStaffUserExistsAsync(filter.CreatedBy, cancellationToken);

        var page = filter.Page <= 0 ? 1 : filter.Page;
        var pageSize = filter.PageSize <= 0 ? 10 : Math.Min(filter.PageSize, 50);

        var baseQuery = _db.AdminNotifications
            .AsNoTracking()
            .Where(x => x.CreatedBy == filter.CreatedBy);

        baseQuery = ApplySearch(baseQuery, filter.Search);
        baseQuery = ApplyTypeFilter(baseQuery, filter.Type);
        baseQuery = ApplyAudienceFilter(baseQuery, filter.Audience);

        var counts = new StaffNotificationRequestCountsResponse(
            await baseQuery.CountAsync(cancellationToken),
            await baseQuery.CountAsync(x => x.Status == AdminNotificationStatus.Pending, cancellationToken),
            await baseQuery.CountAsync(x => x.Status == AdminNotificationStatus.Approved, cancellationToken),
            await baseQuery.CountAsync(x => x.Status == AdminNotificationStatus.Rejected, cancellationToken));

        var filteredQuery = ApplyStatusFilter(baseQuery, filter.Status);
        var totalItems = await filteredQuery.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var currentPage = Math.Min(page, totalPages);

        var pagedNotifications = filteredQuery
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize);

        var rows = await (from notification in pagedNotifications
                          join createdBy in _db.Users.AsNoTracking()
                              on notification.CreatedBy equals createdBy.Id
                          join approvedBy in _db.Users.AsNoTracking()
                              on notification.ApprovedBy equals approvedBy.Id into approvedJoin
                          from approvedBy in approvedJoin.DefaultIfEmpty()
                          join rejectedBy in _db.Users.AsNoTracking()
                              on notification.RejectedBy equals rejectedBy.Id into rejectedJoin
                          from rejectedBy in rejectedJoin.DefaultIfEmpty()
                          select new
                          {
                              notification.Id,
                              notification.Title,
                              notification.Content,
                              notification.NotificationType,
                              notification.TargetAudience,
                              notification.Status,
                              notification.CreatedBy,
                              CreatedByFullName = createdBy.FullName,
                              CreatedByEmail = createdBy.Email,
                              CreatedByPhoneNumber = createdBy.PhoneNumber,
                              notification.CreatedAt,
                              notification.ApprovedBy,
                              ApprovedByFullName = approvedBy == null ? null : approvedBy.FullName,
                              ApprovedByEmail = approvedBy == null ? null : approvedBy.Email,
                              ApprovedByPhoneNumber = approvedBy == null ? null : approvedBy.PhoneNumber,
                              notification.ApprovedAt,
                              notification.RejectedBy,
                              RejectedByFullName = rejectedBy == null ? null : rejectedBy.FullName,
                              RejectedByEmail = rejectedBy == null ? null : rejectedBy.Email,
                              RejectedByPhoneNumber = rejectedBy == null ? null : rejectedBy.PhoneNumber,
                              notification.RejectedAt,
                              notification.RejectedReason
                          })
            .ToListAsync(cancellationToken);

        var items = rows.Select(x => new StaffNotificationRequestResponse(
                x.Id,
                x.Title,
                x.Content,
                x.NotificationType,
                x.TargetAudience.ToString(),
                x.Status.ToString(),
                x.CreatedBy,
                DisplayName(x.CreatedBy, x.CreatedByFullName, x.CreatedByEmail, x.CreatedByPhoneNumber),
                x.CreatedAt,
                x.ApprovedBy,
                x.ApprovedBy.HasValue
                    ? DisplayName(x.ApprovedBy.Value, x.ApprovedByFullName, x.ApprovedByEmail, x.ApprovedByPhoneNumber)
                    : null,
                x.ApprovedAt,
                x.RejectedBy,
                x.RejectedBy.HasValue
                    ? DisplayName(x.RejectedBy.Value, x.RejectedByFullName, x.RejectedByEmail, x.RejectedByPhoneNumber)
                    : null,
                x.RejectedAt,
                x.RejectedReason))
            .ToList();

        return new StaffNotificationRequestPagedResult(
            items,
            counts,
            currentPage,
            pageSize,
            totalItems,
            totalPages);
    }

    public async Task<StaffNotificationRequestResponse> CreateRequestAsync(
        Guid createdBy,
        string title,
        string content,
        string notificationType,
        NotificationAudience targetAudience,
        CancellationToken cancellationToken)
    {
        await EnsureStaffUserExistsAsync(createdBy, cancellationToken);

        var created = await _adminNotificationManagementService.CreateNotificationAsync(
            createdBy,
            title,
            content,
            notificationType,
            targetAudience,
            cancellationToken);

        return Map(created);
    }

    private async Task EnsureStaffUserExistsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var exists = await _db.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == userId && x.IsActive, cancellationToken);
        if (!exists)
        {
            throw new NotificationException(
                "staff.notification.staff_not_found",
                "Khong tim thay tai khoan nhan vien tao yeu cau thong bao.",
                StatusCodes.Status404NotFound);
        }
    }

    private IQueryable<StaffNotificationRequestResponse> ProjectRequests(
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
               select new StaffNotificationRequestResponse(
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

    private static StaffNotificationRequestResponse Map(
        AdminNotificationResponse response)
    {
        return new StaffNotificationRequestResponse(
            response.Id,
            response.Title,
            response.Content,
            response.NotificationType,
            response.TargetAudience,
            response.Status,
            response.CreatedBy,
            response.CreatedByName,
            response.CreatedAt,
            response.ApprovedBy,
            response.ApprovedByName,
            response.ApprovedAt,
            response.RejectedBy,
            response.RejectedByName,
            response.RejectedAt,
            response.RejectedReason);
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
        return IsAllFilter(notificationType)
            ? query
            : query.Where(x => x.NotificationType == notificationType!.Trim());
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

    private static string DisplayName(
        Guid userId,
        string? fullName,
        string? email,
        string? phoneNumber)
    {
        return fullName
            ?? email
            ?? phoneNumber
            ?? userId.ToString();
    }
}
