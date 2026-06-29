using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings.DTOs;

namespace SafeRide.Application.Features.Bookings.Queries.GetBookingHistory;

public sealed class GetBookingHistoryQueryHandler
    : IRequestHandler<GetBookingHistoryQuery, IReadOnlyList<BookingHistoryItemDto>>
{
    private readonly IBookingRepository _bookingRepository;

    public GetBookingHistoryQueryHandler(IBookingRepository bookingRepository)
    {
        _bookingRepository = bookingRepository;
    }

    public Task<IReadOnlyList<BookingHistoryItemDto>> Handle(
        GetBookingHistoryQuery request,
        CancellationToken cancellationToken)
    {
        return request.Role == BookingHistoryRole.Driver
            ? _bookingRepository.GetDriverBookingHistoryAsync(
                request.UserId,
                cancellationToken)
            : _bookingRepository.GetCustomerBookingHistoryAsync(
                request.UserId,
                cancellationToken);
    }
}
