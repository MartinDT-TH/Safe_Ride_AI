using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Contracts.Responses.MobileConfig;
using SafeRide.Domain.Enums;

namespace SafeRide.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/mobile-config")]
public sealed class MobileConfigController : ControllerBase
{
    private const string ConfigVersion = "2026.06.30";
    private const string RealtimeHubPath = "/hubs/saferide";
    private const int DriverLocationUpdateIntervalSeconds = 3;
    private const int SearchingBookingPollIntervalSeconds = 3;
    private const int NearbyDriversRefreshIntervalSeconds = 5;
    private const int TripStatusPollIntervalSeconds = 4;

    private readonly IConfiguration _configuration;

    public MobileConfigController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    [ProducesResponseType<MobileConfigResponse>(StatusCodes.Status200OK)]
    public ActionResult<MobileConfigResponse> Get()
    {
        var primaryMapProvider = _configuration["MapServices:PrimaryProvider"];
        if (string.IsNullOrWhiteSpace(primaryMapProvider))
        {
            primaryMapProvider = "VietMap";
        }

        return Ok(new MobileConfigResponse(
            ConfigVersion,
            new MobileRealtimeConfigResponse(
                RealtimeHubPath,
                new MobileRealtimeEventsResponse(
                    "BookingSearchingStarted",
                    "BookingSearchRadiusExpanded",
                    "BookingStatusChanged",
                    "BookingDriverAssigned",
                    "BookingExpired",
                    "BookingCancelled",
                    "DriverMatched",
                    "DriverLocationUpdated",
                    "DriverOfferCreated",
                    "ReceiveDriverOffer",
                    "DriverOfferAccepted",
                    "DriverOfferRejected",
                    "DriverOfferExpired",
                    "DriverOfferCancelled",
                    "CustomerConfirmedDriverOffer",
                    "TripCreated",
                    "TripStatusChanged")),
            new MobileStatusGroupResponse(
            [
                Status(BookingStatus.PendingSchedule, "Đã đặt lịch"),
                Status(BookingStatus.Searching, "Đang tìm tài xế"),
                Status(BookingStatus.DriverAssigned, "Đã có tài xế"),
                Status(BookingStatus.Cancelled, "Đã hủy"),
                Status(BookingStatus.Expired, "Hết hạn"),
                Status(BookingStatus.Completed, "Hoàn thành")
            ]),
            new MobileStatusGroupResponse(
            [
                Status(TripStatus.ACCEPTED, "Tài xế đã nhận chuyến"),
                Status(TripStatus.DRIVER_ARRIVING, "Tài xế đang đến"),
                Status(TripStatus.ARRIVED, "Tài xế đã đến"),
                Status(TripStatus.IN_PROGRESS, "Đang di chuyển"),
                Status(TripStatus.WAITING_RETURN_CONFIRM, "Chờ xác nhận nhận lại xe"),
                Status(TripStatus.RETURN_CONFIRMED, "Đã xác nhận nhận lại xe"),
                Status(TripStatus.COMPLETED, "Hoàn thành"),
                Status(TripStatus.CANCELLED, "Đã hủy")
            ]),
            new MobileStatusGroupResponse(
            [
                Status(DriverOfferStatus.Sent, "Đã gửi tài xế"),
                Status(DriverOfferStatus.DriverAccepted, "Tài xế đã nhận"),
                Status(DriverOfferStatus.CustomerConfirmed, "Khách đã xác nhận"),
                Status(DriverOfferStatus.Rejected, "Đã từ chối"),
                Status(DriverOfferStatus.Expired, "Hết hạn"),
                Status(DriverOfferStatus.Cancelled, "Đã hủy")
            ]),
            new MobileDriverConfigResponse(
            [
                Status(DriverWorkStatus.Online, "Đang hoạt động"),
                Status(DriverWorkStatus.Offline, "Ngoại tuyến"),
                Status(DriverWorkStatus.Busy, "Đang có chuyến")
            ],
                DriverLocationUpdateIntervalSeconds),
            new MobileMatchingConfigResponse(
                SearchingBookingPollIntervalSeconds,
                NearbyDriversRefreshIntervalSeconds,
                TripStatusPollIntervalSeconds,
                DriverLocationUpdateIntervalSeconds),
            new MobileFeatureConfigResponse(
                "GoogleMaps", // primaryMapProvider
                EnableMapProvider("GoogleMaps"),
                EnableMapProvider("VietMap"))));
    }

    private static MobileStatusOptionResponse Status<TEnum>(TEnum value, string label)
        where TEnum : struct, Enum
    {
        return new MobileStatusOptionResponse(value.ToString(), label);
    }

    private bool EnableMapProvider(string providerName)
    {
        return _configuration.GetSection($"MapServices:{providerName}").Exists();
    }
}
