using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Microsoft.Extensions.Logging;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Realtime;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.RiskProtection;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class SafetyReportService : ISafetyReportService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAdminReportRealtimeService _realtime;
    private readonly ILogger<SafetyReportService> _logger;

    public SafetyReportService(
        ApplicationDbContext dbContext,
        IAdminReportRealtimeService realtime,
        ILogger<SafetyReportService> logger)
    {
        _dbContext = dbContext;
        _realtime = realtime;
        _logger = logger;
    }

    public async Task<SafetyReportResponse> CreateAsync(
        Guid driverId, long tripId, SafetyReportRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.ReportType)
            || request.ReportType == SafetyReportType.GENERAL || string.IsNullOrWhiteSpace(request.ReasonCode)
            || string.IsNullOrWhiteSpace(request.Description))
            throw new BookingException("safety_report.invalid", "Loại, lý do và nội dung báo cáo an toàn là bắt buộc.", StatusCodes.Status400BadRequest);
        var reasonCode = request.ReasonCode.Trim();
        if (!IsValidReasonCode(request.ReportType, reasonCode))
            throw new BookingException(
                "safety_report.invalid_reason",
                "Lý do báo cáo không phù hợp với loại sự cố an toàn đã chọn.",
                StatusCodes.Status400BadRequest);
        if (request.Latitude.HasValue != request.Longitude.HasValue
            || request.Latitude is < -90m or > 90m
            || request.Longitude is < -180m or > 180m)
            throw new BookingException(
                "safety_report.invalid_location",
                "Vị trí báo cáo an toàn không hợp lệ.",
                StatusCodes.Status400BadRequest);
        if (request.EscalationRequested && request.Latitude is null)
            throw new BookingException(
                "safety_report.escalation_location_required",
                "Cần cung cấp vị trí để kích hoạt luồng SOS.",
                StatusCodes.Status400BadRequest);
        var trip = await _dbContext.Trips.SingleOrDefaultAsync(
            x => x.Id == tripId && x.DriverId == driverId, cancellationToken);
        if (trip is null) throw new BookingException("trip.not_found", "Không tìm thấy chuyến đi.", StatusCodes.Status404NotFound);
        if (trip.TripStatus is TripStatus.COMPLETED or TripStatus.CANCELLED)
            throw new BookingException("safety_report.trip_ended", "Chuyến đi đã kết thúc.", StatusCodes.Status409Conflict);
        var now = DateTime.UtcNow;
        var preTripCheckId = request.ReportType == SafetyReportType.VEHICLE_ISSUE
            ? await _dbContext.PreTripVehicleChecks
                .Where(x => x.TripId == tripId)
                .OrderByDescending(x => x.CheckedAtUtc)
                .ThenByDescending(x => x.Id)
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var report = new Report
        {
            TripId = tripId,
            UserId = driverId,
            Subject = request.ReportType == SafetyReportType.UNSAFE_CUSTOMER
                ? "Driver reported unsafe customer"
                : "Driver reported vehicle safety issue",
            Description = request.Description.Trim(),
            Status = ReportStatus.Pending,
            ReportType = request.ReportType,
            ReasonCode = reasonCode,
            OccurredAtUtc = now,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            EscalationRequested = request.EscalationRequested,
            PreTripVehicleCheckId = preTripCheckId,
            CreatedAt = now
        };
        _dbContext.Reports.Add(report);
        SOSAlert? escalation = null;
        if (request.EscalationRequested)
        {
            if (trip.TripStatus is not (TripStatus.ACCEPTED or TripStatus.ARRIVED or TripStatus.IN_PROGRESS))
                throw new BookingException(
                    "safety_report.escalation_trip_not_active",
                    "Chỉ có thể kích hoạt SOS khi chuyến đi đang diễn ra.",
                    StatusCodes.Status409Conflict);
            escalation = await _dbContext.Sosalerts.SingleOrDefaultAsync(
                x => x.TripId == tripId && x.SOSStatus == SOSStatus.Active,
                cancellationToken);
            if (escalation is null)
            {
                escalation = new SOSAlert
                {
                    TripId = tripId,
                    TriggeredByUserId = driverId,
                    Location = new Point(
                        (double)request.Longitude!.Value,
                        (double)request.Latitude!.Value)
                    {
                        SRID = 4326
                    },
                    EmergencyMessage = $"Safety report {request.ReportType}: {reasonCode}",
                    SOSStatus = SOSStatus.Active,
                    CreatedAt = now
                };
                _dbContext.Sosalerts.Add(escalation);
            }
            trip.IsSOSActivated = true;
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
        try
        {
            await _realtime.PublishReportCreatedAsync(new ReportCreatedEvent(
                report.Id, tripId, driverId, report.Subject, report.Status, now), cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Could not publish safety report refresh hint for report {ReportId}.",
                report.Id);
        }
        return new(
            report.Id,
            tripId,
            report.ReportType,
            report.ReasonCode,
            report.EscalationRequested,
            escalation?.Id,
            now);
    }

    private static bool IsValidReasonCode(SafetyReportType reportType, string reasonCode) =>
        reportType switch
        {
            SafetyReportType.UNSAFE_CUSTOMER => Enum.GetNames<UnsafeCustomerReason>()
                .Contains(reasonCode, StringComparer.Ordinal),
            SafetyReportType.VEHICLE_ISSUE => Enum.GetNames<VehicleFaultType>()
                .Contains(reasonCode, StringComparer.Ordinal),
            _ => false
        };
}
