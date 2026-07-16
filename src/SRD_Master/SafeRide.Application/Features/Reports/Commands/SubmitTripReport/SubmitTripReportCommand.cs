using MediatR;
using SafeRide.Contracts.Responses.Feedbacks;

namespace SafeRide.Application.Features.Reports.Commands.SubmitTripReport;

public sealed record SubmitTripReportCommand(
    long TripId,
    Guid CustomerId,
    string Subject,
    string Description)
    : IRequest<SubmitTripReportResponse>;
