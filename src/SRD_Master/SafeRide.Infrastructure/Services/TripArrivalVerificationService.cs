using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Features.Bookings;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;

namespace SafeRide.Infrastructure.Services;

public sealed class TripArrivalVerificationService : ITripArrivalVerificationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IRedisService _redisService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IOptionsMonitor<CustomerNoShowOptions> _options;

    public TripArrivalVerificationService(
        ApplicationDbContext dbContext,
        IRedisService redisService,
        IDateTimeProvider dateTimeProvider,
        IOptionsMonitor<CustomerNoShowOptions> options)
    {
        _dbContext = dbContext;
        _redisService = redisService;
        _dateTimeProvider = dateTimeProvider;
        _options = options;
    }

    public async Task<TripArrivalVerificationResult> VerifyAndRecordAsync(
        Guid driverId,
        long tripId,
        CancellationToken cancellationToken)
    {
        var trip = await _dbContext.Trips
            .Include(x => x.Booking)
            .FirstOrDefaultAsync(x => x.Id == tripId, cancellationToken);
        if (trip is null)
            throw new BookingException("trip.not_found", "Không tìm thấy chuyến đi.", 404);
        if (trip.DriverId != driverId)
            throw new BookingException("trip.driver_not_assigned", "Tài xế không được phân công cho chuyến đi này.", 403);
        if (trip.TripStatus is not (TripStatus.ACCEPTED or TripStatus.DRIVER_ARRIVING))
            throw new BookingException("trip.invalid_arrival_status", "Chuyến đi không cho phép xác nhận đã đến điểm đón.", 400);

        var locationJson = await _redisService.GetAsync(RedisKeys.DriverLocation(driverId));
        var location = locationJson is null
            ? null
            : JsonSerializer.Deserialize<DriverLocationCache>(locationJson);
        if (location is null)
            throw new BookingException("trip.arrival_location_missing", "Không tìm thấy vị trí hiện tại của tài xế.", 400);

        var utcNow = _dateTimeProvider.UtcNow;
        if (utcNow - location.UpdatedAt > TimeSpan.FromSeconds(_options.CurrentValue.DriverLocationFreshnessSeconds))
            throw new BookingException("trip.arrival_location_stale", "Vị trí tài xế đã quá cũ. Vui lòng cập nhật vị trí rồi thử lại.", 400);
        if (location.Latitude is < -90 or > 90 || location.Longitude is < -180 or > 180)
            throw new BookingException("trip.arrival_location_invalid", "Vị trí tài xế không hợp lệ.", 400);

        var distanceMeters = CalculateDistanceMeters(
            location.Latitude,
            location.Longitude,
            trip.Booking.PickupLocation.Y,
            trip.Booking.PickupLocation.X);
        if (distanceMeters > _options.CurrentValue.ArrivalRadiusMeters)
            throw new BookingException("trip.arrival_too_far", "Tài xế chưa ở đủ gần điểm đón.", 400);

        trip.ArrivalLatitude = (decimal)location.Latitude;
        trip.ArrivalLongitude = (decimal)location.Longitude;
        trip.ArrivalDistanceMeters = (decimal)distanceMeters;
        trip.ArrivalLocationVerifiedAt = utcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new TripArrivalVerificationResult(
            location.Latitude,
            location.Longitude,
            decimal.Round((decimal)distanceMeters, 3),
            utcNow);
    }

    private static double CalculateDistanceMeters(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        const double earthRadiusMeters = 6371000d;
        var lat1 = latitude1 * Math.PI / 180d;
        var lat2 = latitude2 * Math.PI / 180d;
        var deltaLat = (latitude2 - latitude1) * Math.PI / 180d;
        var deltaLon = (longitude2 - longitude1) * Math.PI / 180d;
        var a = Math.Sin(deltaLat / 2d) * Math.Sin(deltaLat / 2d)
            + Math.Cos(lat1) * Math.Cos(lat2)
            * Math.Sin(deltaLon / 2d) * Math.Sin(deltaLon / 2d);
        return earthRadiusMeters * 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
    }
}
