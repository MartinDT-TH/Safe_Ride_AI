using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings.DTOs;

namespace SafeRide.Application.Features.Bookings.Queries.GetBookingDetails;

public sealed class GetActiveBookingQueryHandler
    : IRequestHandler<GetActiveBookingQuery, BookingDetailsDto?>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetActiveBookingQueryHandler(
        IBookingRepository bookingRepository,
        IDateTimeProvider dateTimeProvider)
    {
        _bookingRepository = bookingRepository;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<BookingDetailsDto?> Handle(
        GetActiveBookingQuery request,
        CancellationToken cancellationToken)
    {
        await _bookingRepository.ExpireStaleNowBookingsAsync(
            request.CustomerId,
            _dateTimeProvider.UtcNow,
            cancellationToken);

        var booking = await _bookingRepository.GetActiveNowBookingAsync(
            request.CustomerId,
            cancellationToken);
        if (booking is null)
        {
            return null;
        }

        return await BookingDetailsMapper.ToDtoAsync(
            booking,
            _bookingRepository,
            cancellationToken);
    }
}
