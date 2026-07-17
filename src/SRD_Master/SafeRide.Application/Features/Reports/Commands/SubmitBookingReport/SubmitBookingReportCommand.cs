using MediatR;
using SafeRide.Contracts.Responses.Feedbacks;

namespace SafeRide.Application.Features.Reports.Commands.SubmitBookingReport;

public sealed record SubmitBookingReportCommand(
    long BookingId,
    Guid CustomerId,
    string Subject,
    string Description)
    : IRequest<SubmitTripReportResponse>;
