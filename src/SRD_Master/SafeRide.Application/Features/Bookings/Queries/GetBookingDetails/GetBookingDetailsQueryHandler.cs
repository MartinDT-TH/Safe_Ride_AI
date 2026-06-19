using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings.DTOs;

namespace SafeRide.Application.Features.Bookings.Queries.GetBookingDetails;

public sealed class GetBookingDetailsQueryHandler
    : IRequestHandler<GetBookingDetailsQuery, BookingDetailsDto>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IGoogleMapsService _googleMapsService;

    public GetBookingDetailsQueryHandler(
        IBookingRepository bookingRepository,
        IDateTimeProvider dateTimeProvider,
        IGoogleMapsService googleMapsService)
    {
        _bookingRepository = bookingRepository;
        _dateTimeProvider = dateTimeProvider;
        _googleMapsService = googleMapsService;
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
            _googleMapsService,
            cancellationToken);
    }
}
