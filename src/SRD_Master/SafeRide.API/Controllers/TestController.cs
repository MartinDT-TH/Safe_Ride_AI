using Microsoft.AspNetCore.Mvc;
using SafeRide.Infrastructure.Redis;

namespace SafeRide.API.Controllers;

[ApiController]
[Route("api/test")]
public class TestController : ControllerBase
{
    private readonly IRedisService _redis;

    public TestController(
        IRedisService redis)
    {
        _redis = redis;
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
}
