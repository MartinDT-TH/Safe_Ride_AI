using SafeRide.Domain.Enums;

namespace SafeRide.Contracts.Responses.Feedbacks;

public sealed record SubmitTripReportResponse(
    long ReportId,
    long TripId,
    string Subject,
    string Description,
    ReportStatus Status,
    DateTime CreatedAt,
    string Message);
