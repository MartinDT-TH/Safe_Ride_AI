using MediatR;

namespace SafeRide.Application.Features.StaffPayments.Queries.GetStaffPaymentStatuses;

public sealed record GetStaffPaymentStatusesQuery(
    int Page,
    int PageSize,
    string? Status,
    string? Method,
    DateOnly? Date) : IRequest<StaffPaymentStatusPagedResult>;
