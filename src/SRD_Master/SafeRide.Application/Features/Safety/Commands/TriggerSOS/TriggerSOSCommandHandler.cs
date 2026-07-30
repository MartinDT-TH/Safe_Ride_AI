using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Realtime;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Safety.Commands.TriggerSOS;

public sealed class TriggerSOSCommandHandler
    : IRequestHandler<TriggerSOSCommand, TriggerSOSResponse>
{
    private static readonly HashSet<TripStatus> ValidTripStatuses =
    [
        TripStatus.ACCEPTED,
        TripStatus.ARRIVED,
        TripStatus.IN_PROGRESS
    ];

    private readonly ISOSAlertRepository _sosAlertRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IRealtimeNotificationService _realtimeNotificationService;

    public TriggerSOSCommandHandler(
        ISOSAlertRepository sosAlertRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IRealtimeNotificationService realtimeNotificationService)
    {
        _sosAlertRepository = sosAlertRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _realtimeNotificationService = realtimeNotificationService;
    }

    public async Task<TriggerSOSResponse> Handle(
        TriggerSOSCommand request,
        CancellationToken cancellationToken)
    {
        ValidateLocation(request.Latitude, request.Longitude);

        var trip = await _sosAlertRepository.GetTripForSOSAsync(
            request.TripId,
            cancellationToken);
        if (trip is null)
        {
            throw new SafetyException(
                "sos.trip_not_found",
                "Không tìm thấy chuyến đi.",
                404);
        }

        ValidateCustomerOwnsTrip(trip, request.CustomerId);
        ValidateTripCanTriggerSOS(trip);

        var activeAlert = await _sosAlertRepository.GetActiveAlertByTripIdAsync(
            trip.Id,
            cancellationToken);
        if (trip.IsSOSActivated || activeAlert is not null)
        {
            throw new SafetyException(
                "sos.already_active",
                "SOS đã được kích hoạt trước đó.",
                409);
        }

        var utcNow = _dateTimeProvider.UtcNow;
        var sosAlert = new SOSAlert
        {
            TripId = trip.Id,
            TriggeredByUserId = request.CustomerId,
            Location = new NetTopologySuite.Geometries.Point(
                request.Longitude,
                request.Latitude)
            {
                SRID = 4326
            },
            EmergencyMessage = NormalizeMessage(request.Message),
            SOSStatus = SOSStatus.Active,
            CreatedAt = utcNow
        };

        trip.IsSOSActivated = true;

        await _sosAlertRepository.AddAsync(sosAlert, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _realtimeNotificationService.PublishSOSTriggeredAsync(
            new SOSTriggeredEvent(
                sosAlert.Id,
                trip.Id,
                trip.BookingId,
                request.CustomerId,
                trip.DriverId,
                request.Latitude,
                request.Longitude,
                sosAlert.EmergencyMessage,
                sosAlert.SOSStatus,
                utcNow,
                "Khách hàng đã kích hoạt SOS."),
            cancellationToken);

        return new TriggerSOSResponse(
            sosAlert.Id,
            trip.Id,
            sosAlert.SOSStatus,
            "SOS đã được kích hoạt",
            utcNow);
    }

    private static void ValidateLocation(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            throw new SafetyException(
                "sos.invalid_location",
                "Vị trí SOS không hợp lệ.",
                400);
        }
    }

    private static void ValidateCustomerOwnsTrip(Trip trip, Guid customerId)
    {
        if (trip.Booking.CustomerId != customerId)
        {
            throw new SafetyException(
                "sos.forbidden",
                "Bạn không có quyền kích hoạt SOS cho chuyến đi này.",
                403);
        }
    }

    private static void ValidateTripCanTriggerSOS(Trip trip)
    {
        if (!ValidTripStatuses.Contains(trip.TripStatus))
        {
            throw new SafetyException(
                "sos.trip_not_active",
                "Chỉ có thể kích hoạt SOS khi chuyến đi đang diễn ra.",
                400);
        }
    }

    private static string? NormalizeMessage(string? message)
    {
        var normalized = message?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : normalized;
    }
}
