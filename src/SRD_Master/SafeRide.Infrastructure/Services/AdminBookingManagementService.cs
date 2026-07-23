using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.AdminBookings;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class AdminBookingManagementService
    : IAdminBookingManagementService
{
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    private readonly ApplicationDbContext _db;
    private readonly IDateTimeProvider _dateTimeProvider;

    public AdminBookingManagementService(
        ApplicationDbContext db,
        IDateTimeProvider dateTimeProvider)
    {
        _db = db;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<AdminBookingPagedResult> GetBookingsAsync(
        AdminBookingListFilter filter,
        CancellationToken cancellationToken)
    {
        var page = filter.Page <= 0 ? 1 : filter.Page;
        var pageSize = filter.PageSize <= 0 ? 10 : Math.Min(filter.PageSize, 50);
        var normalizedRange = NormalizeDateRange(filter.FromDate, filter.ToDate);

        var baseQuery = _db.Bookings
            .AsNoTracking();

        baseQuery = ApplyDateRange(baseQuery, normalizedRange.FromDate, normalizedRange.ToDate);
        baseQuery = ApplySearch(baseQuery, filter.Search);

        var now = _dateTimeProvider.UtcNow;
        var nextScheduledAt = await baseQuery
            .Where(booking => booking.BookingStatus == BookingStatus.PendingSchedule
                && booking.ScheduledAt.HasValue
                && booking.ScheduledAt >= now)
            .OrderBy(booking => booking.ScheduledAt)
            .Select(booking => booking.ScheduledAt)
            .FirstOrDefaultAsync(cancellationToken);

        var counts = new AdminBookingCountsResponse(
            await baseQuery.CountAsync(cancellationToken),
            await baseQuery.CountAsync(
                booking => booking.BookingStatus == BookingStatus.Searching,
                cancellationToken),
            await baseQuery.CountAsync(
                booking => booking.BookingStatus == BookingStatus.PendingSchedule,
                cancellationToken),
            await baseQuery.CountAsync(
                booking => booking.BookingStatus == BookingStatus.Cancelled
                    || booking.BookingStatus == BookingStatus.Expired,
                cancellationToken),
            nextScheduledAt,
            nextScheduledAt.HasValue
                ? Math.Max(0, (int)Math.Ceiling((nextScheduledAt.Value - now).TotalMinutes))
                : null);

        var filteredQuery = ApplyStatusFilter(baseQuery, filter.Status);
        var totalItems = await filteredQuery.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var currentPage = Math.Min(page, totalPages);
        var skip = (currentPage - 1) * pageSize;

        var pagedQuery = ApplySort(
                filteredQuery,
                filter.SortBy,
                filter.SortDirection)
            .Skip(skip)
            .Take(pageSize);

        var items = await ProjectBookings(pagedQuery)
            .ToListAsync(cancellationToken);

        return new AdminBookingPagedResult(
            items,
            counts,
            currentPage,
            pageSize,
            totalItems,
            totalPages);
    }

    private static IQueryable<AdminBookingResponse> ProjectBookings(
        IQueryable<Booking> query)
    {
        return query.Select(booking => new AdminBookingResponse(
            booking.BookingId,
            "SR-" + booking.BookingId,
            booking.CustomerId,
            booking.Customer.FullName ?? booking.Customer.UserName ?? "Khách hàng",
            booking.Customer.PhoneNumber,
            booking.Customer.AvatarUrl,
            booking.Trip != null ? booking.Trip.DriverId : null,
            booking.Trip != null
                ? booking.Trip.Driver.Driver.FullName
                    ?? booking.Trip.Driver.Driver.UserName
                    ?? "Tài xế SafeRide"
                : null,
            booking.Trip != null ? booking.Trip.Driver.Driver.PhoneNumber : null,
            booking.Trip != null ? booking.Trip.Driver.Driver.AvatarUrl : null,
            booking.PickupAddress,
            booking.DestinationAddress,
            booking.Vehicle.BrandModel,
            booking.Vehicle.PlateNumber,
            booking.Vehicle.Color,
            booking.Vehicle.VehicleType,
            booking.ServiceType.ServiceName,
            booking.BookingType,
            booking.BookingStatus,
            booking.EstimatedFare,
            booking.Trip == null
                ? null
                : booking.Trip.Payments
                    .OrderByDescending(payment => payment.PaymentStatus == PaymentStatus.Success)
                    .ThenByDescending(payment => payment.CreatedAt)
                    .Select(payment => (PaymentMethod?)payment.PaymentMethod)
                    .FirstOrDefault(),
            booking.Trip == null
                ? null
                : booking.Trip.Payments
                    .OrderByDescending(payment => payment.PaymentStatus == PaymentStatus.Success)
                    .ThenByDescending(payment => payment.CreatedAt)
                    .Select(payment => (PaymentStatus?)payment.PaymentStatus)
                    .FirstOrDefault(),
            booking.ScheduledAt,
            booking.CreatedAt,
            booking.UpdatedAt));
    }

    private static IQueryable<Booking> ApplySearch(
        IQueryable<Booking> query,
        string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var normalizedSearch = search.Trim();
        var pattern = $"%{normalizedSearch}%";
        var hasBookingId = TryParseBookingId(normalizedSearch, out var bookingId);

        return query.Where(booking =>
            (hasBookingId && booking.BookingId == bookingId)
            || EF.Functions.Like(booking.PickupAddress, pattern)
            || (booking.DestinationAddress != null
                && EF.Functions.Like(booking.DestinationAddress, pattern))
            || EF.Functions.Like(booking.Vehicle.BrandModel, pattern)
            || EF.Functions.Like(booking.Vehicle.PlateNumber, pattern)
            || EF.Functions.Like(booking.ServiceType.ServiceName, pattern)
            || EF.Functions.Like(
                booking.Customer.FullName
                    ?? booking.Customer.UserName
                    ?? string.Empty,
                pattern)
            || EF.Functions.Like(
                booking.Customer.PhoneNumber ?? string.Empty,
                pattern)
            || (booking.Trip != null
                && EF.Functions.Like(
                    booking.Trip.Driver.Driver.FullName
                        ?? booking.Trip.Driver.Driver.UserName
                        ?? string.Empty,
                    pattern))
            || (booking.Trip != null
                && EF.Functions.Like(
                    booking.Trip.Driver.Driver.PhoneNumber ?? string.Empty,
                    pattern)));
    }

    private static IQueryable<Booking> ApplyStatusFilter(
        IQueryable<Booking> query,
        string? status)
    {
        if (IsAllFilter(status))
        {
            return query;
        }

        if (!Enum.TryParse<BookingStatus>(status, true, out var parsedStatus))
        {
            return query.Where(_ => false);
        }

        return query.Where(booking => booking.BookingStatus == parsedStatus);
    }

    private static IQueryable<Booking> ApplySort(
        IQueryable<Booking> query,
        string? sortBy,
        string? sortDirection)
    {
        var descending = !string.Equals(
            sortDirection,
            "asc",
            StringComparison.OrdinalIgnoreCase);

        return (sortBy ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "id" or "bookingid" => descending
                ? query.OrderByDescending(booking => booking.BookingId)
                    .ThenByDescending(booking => booking.CreatedAt)
                : query.OrderBy(booking => booking.BookingId)
                    .ThenBy(booking => booking.CreatedAt),
            "scheduledat" => descending
                ? query.OrderByDescending(booking => booking.ScheduledAt)
                    .ThenByDescending(booking => booking.BookingId)
                : query.OrderBy(booking => booking.ScheduledAt)
                    .ThenBy(booking => booking.BookingId),
            "updatedat" => descending
                ? query.OrderByDescending(booking => booking.UpdatedAt)
                    .ThenByDescending(booking => booking.BookingId)
                : query.OrderBy(booking => booking.UpdatedAt)
                    .ThenBy(booking => booking.BookingId),
            "estimatedfare" or "fare" => descending
                ? query.OrderByDescending(booking => booking.EstimatedFare)
                    .ThenByDescending(booking => booking.BookingId)
                : query.OrderBy(booking => booking.EstimatedFare)
                    .ThenBy(booking => booking.BookingId),
            _ => descending
                ? query.OrderByDescending(booking => booking.CreatedAt)
                    .ThenByDescending(booking => booking.BookingId)
                : query.OrderBy(booking => booking.CreatedAt)
                    .ThenBy(booking => booking.BookingId),
        };
    }

    private static IQueryable<Booking> ApplyDateRange(
        IQueryable<Booking> query,
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        if (fromDate.HasValue)
        {
            var fromUtc = ConvertLocalDateStartToUtc(fromDate.Value);
            query = query.Where(booking => booking.CreatedAt >= fromUtc);
        }

        if (toDate.HasValue)
        {
            var toExclusiveUtc = ConvertLocalDateStartToUtc(toDate.Value.AddDays(1));
            query = query.Where(booking => booking.CreatedAt < toExclusiveUtc);
        }

        return query;
    }

    private static (DateOnly? FromDate, DateOnly? ToDate) NormalizeDateRange(
        DateOnly? fromDate,
        DateOnly? toDate)
    {
        if (fromDate.HasValue
            && toDate.HasValue
            && fromDate.Value > toDate.Value)
        {
            return (toDate, fromDate);
        }

        return (fromDate, toDate);
    }

    private static bool TryParseBookingId(
        string search,
        out long bookingId)
    {
        var normalized = search.Trim();

        if (normalized.StartsWith("#", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..];
        }

        if (normalized.StartsWith("SR-", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[3..];
        }

        return long.TryParse(normalized, out bookingId);
    }

    private static bool IsAllFilter(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "all", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime ConvertLocalDateStartToUtc(DateOnly value)
    {
        return TimeZoneInfo.ConvertTimeToUtc(
            value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
            VietnamTimeZone);
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
    }
}
