using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings.DTOs;

namespace SafeRide.Application.Features.Bookings.Queries.GetBookingDetails;

public sealed class GetBookingDetailsQueryHandler
    : IRequestHandler<GetBookingDetailsQuery, BookingDetailsDto>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IMapRoutingService _mapRoutingService;
    private readonly IMatchingPolicyProvider _matchingPolicyProvider;

    public GetBookingDetailsQueryHandler(
        IBookingRepository bookingRepository,
        IDateTimeProvider dateTimeProvider,
        IMapRoutingService mapRoutingService,
        IMatchingPolicyProvider matchingPolicyProvider)
    {
        _bookingRepository = bookingRepository;
        _dateTimeProvider = dateTimeProvider;
        _mapRoutingService = mapRoutingService;
        _matchingPolicyProvider = matchingPolicyProvider;
    }

    public async Task<BookingDetailsDto> Handle(
        GetBookingDetailsQuery request,
        CancellationToken cancellationToken)
    {
        await _bookingRepository.ExpireStaleNowBookingsAsync(
            request.CustomerId,
            _dateTimeProvider.UtcNow,
            cancellationToken);

        var booking = await _bookingRepository.GetCustomerBookingWithDetailsAsync(
            request.BookingId,
            request.CustomerId,
            cancellationToken);
        if (booking is null)
        {
            throw new BookingException(
                "booking.not_found",
                "Không tìm thấy chuyến của bạn.",
                404);
        }

        return await BookingDetailsMapper.ToDtoAsync(
            booking,
            _bookingRepository,
            _mapRoutingService,
            _matchingPolicyProvider,
            _dateTimeProvider.UtcNow,
            cancellationToken);
    }
}
