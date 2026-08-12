using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.StaffPayments;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class StaffPaymentStatusService : IStaffPaymentStatusService
{
    private readonly ApplicationDbContext _db;

    public StaffPaymentStatusService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<StaffPaymentStatusPagedResult> GetPaymentStatusesAsync(
        StaffPaymentStatusListFilter filter,
        CancellationToken cancellationToken)
    {
        var page = filter.Page <= 0 ? 1 : filter.Page;
        var pageSize = filter.PageSize <= 0 ? 10 : Math.Min(filter.PageSize, 100);

        var baseQuery = _db.Payments.AsNoTracking();
        baseQuery = ApplyMethodFilter(baseQuery, filter.Method);
        baseQuery = ApplyDateFilter(baseQuery, filter.Date);

        var counts = new StaffPaymentStatusCountsResponse(
            await baseQuery.CountAsync(cancellationToken),
            await baseQuery.CountAsync(x => x.PaymentStatus == PaymentStatus.Pending, cancellationToken),
            await baseQuery.CountAsync(x => x.PaymentStatus == PaymentStatus.Success, cancellationToken),
            await baseQuery.CountAsync(x => x.PaymentStatus == PaymentStatus.Failed, cancellationToken),
            await baseQuery.CountAsync(x => x.PaymentStatus == PaymentStatus.Cancelled, cancellationToken));

        var filteredQuery = ApplyStatusFilter(baseQuery, filter.Status);
        var totalItems = await filteredQuery.CountAsync(cancellationToken);
        var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)pageSize);
        var currentPage = Math.Min(page, totalPages);

        var items = await filteredQuery
            .OrderByDescending(x => x.PaidAt ?? x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new StaffPaymentStatusResponse(
                x.Id,
                x.TripId,
                x.Trip.BookingId,
                x.Trip.Booking.Customer.FullName ?? "Khach hang SafeRide",
                MaskPhone(x.Trip.Booking.Customer.PhoneNumber),
                x.Amount,
                x.Currency,
                x.PaymentMethod,
                x.PaymentStatus,
                x.PaidAt ?? x.CreatedAt))
            .ToListAsync(cancellationToken);

        return new StaffPaymentStatusPagedResult(
            counts,
            items,
            currentPage,
            pageSize,
            totalItems,
            totalPages);
    }

    private static IQueryable<Domain.Entities.Payment> ApplyStatusFilter(
        IQueryable<Domain.Entities.Payment> query,
        string? status)
    {
        if (IsAllFilter(status))
        {
            return query;
        }

        if (!Enum.TryParse<PaymentStatus>(status, true, out var parsedStatus))
        {
            return query.Where(_ => false);
        }

        return query.Where(x => x.PaymentStatus == parsedStatus);
    }

    private static IQueryable<Domain.Entities.Payment> ApplyMethodFilter(
        IQueryable<Domain.Entities.Payment> query,
        string? method)
    {
        if (IsAllFilter(method))
        {
            return query;
        }

        if (!Enum.TryParse<PaymentMethod>(method, true, out var parsedMethod))
        {
            return query.Where(_ => false);
        }

        return query.Where(x => x.PaymentMethod == parsedMethod);
    }

    private static IQueryable<Domain.Entities.Payment> ApplyDateFilter(
        IQueryable<Domain.Entities.Payment> query,
        DateOnly? date)
    {
        if (!date.HasValue)
        {
            return query;
        }

        var start = date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = start.AddDays(1);
        return query.Where(x => x.CreatedAt >= start && x.CreatedAt < end);
    }

    private static bool IsAllFilter(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "all", StringComparison.OrdinalIgnoreCase);
    }

    private static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length < 7)
        {
            return "-";
        }

        return $"{phone[..3]}***{phone[^4..]}";
    }
}
