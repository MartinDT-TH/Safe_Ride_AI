using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Contracts.Responses.Drivers;
using SafeRide.Infrastructure.Redis;
using System.Text.Json;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize]
[Route("api/drivers")]
public sealed class DriversController : ControllerBase
{
    private readonly IRedisService _redisService;

    public DriversController(IRedisService redisService)
    {
        _redisService = redisService;
    }

    [HttpGet("nearby")]
    [ProducesResponseType<List<NearbyDriverResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<NearbyDriverResponse>>> GetNearbyDrivers(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] double radiusKm = 5.0,
        [FromQuery] int limit = 10)
    {
        var driverIds = await _redisService.GeoRadiusAsync(
            RedisKeys.OnlineDriversGeo,
            longitude,
            latitude,
            radiusKm,
            limit);

        var tasks = driverIds.Select(async id =>
        {
            var guid = Guid.Parse(id);
            var locationJson = await _redisService.GetAsync(RedisKeys.DriverLocation(guid));
            if (string.IsNullOrEmpty(locationJson)) return null;

            var cache = JsonSerializer.Deserialize<DriverLocationCache>(locationJson);
            return cache is null ? null : new NearbyDriverResponse(
                guid,
                cache.Latitude,
                cache.Longitude);
        });

        var results = await Task.WhenAll(tasks);
        return Ok(results.Where(x => x is not null).ToList());
    }
}

public record DriverLocationCache(
    Guid DriverId,
    double Latitude,
    double Longitude,
    DateTime UpdatedAt);
