using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Contracts.Responses.Feedbacks;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Reports.Commands.SubmitBookingReport;

public sealed class SubmitBookingReportCommandHandler
    : IRequestHandler<SubmitBookingReportCommand, SubmitTripReportResponse>
{
    private readonly IReportRepository _reportRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SubmitBookingReportCommandHandler(
        IReportRepository reportRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _reportRepository = reportRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<SubmitTripReportResponse> Handle(
        SubmitBookingReportCommand request,
        CancellationToken cancellationToken)
    {
        var subject = NormalizeRequired(
            request.Subject,
            "report.subject_required",
            "Vui lòng nhập tiêu đề báo cáo.");
        var description = NormalizeRequired(
            request.Description,
            "report.description_required",
            "Vui lòng nhập nội dung báo cáo.");

        var booking = await _reportRepository.GetBookingForReportAsync(
            request.BookingId,
            cancellationToken);
        if (booking is null)
        {
            throw new ReportException(
                "report.booking_not_found",
                "Không tìm thấy yêu cầu đặt chuyến.",
                404);
        }

        ValidateCustomerOwnsBooking(booking, request.CustomerId);

        var trip = booking.Trip;
        if (trip is null)
        {
            throw new ReportException(
                "report.trip_not_found",
                "Không tìm thấy chuyến đi.",
                404);
        }

        ValidateTripCanBeReported(trip);
        await ValidateReportDoesNotExistAsync(
            trip.Id,
            request.CustomerId,
            cancellationToken);

        var utcNow = _dateTimeProvider.UtcNow;
        var report = new Report
        {
            TripId = trip.Id,
            UserId = request.CustomerId,
            Subject = subject,
            Description = description,
            Status = ReportStatus.Pending,
            CreatedAt = utcNow
        };

        await _reportRepository.AddReportAsync(report, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SubmitTripReportResponse(
            report.Id,
            trip.Id,
            subject,
            description,
            report.Status,
            utcNow,
            "Đã gửi báo cáo chuyến đi.");
    }

    private static string NormalizeRequired(
        string value,
        string code,
        string message)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ReportException(code, message, 400);
        }

        return normalized;
    }

    private static void ValidateCustomerOwnsBooking(Booking booking, Guid customerId)
    {
        if (booking.CustomerId != customerId)
        {
            throw new ReportException(
                "report.forbidden",
                "Bạn không có quyền báo cáo chuyến đi này.",
                403);
        }
    }

    private static void ValidateTripCanBeReported(Trip trip)
    {
        if (trip.TripStatus is not (TripStatus.COMPLETED or TripStatus.CANCELLED))
        {
            throw new ReportException(
                "report.trip_not_reportable",
                "Chỉ có thể báo cáo chuyến đi đã hoàn thành hoặc đã hủy.",
                409);
        }
    }

    private async Task ValidateReportDoesNotExistAsync(
        long tripId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var alreadyReported = await _reportRepository.ExistsByTripAndUserAsync(
            tripId,
            customerId,
            cancellationToken);
        if (alreadyReported)
        {
            throw new ReportException(
                "report.already_exists",
                "Bạn đã báo cáo chuyến đi này rồi.",
                409);
        }
    }
}
