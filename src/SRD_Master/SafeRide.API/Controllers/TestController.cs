using Microsoft.AspNetCore.Mvc;
using SafeRide.Infrastructure.Redis;

namespace SafeRide.API.Controllers;

[ApiController]
[Route("api/test")]
public class TestController : ControllerBase
{
    private readonly IRedisService _redis;
    private readonly MediatR.ISender _sender;

    public TestController(
        IRedisService redis,
        MediatR.ISender sender)
    {
        _redis = redis;
        _sender = sender;
    }

    [HttpGet("redis")]
    public async Task<IActionResult> TestRedis()
    {
        await _redis.SetAsync(
            "test:key",
            "SafeRide",
            TimeSpan.FromMinutes(1));

        var value =
            await _redis.GetAsync("test:key");

        return Ok(value);
    }

    [HttpPost("mock-booking")]
    public async Task<IActionResult> MockBooking([FromBody] MockBookingRequest request)
    {
        var customerId = Guid.Parse("571b5831-632a-488d-bb0b-06567bdef38f");

        var result = await _sender.Send(new SafeRide.Application.Features.Bookings.Commands.CreateBooking.CreateBookingCommand(
            customerId,
            request.VehicleId,
            request.ServiceTypeId,
            SafeRide.Domain.Enums.BookingType.Now,
            null,
            request.PickupAddress ?? "Mocked Pickup Address",
            request.PickupLatitude,
            request.PickupLongitude,
            request.DestinationAddress ?? "Mocked Destination Address",
            request.DestinationLatitude ?? (request.PickupLatitude + 0.05),
            request.DestinationLongitude ?? (request.PickupLongitude + 0.05),
            "Mocked demo booking",
            null,
            null
        ));

        return Ok(result);
    }
}

public class MockBookingRequest
{
    public double PickupLatitude { get; set; }
    public double PickupLongitude { get; set; }
    public double? DestinationLatitude { get; set; }
    public double? DestinationLongitude { get; set; }
    public string? PickupAddress { get; set; }
    public string? DestinationAddress { get; set; }
    public long VehicleId { get; set; } = 1;
    public long ServiceTypeId { get; set; } = 1;
}
