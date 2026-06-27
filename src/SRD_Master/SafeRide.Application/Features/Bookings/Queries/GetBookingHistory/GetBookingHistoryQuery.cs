using MediatR;
using SafeRide.Application.Features.Bookings.DTOs;

namespace SafeRide.Application.Features.Bookings.Queries.GetBookingHistory;

public enum BookingHistoryRole
{
    Customer,
    Driver
}

public sealed record GetBookingHistoryQuery(
    Guid UserId,
    BookingHistoryRole Role)
    : IRequest<IReadOnlyList<BookingHistoryItemDto>>;
