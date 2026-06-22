using MediatR;
using SafeRide.Application.Common.Exceptions;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;


namespace SafeRide.Application.Features.Bookings.Queries.EstimateBookingFare;

public sealed class EstimateBookingFareQueryHandler
    : IRequestHandler<EstimateBookingFareQuery, EstimateBookingFareResult>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IMapRoutingService _mapRoutingService;
    private readonly IFareEstimationService _fareEstimationService;
    private readonly IVehicleLicenseRequirementService _vehicleLicenseRequirementService;

    public EstimateBookingFareQueryHandler(
        IBookingRepository bookingRepository,
        IMapRoutingService mapRoutingService,
        IFareEstimationService fareEstimationService,
        IVehicleLicenseRequirementService vehicleLicenseRequirementService)
    {
        _bookingRepository = bookingRepository;
        _mapRoutingService = mapRoutingService;
        _fareEstimationService = fareEstimationService;
        _vehicleLicenseRequirementService = vehicleLicenseRequirementService;
    }

    public async Task<EstimateBookingFareResult> Handle(
        EstimateBookingFareQuery request,
        CancellationToken cancellationToken)
    {
        ValidatePickupCoordinate(request);

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

        ValidateVehicleLicenseRequirement(vehicle);

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

        if (pricingRule.PricePerHour.HasValue)
        {
            var estimatedHours = request.EstimatedHours;
            if (!estimatedHours.HasValue || estimatedHours is < 1 or > 24)
            {
                throw new BookingException(
                    "booking.invalid_estimated_hours",
                    "Số giờ thuê dự kiến phải từ 1 đến 24 giờ.",
                    400);
            }

            var durationMinutes = estimatedHours.Value * 60;
            var hourlyEstimatedFare = _fareEstimationService.CalculateFare(
                pricingRule,
                0m,
                durationMinutes);

            return new EstimateBookingFareResult(
                0,
                durationMinutes,
                null,
                hourlyEstimatedFare);
        }

        ValidateDestinationCoordinates(request);

        RouteEstimateResult route;
        try
        {
            route = await _mapRoutingService.GetRouteEstimateAsync(
                new RouteEstimateRequest
                {
                    Origin = new LocationPoint(
                        request.PickupLatitude,
                        request.PickupLongitude),
                    Destination = new LocationPoint(
                        request.DestinationLatitude,
                        request.DestinationLongitude),
                    Provider = MapProvider.Auto,
                    TravelMode = MapTravelMode.Car,
                    IncludePolyline = true,
                    RequestSource = "EstimateFare"
                },
                cancellationToken);
        }
        catch (MapServiceException exception)
        {
            throw new BookingException(
                "booking.route_estimation_failed",
                exception.Message,
                502);
        }

        if (string.IsNullOrWhiteSpace(route.EncodedPolyline))
        {
            throw new BookingException(
                "booking.route_polyline_missing",
                "Không thể tính tuyến đường. Vui lòng kiểm tra lại điểm đón và điểm đến.",
                502);
        }

        var distanceKm = decimal.Round(
            (decimal)route.DistanceKm,
            2,
            MidpointRounding.AwayFromZero);
        var estimatedFare = _fareEstimationService.CalculateFare(
            pricingRule,
            distanceKm,
            route.DurationMinutes);

        return new EstimateBookingFareResult(
            (double)distanceKm,
            route.DurationMinutes,
            route.EncodedPolyline,
            estimatedFare);
    }

    private static void ValidatePickupCoordinate(EstimateBookingFareQuery request)
    {
        ValidateCoordinate(request.PickupLatitude, request.PickupLongitude);
    }

    private static void ValidateDestinationCoordinates(EstimateBookingFareQuery request)
    {
        ValidateCoordinate(
            request.DestinationLatitude,
            request.DestinationLongitude);

        if (request.PickupLatitude == request.DestinationLatitude
            && request.PickupLongitude == request.DestinationLongitude)
        {
            throw new BookingException(
                "booking.same_locations",
                "Điểm đón và điểm đến phải khác nhau.",
                400);
        }
    }

    private void ValidateVehicleLicenseRequirement(Vehicle vehicle)
    {
        if (_vehicleLicenseRequirementService.HasValidRequirement(vehicle))
        {
            return;
        }

        throw new BookingException(
            "booking.invalid_vehicle_license_requirement",
            "Không xác định được hạng bằng lái cần thiết cho xe đã chọn.",
            400);
    }

    private static void ValidateCoordinate(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude)
            || !double.IsFinite(longitude)
            || latitude is < -90 or > 90
            || longitude is < -180 or > 180)
        {
            throw new BookingException(
                "booking.invalid_coordinates",
                "Tọa độ điểm đón hoặc điểm đến không hợp lệ.",
                400);
        }
    }
}
