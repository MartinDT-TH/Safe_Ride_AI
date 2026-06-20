using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Bookings.Queries.GetBookingCatalog;

public sealed class GetBookingCatalogQueryHandler
    : IRequestHandler<GetBookingCatalogQuery, GetBookingCatalogResult>
{
    private const string PerTripMode = "perTrip";
    private const string HourlyMode = "hourly";

    private readonly IBookingRepository _bookingRepository;

    public GetBookingCatalogQueryHandler(IBookingRepository bookingRepository)
    {
        _bookingRepository = bookingRepository;
    }

    public async Task<GetBookingCatalogResult> Handle(
        GetBookingCatalogQuery request,
        CancellationToken cancellationToken)
    {
        var vehicles = await _bookingRepository.GetCustomerVehiclesAsync(
            request.CustomerId,
            cancellationToken);
        var pricingRules = await _bookingRepository.GetBookablePricingRulesAsync(
            request.CustomerId,
            cancellationToken);

        if (pricingRules.Count == 0)
        {
            throw new BookingException(
                "booking.no_available_services",
                "Hiện chưa có dịch vụ nào khả dụng cho các loại xe của bạn.",
                404);
        }

        var services = pricingRules
            .GroupBy(rule => rule.ServiceTypeId)
            .Select(group =>
            {
                var first = group
                    .OrderByDescending(rule => rule.CreatedAt)
                    .First();
                var mode = group.Any(rule => rule.PricePerHour.HasValue)
                    ? HourlyMode
                    : PerTripMode;

                return new BookingServiceOptionResult(
                    first.ServiceTypeId,
                    first.ServiceType.ServiceName,
                    mode,
                    CreateServiceDescription(mode));
            })
            .OrderBy(service => service.Id)
            .ToList();

        if (vehicles.Count == 0)
        {
            throw new BookingException(
                "booking.no_registered_vehicles",
                "Bạn chưa đăng ký phương tiện nào. Vui lòng đăng ký xe để bắt đầu đặt chuyến.",
                404);
        }

        var vehicleOptions = vehicles
            .Select(vehicle => new BookingVehicleOptionResult(
                vehicle.Id,
                vehicle.BrandModel,
                vehicle.PlateNumber,
                vehicle.Color ?? string.Empty,
                vehicle.VehicleType == VehicleType.Motorbike))
            .ToList();

        return new GetBookingCatalogResult(services, vehicleOptions);
    }

    private static string CreateServiceDescription(string mode)
    {
        return mode == HourlyMode
            ? "Tính giá theo thời gian dự kiến"
            : "Tính giá theo quãng đường";
    }
}
