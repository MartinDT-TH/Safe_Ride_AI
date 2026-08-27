using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SafeRide.API.Controllers;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Features.Drivers.DTOs;
using SafeRide.Application.Features.Bookings.Commands.CreateBooking;
using SafeRide.Contracts.Requests.Drivers;

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

    [Fact]
    public async Task UpdateMatchingPreferences_UsesAuthenticatedDriverOnly()
    {
        var driverId = Guid.NewGuid();
        var preferences = new DriverMatchingPreferencesServiceFake();
        var controller = CreateController(new DriverRealtimeServiceFake(), preferences);
        controller.ControllerContext = CreateControllerContext(driverId.ToString());

        var result = await controller.UpdateMatchingPreferences(
            new UpdateDriverMatchingPreferencesRequest(true, false),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SafeRide.Contracts.Responses.Drivers.DriverMatchingPreferencesResponse>(ok.Value);
        Assert.Equal(driverId, preferences.UpdatedDriverId);
        Assert.True(response.AcceptLongPickupTrips);
        Assert.False(response.AcceptLongDistanceTrips);
    }

    private static DriversController CreateController(
        IDriverRealtimeService driverRealtimeService,
        IDriverMatchingPreferencesService? preferencesService = null)
    {
        return new DriversController(
            new SenderFake(),
            new BookingAssignmentServiceFake(),
            driverRealtimeService,
            preferencesService ?? new DriverMatchingPreferencesServiceFake());
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
            DriverLocationUpdateInput location,
            CancellationToken cancellationToken = default)
        {
            DriverId = driverId;
            Latitude = location.Latitude;
            Longitude = location.Longitude;
            return Task.CompletedTask;
        }

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

    private sealed class SenderFake : ISender
    {
        public Task<TResponse> Send<TResponse>(
            IRequest<TResponse> request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task Send<TRequest>(
            TRequest request,
            CancellationToken cancellationToken = default)
            where TRequest : IRequest =>
            throw new NotImplementedException();

        public Task<object?> Send(
            object request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            CancellationToken cancellationToken = default) =>
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

    private sealed class DriverMatchingPreferencesServiceFake : IDriverMatchingPreferencesService
    {
        public Guid? UpdatedDriverId { get; private set; }
        public Task<DriverMatchingPreferencesDto> GetAsync(Guid driverId, CancellationToken cancellationToken) =>
            Task.FromResult(new DriverMatchingPreferencesDto(false, false));

        public Task<DriverMatchingPreferencesDto> UpdateAsync(
            Guid driverId,
            bool acceptLongPickupTrips,
            bool acceptLongDistanceTrips,
            CancellationToken cancellationToken)
        {
            UpdatedDriverId = driverId;
            return Task.FromResult(new DriverMatchingPreferencesDto(
                acceptLongPickupTrips,
                acceptLongDistanceTrips));
        }
    }
}
