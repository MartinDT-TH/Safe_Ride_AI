using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.StaffNoShowReviews;

public sealed record CustomerNoShowReviewListFilter(
    Guid? CustomerId,
    CustomerBehaviorEventStatus? Status,
    DateTime? From,
    DateTime? To,
    int Page,
    int PageSize);

public sealed record CustomerNoShowReviewItem(
    long EventId, Guid CustomerId, string CustomerName, string? CustomerPhone,
    long BookingId, long TripId, Guid DriverId, string DriverName, string? DriverPhone,
    CustomerBehaviorEventType EventType, CustomerBehaviorEventStatus Status,
    DateTime? DriverReportedAt, DateTime? ArrivedAt, decimal? ArrivalDistanceMeters,
    DateTime? ReminderSentAt, DateTime? WaitSatisfiedAt, DateTime? VerifiedAt,
    DateTime? ExemptedAt, Guid? ReviewedByUserId, string? ReviewReason, DateTime CreatedAt);

public sealed record CustomerNoShowReviewList(
    IReadOnlyCollection<CustomerNoShowReviewItem> Items, int Page, int PageSize, int TotalItems, int TotalPages);

public sealed record CustomerNoShowReviewDetail(
    CustomerNoShowReviewItem Event,
    IReadOnlyCollection<DriverNoShowSupportSummary> Supports,
    CustomerBookingPrivilegeSummary? Privilege);

public sealed record DriverNoShowSupportSummary(long Id, decimal AcceptedPickupDistanceKm, decimal SupportAmount, DriverNoShowSupportStatus Status, DateTime CreatedAt, DateTime? PaidAt);

public sealed record CustomerBookingPrivilegeSummary(
    Guid CustomerId, CustomerBehaviorRestrictionLevel RestrictionLevel, bool ScheduledBookingAllowed,
    DateTime? ScheduledRestrictedUntil, bool InstantBookingAllowed, DateTime? BookingCooldownUntil,
    int VerifiedNoShowCount, int EligibleBookingCount, decimal NoShowRate, int ConsecutiveNoShowStreak,
    DateTime? LastNoShowAt, bool UnderStaffReview, DateTime UpdatedAt);

public sealed record ExemptCustomerNoShowRequest(string Reason);
public sealed record ClearCustomerBookingRestrictionRequest(string Reason);
