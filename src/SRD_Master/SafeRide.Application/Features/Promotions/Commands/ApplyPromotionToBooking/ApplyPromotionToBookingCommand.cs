using MediatR;
using SafeRide.Contracts.Responses.Promotions;

namespace SafeRide.Application.Features.Promotions.Commands.ApplyPromotionToBooking;

public sealed record ApplyPromotionToBookingCommand(
    Guid CustomerId,
    long BookingId,
    string PromotionCode)
    : IRequest<ApplyPromotionToBookingResponse>;
