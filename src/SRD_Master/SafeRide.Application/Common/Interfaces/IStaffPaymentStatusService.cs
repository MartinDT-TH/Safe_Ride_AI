using SafeRide.Application.Features.StaffPayments;

namespace SafeRide.Application.Common.Interfaces;

public interface IStaffPaymentStatusService
{
    Task<StaffPaymentStatusPagedResult> GetPaymentStatusesAsync(
        StaffPaymentStatusListFilter filter,
        CancellationToken cancellationToken);
}
