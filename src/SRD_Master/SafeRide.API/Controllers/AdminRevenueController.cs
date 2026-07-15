using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/revenue")]
public sealed class AdminRevenueController : ControllerBase
{
    private const decimal PlatformShareRate = 0.30m;
    private readonly ApplicationDbContext _db;

    public AdminRevenueController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<AdminRevenueResponse>> GetRevenue(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        var endDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = from ?? endDate.AddDays(-29);
        if (startDate > endDate)
            return BadRequest(new { message = "Ngày bắt đầu không được sau ngày kết thúc." });
        if (endDate.DayNumber - startDate.DayNumber > 365)
            return BadRequest(new { message = "Khoảng thời gian tối đa là 366 ngày." });

        var start = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endExclusive = endDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var days = endDate.DayNumber - startDate.DayNumber + 1;
        var previousStart = start.AddDays(-days);
        var payments = _db.Payments.AsNoTracking()
            .Where(x => x.PaymentStatus == PaymentStatus.Success && x.PaidAt != null);

        var currentRows = await payments
            .Where(x => x.PaidAt >= start && x.PaidAt < endExclusive)
            .Select(x => new RevenueRow(x.TripId, x.Amount, x.PaidAt!.Value.Date,
                x.Trip.Booking.ServiceType.ServiceName))
            .ToListAsync(cancellationToken);
        var previousRevenue = await payments
            .Where(x => x.PaidAt >= previousStart && x.PaidAt < start)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var previousTrips = await payments
            .Where(x => x.PaidAt >= previousStart && x.PaidAt < start)
            .Select(x => x.TripId).Distinct().CountAsync(cancellationToken);

        var totalRevenue = currentRows.Sum(x => x.Amount);
        var successfulTrips = currentRows.Select(x => x.TripId).Distinct().Count();
        var byDate = currentRows.GroupBy(x => DateOnly.FromDateTime(x.PaidDate))
            .ToDictionary(x => x.Key, x => x.Sum(row => row.Amount));
        var timeline = Enumerable.Range(0, days).Select(startDate.AddDays)
            .Select(date => new RevenueTimelinePoint(date, byDate.GetValueOrDefault(date))).ToArray();
        var services = currentRows.GroupBy(x => x.ServiceName)
            .Select(group => new RevenueServiceBreakdown(group.Key, group.Sum(x => x.Amount),
                group.Select(x => x.TripId).Distinct().Count(),
                totalRevenue == 0 ? 0 : Math.Round(group.Sum(x => x.Amount) / totalRevenue * 100m, 2)))
            .OrderByDescending(x => x.Revenue).ToArray();

        return Ok(new AdminRevenueResponse(startDate, endDate, totalRevenue, successfulTrips,
            Math.Round(totalRevenue * PlatformShareRate, 0),
            CalculateGrowth(totalRevenue, previousRevenue), CalculateGrowth(successfulTrips, previousTrips),
            timeline, services));
    }

    private static decimal? CalculateGrowth(decimal current, decimal previous) =>
        previous == 0 ? current == 0 ? 0 : null : Math.Round((current - previous) / previous * 100m, 2);

    private sealed record RevenueRow(long TripId, decimal Amount, DateTime PaidDate, string ServiceName);
}

public sealed record AdminRevenueResponse(DateOnly From, DateOnly To, decimal TotalRevenue,
    int SuccessfulTrips, decimal PlatformFee, decimal? RevenueGrowthPercent, decimal? TripsGrowthPercent,
    IReadOnlyCollection<RevenueTimelinePoint> Timeline, IReadOnlyCollection<RevenueServiceBreakdown> Services);
public sealed record RevenueTimelinePoint(DateOnly Date, decimal Revenue);
public sealed record RevenueServiceBreakdown(string ServiceName, decimal Revenue, int Trips, decimal Percentage);
