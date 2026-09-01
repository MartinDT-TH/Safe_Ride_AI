using SafeRide.Application.Features.StaffNoShowReviews;

namespace SafeRide.Application.Common.Interfaces;

public interface IStaffNoShowReviewService
{
    Task<CustomerNoShowReviewList> ListAsync(CustomerNoShowReviewListFilter filter, CancellationToken cancellationToken);
    Task<CustomerNoShowReviewDetail> GetAsync(long eventId, CancellationToken cancellationToken);
    Task<CustomerNoShowReviewDetail> ExemptAsync(long eventId, Guid reviewerId, string reason, CancellationToken cancellationToken);
    Task<CustomerBookingPrivilegeSummary> ClearRestrictionsAsync(Guid customerId, Guid reviewerId, string reason, CancellationToken cancellationToken);
    Task<CustomerBookingPrivilegeSummary> GetPrivilegeAsync(Guid customerId, CancellationToken cancellationToken);
}
