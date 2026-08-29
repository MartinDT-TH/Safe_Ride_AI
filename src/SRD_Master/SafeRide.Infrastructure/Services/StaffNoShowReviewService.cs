using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.StaffNoShowReviews;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class StaffNoShowReviewService : IStaffNoShowReviewService
{
    private readonly ApplicationDbContext _db;
    private readonly ICustomerBookingPrivilegeService _privileges;

    public StaffNoShowReviewService(ApplicationDbContext db, ICustomerBookingPrivilegeService privileges)
    { _db = db; _privileges = privileges; }

    public async Task<CustomerNoShowReviewList> ListAsync(CustomerNoShowReviewListFilter filter, CancellationToken ct)
    {
        var page = Math.Max(1, filter.Page);
        var size = Math.Clamp(filter.PageSize <= 0 ? 20 : filter.PageSize, 1, 100);
        var query = _db.CustomerBehaviorEvents.AsNoTracking();
        if (filter.CustomerId.HasValue) query = query.Where(x => x.CustomerId == filter.CustomerId.Value);
        if (filter.Status.HasValue) query = query.Where(x => x.Status == filter.Status.Value);
        if (filter.From.HasValue) query = query.Where(x => x.CreatedAt >= filter.From.Value);
        if (filter.To.HasValue) query = query.Where(x => x.CreatedAt <= filter.To.Value);
        var total = await query.CountAsync(ct);
        var totalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)size);
        page = Math.Min(page, totalPages);
        var events = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * size).Take(size)
            .Include(x => x.Customer).Include(x => x.Driver).ThenInclude(x => x.Driver)
            .ToListAsync(ct);
        return new CustomerNoShowReviewList(events.Select(Map).ToList(), page, size, total, totalPages);
    }

    public async Task<CustomerNoShowReviewDetail> GetAsync(long eventId, CancellationToken ct)
    {
        var item = await Load(eventId, ct) ?? throw new BookingException("staff.no_show_event_not_found", "Không tìm thấy sự kiện khách không xuất hiện.", StatusCodes.Status404NotFound);
        return await BuildDetail(item, ct);
    }

    public async Task<CustomerNoShowReviewDetail> ExemptAsync(long eventId, Guid reviewerId, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new BookingException("staff.no_show_review_reason_required", "Vui lòng nhập lý do miễn trừ.", StatusCodes.Status400BadRequest);
        var item = await Load(eventId, ct) ?? throw new BookingException("staff.no_show_event_not_found", "Không tìm thấy sự kiện khách không xuất hiện.", StatusCodes.Status404NotFound);
        if (item.Status == CustomerBehaviorEventStatus.REVERSED) throw new BookingException("staff.no_show_event_reversed", "Sự kiện đã bị đảo ngược và không thể miễn trừ.", StatusCodes.Status409Conflict);
        if (item.Status != CustomerBehaviorEventStatus.EXEMPTED)
        {
            item.EventType = CustomerBehaviorEventType.EXEMPTED_NO_SHOW;
            item.Status = CustomerBehaviorEventStatus.EXEMPTED;
            item.ExemptedAt = DateTime.UtcNow;
            item.ReviewedByUserId = reviewerId;
            item.ReviewReason = reason.Trim();
            item.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        await _privileges.RecalculateAsync(item.CustomerId, ct);
        return await BuildDetail(item, ct);
    }

    public async Task<CustomerBookingPrivilegeSummary> ClearRestrictionsAsync(Guid customerId, Guid reviewerId, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new BookingException("staff.booking_privilege_reason_required", "Vui lòng nhập lý do gỡ hạn chế.", StatusCodes.Status400BadRequest);
        var customerExists = await _db.Users.AnyAsync(x => x.Id == customerId, ct);
        if (!customerExists) throw new BookingException("staff.customer_not_found", "Không tìm thấy khách hàng.", StatusCodes.Status404NotFound);
        return Map(await _privileges.ClearRestrictionsAsync(customerId, ct));
    }

    public async Task<CustomerBookingPrivilegeSummary> GetPrivilegeAsync(Guid customerId, CancellationToken ct)
    {
        if (!await _db.Users.AnyAsync(x => x.Id == customerId, ct)) throw new BookingException("staff.customer_not_found", "Không tìm thấy khách hàng.", StatusCodes.Status404NotFound);
        return Map(await _privileges.RecalculateAsync(customerId, ct));
    }

    private Task<CustomerBehaviorEvent?> Load(long id, CancellationToken ct) => _db.CustomerBehaviorEvents.Include(x => x.Customer).Include(x => x.Driver).ThenInclude(x => x.Driver).SingleOrDefaultAsync(x => x.Id == id, ct);
    private async Task<CustomerNoShowReviewDetail> BuildDetail(CustomerBehaviorEvent e, CancellationToken ct)
    {
        var supports = await _db.DriverNoShowSupports.AsNoTracking().Where(x => x.CustomerBehaviorEventId == e.Id).Select(x => new DriverNoShowSupportSummary(x.Id, x.AcceptedPickupDistanceKm, x.SupportAmount, x.Status, x.CreatedAt, x.PaidAt)).ToListAsync(ct);
        var privilege = await _db.CustomerBookingPrivileges.AsNoTracking().SingleOrDefaultAsync(x => x.CustomerId == e.CustomerId, ct);
        return new CustomerNoShowReviewDetail(Map(e), supports, privilege is null ? null : Map(privilege));
    }
    private static CustomerNoShowReviewItem Map(CustomerBehaviorEvent x) => new(x.Id, x.CustomerId, x.Customer.FullName ?? x.Customer.Email ?? x.Customer.Id.ToString(), x.Customer.PhoneNumber, x.BookingId, x.TripId, x.DriverId, x.Driver.Driver.FullName ?? x.Driver.Driver.Email ?? x.DriverId.ToString(), x.Driver.Driver.PhoneNumber, x.EventType, x.Status, x.DriverReportedAt, x.ArrivedAt, x.ArrivalDistanceMeters, x.ReminderSentAt, x.WaitSatisfiedAt, x.VerifiedAt, x.ExemptedAt, x.ReviewedByUserId, x.ReviewReason, x.CreatedAt);
    private static CustomerBookingPrivilegeSummary Map(CustomerBookingPrivilege x) => new(x.CustomerId, x.RestrictionLevel, x.ScheduledBookingAllowed, x.ScheduledRestrictedUntil, x.InstantBookingAllowed, x.BookingCooldownUntil, x.VerifiedNoShowCount, x.EligibleBookingCount, x.NoShowRate, x.ConsecutiveNoShowStreak, x.LastNoShowAt, x.UnderStaffReview, x.UpdatedAt);
}
