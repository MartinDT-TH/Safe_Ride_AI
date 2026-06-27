using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SafeRide.API.Controllers;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings.Commands.CreateBooking;
using SafeRide.Contracts.Requests.Drivers;
using SafeRide.Infrastructure.Redis;

namespace SafeRide.UnitTests;

public sealed class DriversControllerTests
{
    [Fact]
    public async Task UpdateLocation_WithAuthenticatedDriver_ReturnsNoContentAndUpdatesLocation()
    {
        var driverId = Guid.NewGuid();
        var driverRealtimeService = new DriverRealtimeServiceFake();
        var controller = CreateController(driverRealtimeService);
        controller.ControllerContext = CreateControllerContext(driverId.ToString());
        var request = new UpdateDriverLocationRequest(10.762622, 106.660172);

        var result = await controller.UpdateLocation(request, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(driverId, driverRealtimeService.DriverId);
        Assert.Equal(request.Latitude, driverRealtimeService.Latitude);
        Assert.Equal(request.Longitude, driverRealtimeService.Longitude);
    }

    [Fact]
    public async Task UpdateLocation_WhenDriverIdCannotBeResolved_ReturnsUnauthorized()
    {
        var driverRealtimeService = new DriverRealtimeServiceFake();
        var controller = CreateController(driverRealtimeService);
        controller.ControllerContext = CreateControllerContext("not-a-guid");

        var result = await controller.UpdateLocation(
            new UpdateDriverLocationRequest(10.762622, 106.660172),
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        Assert.Null(driverRealtimeService.DriverId);
    }

    [Fact]
    public void UpdateDriverLocationRequest_WithOutOfRangeCoordinate_FailsValidation()
    {
        var request = new UpdateDriverLocationRequest(91, 106.660172);
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(validationResults, x =>
            x.MemberNames.Contains(nameof(UpdateDriverLocationRequest.Latitude)));
    }

    private static DriversController CreateController(
        IDriverRealtimeService driverRealtimeService)
    {
        return new DriversController(
            new RedisServiceFake(),
            new BookingAssignmentServiceFake(),
            driverRealtimeService,
            dbContext: null!);
    }

    private static ControllerContext CreateControllerContext(string userId)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId)],
            authenticationType: "Test");

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }

    private sealed class DriverRealtimeServiceFake : IDriverRealtimeService
    {
        public Guid? DriverId { get; private set; }
        public double? Latitude { get; private set; }
        public double? Longitude { get; private set; }

        public Task UpdateDriverLocationAsync(
            Guid driverId,
            double latitude,
            double longitude,
            CancellationToken cancellationToken = default)
        {
            DriverId = driverId;
            Latitude = latitude;
            Longitude = longitude;
            return Task.CompletedTask;
        }

        public Task SetDriverOnlineAsync(
            Guid driverId,
            double latitude,
            double longitude,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task SetDriverOfflineAsync(
            Guid driverId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task RemoveDriverFromOnlineGeoAsync(
            Guid driverId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    private sealed class RedisServiceFake : IRedisService
    {
        public Task SetAsync(string key, string value, TimeSpan expiration) =>
            throw new NotImplementedException();

        public Task<bool> SetIfNotExistsAsync(
            string key,
            string value,
            TimeSpan expiration) =>
            throw new NotImplementedException();

        public Task<string?> GetAsync(string key) =>
            throw new NotImplementedException();

        public Task RemoveAsync(string key) =>
            throw new NotImplementedException();

        public Task<long> IncrementAsync(string key, TimeSpan expiration) =>
            throw new NotImplementedException();

        public Task GeoAddAsync(
            string key,
            double longitude,
            double latitude,
            string member) =>
            throw new NotImplementedException();

        public Task GeoRemoveAsync(
            string key,
            string member,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<string>> GeoRadiusAsync(
            string key,
            double longitude,
            double latitude,
            double radiusKm,
            int count) =>
            throw new NotImplementedException();

        public Task<OtpVerificationResult> VerifyAndConsumeOtpAsync(
            string otpKey,
            string attemptsKey,
            string expectedHash,
            int maxAttempts) =>
            throw new NotImplementedException();
    }

    private sealed class BookingAssignmentServiceFake : IBookingAssignmentService
    {
        public Task<CreateBookingResponse> ConfirmDriverAsync(
            Guid customerId,
            long bookingId,
            long? offerId,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<CreateBookingResponse> RejectDriverAsync(
            Guid customerId,
            long bookingId,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<CreateBookingResponse> AcceptDriverOfferAsync(
            Guid driverId,
            long offerId,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task RejectDriverOfferAsync(
            Guid driverId,
            long offerId,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }
}
