using MediatR;
using NetTopologySuite.Geometries;
using SafeRide.Application.Common.Exceptions;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Bookings.Commands.CreateBooking;

public sealed class CreateBookingCommandHandler
    : IRequestHandler<CreateBookingCommand, CreateBookingResponse>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IMapService _mapService;
    private readonly IFareEstimationService _fareEstimationService;
    private readonly IBookingMatchingService _matchingService;

    public CreateBookingCommandHandler(
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IMapService mapService,
        IFareEstimationService fareEstimationService,
        IBookingMatchingService matchingService)
    {
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _mapService = mapService;
        _fareEstimationService = fareEstimationService;
        _matchingService = matchingService;
    }

    public async Task<CreateBookingResponse> Handle(
        CreateBookingCommand request,
        CancellationToken cancellationToken)
    {
        var utcNow = _dateTimeProvider.UtcNow;
        ValidateSchedule(request.BookingType, request.ScheduledAt, utcNow);
        ValidateCoordinates(request);

        var vehicle = await _bookingRepository.GetCustomerVehicleAsync(
            request.VehicleId,
            request.CustomerId,
            cancellationToken);
        if (vehicle is null)
        {
            throw new BookingException(
                "booking.vehicle_not_found",
                "Không tìm thấy xe hợp lệ của bạn.",
                404);
        }

        var pricingRule = await _bookingRepository.GetPricingRuleAsync(
            request.ServiceTypeId,
            request.VehicleId,
            cancellationToken);
        if (pricingRule is null)
        {
            throw new BookingException(
                "booking.pricing_rule_not_found",
                "Không tìm thấy cấu hình giá phù hợp cho dịch vụ và xe đã chọn.",
                400);
        }

        RouteEstimateResult route;
        try
        {
            route = await _mapService.GetRouteEstimateAsync(
                new LocationPoint(request.PickupLatitude, request.PickupLongitude),
                new LocationPoint(request.DestinationLatitude, request.DestinationLongitude),
                cancellationToken);
        }
        catch (MapServiceException exception)
        {
            throw new BookingException(
                "booking.route_estimation_failed",
                exception.Message,
                502);
        }

        var estimatedDistanceKm = decimal.Round(
            (decimal)route.DistanceKm,
            2,
            MidpointRounding.AwayFromZero);
        var estimatedFare = _fareEstimationService.CalculateFare(
            pricingRule,
            estimatedDistanceKm,
            route.DurationMinutes);
        var bookingStatus = request.BookingType == BookingType.Now
            ? BookingStatus.Searching
            : BookingStatus.PendingSchedule;

        var booking = new Booking
        {
            CustomerId = request.CustomerId,
            VehicleId = request.VehicleId,
            ServiceTypeId = request.ServiceTypeId,
            BookingType = request.BookingType,
            BookingStatus = bookingStatus,
            ScheduledAt = request.ScheduledAt,
            PickupAddress = request.PickupAddress.Trim(),
            PickupLocation = CreatePoint(request.PickupLatitude, request.PickupLongitude),
            DestinationAddress = request.DestinationAddress.Trim(),
            DestinationLocation = CreatePoint(
                request.DestinationLatitude,
                request.DestinationLongitude),
            EstimatedDistanceKm = estimatedDistanceKm,
            EstimatedDurationMinutes = route.DurationMinutes,
            EstimatedFare = estimatedFare,
            SpecialRequest = NormalizeOptionalText(request.SpecialRequest),
            PricingRuleId = pricingRule.Id,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        await _bookingRepository.AddAsync(booking, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (request.BookingType == BookingType.Now)
        {
            await _matchingService.StartMatchingAsync(
                booking.BookingId,
                cancellationToken);
        }

        var message = request.BookingType == BookingType.Now
            ? "Đặt chuyến thành công. Hệ thống đang tìm tài xế."
            : "Đặt trước chuyến đi thành công.";

        return new CreateBookingResponse(
            booking.BookingId,
            booking.BookingType,
            booking.BookingStatus,
            booking.ScheduledAt,
            booking.EstimatedFare,
            message);
    }

    private static void ValidateSchedule(
        BookingType bookingType,
        DateTime? scheduledAt,
        DateTime utcNow)
    {
        if (!Enum.IsDefined(bookingType))
        {
            throw new BookingException(
                "booking.invalid_type",
                "Loại đặt chuyến không hợp lệ.",
                400);
        }

        if (bookingType == BookingType.Now && scheduledAt.HasValue)
        {
            throw new BookingException(
                "booking.schedule_not_allowed",
                "Chuyến đi ngay không được có thời gian đặt trước.",
                400);
        }

        if (bookingType == BookingType.Scheduled
            && (!scheduledAt.HasValue || scheduledAt.Value < utcNow.AddMinutes(30)))
        {
            throw new BookingException(
                "booking.invalid_schedule",
                "Thời gian đặt trước phải cách thời điểm hiện tại ít nhất 30 phút.",
                400);
        }

        if (scheduledAt.HasValue && scheduledAt.Value.Kind == DateTimeKind.Local)
        {
            throw new BookingException(
                "booking.schedule_must_be_utc",
                "Thời gian đặt trước phải được gửi theo múi giờ UTC.",
                400);
        }
    }

    private static void ValidateCoordinates(CreateBookingCommand request)
    {
        ValidateCoordinate(
            request.PickupLatitude,
            request.PickupLongitude,
            "Điểm đón");
        ValidateCoordinate(
            request.DestinationLatitude,
            request.DestinationLongitude,
            "Điểm đến");

        if (string.IsNullOrWhiteSpace(request.PickupAddress)
            || string.IsNullOrWhiteSpace(request.DestinationAddress))
        {
            throw new BookingException(
                "booking.address_required",
                "Địa chỉ điểm đón và điểm đến là bắt buộc.",
                400);
        }
    }

    private static void ValidateCoordinate(
        double latitude,
        double longitude,
        string locationName)
    {
        if (!double.IsFinite(latitude)
            || !double.IsFinite(longitude)
            || latitude is < -90 or > 90
            || longitude is < -180 or > 180)
        {
            throw new BookingException(
                "booking.invalid_coordinates",
                $"{locationName} có tọa độ không hợp lệ.",
                400);
        }
    }

    private static Point CreatePoint(double latitude, double longitude)
    {
        return new Point(longitude, latitude) { SRID = 4326 };
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
