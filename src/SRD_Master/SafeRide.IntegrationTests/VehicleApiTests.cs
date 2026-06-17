using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SafeRide.Application.Features.Auth.Services;
using SafeRide.Application.Features.Vehicles.DTOs;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.IntegrationTests;

public sealed class VehicleApiTests
{
    [Fact]
    public async Task VehicleLifecycle_PersistsCrudAndEnforcesOwnership()
    {
        using var factory = new AuthApiFactory();
        using var ownerClient = factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                await CreateAccessTokenAsync(factory, "owner@example.test"));

        var first = await CreateVehicleAsync(
            ownerClient,
            "Honda Vision",
            "29A1-123.45",
            VehicleType.Motorbike);
        var second = await CreateVehicleAsync(
            ownerClient,
            "Toyota Vios",
            "30F-987.65",
            VehicleType.Car);
        var updateResponse = await ownerClient.PutAsJsonAsync(
            $"/api/vehicles/{second.Id}",
            new SaveVehicleRequest
            {
                BrandModel = "Toyota Vios 2024",
                PlateNumber = second.PlateNumber,
                Color = "Trắng",
                VehicleType = VehicleType.Car
            });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var otherClient = factory.CreateClient();
        otherClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                await CreateAccessTokenAsync(factory, "other@example.test"));
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await otherClient.DeleteAsync($"/api/vehicles/{second.Id}")).StatusCode);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await ownerClient.DeleteAsync($"/api/vehicles/{second.Id}")).StatusCode);

        var vehicles = await ownerClient.GetFromJsonAsync<List<VehicleResponse>>(
            "/api/vehicles");
        Assert.NotNull(vehicles);
        var remaining = Assert.Single(vehicles);
        Assert.Equal(first.Id, remaining.Id);
    }

    private static async Task<VehicleResponse> CreateVehicleAsync(
        HttpClient client,
        string brandModel,
        string plateNumber,
        VehicleType vehicleType)
    {
        var response = await client.PostAsJsonAsync(
            "/api/vehicles",
            new SaveVehicleRequest
            {
                BrandModel = brandModel,
                PlateNumber = plateNumber,
                Color = "Đen",
                VehicleType = vehicleType
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<VehicleResponse>())!;
    }

    private static async Task<string> CreateAccessTokenAsync(
        AuthApiFactory factory,
        string email)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AspNetUser>>();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var user = new AspNetUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FullName = email,
            PhoneNumber = email == "owner@example.test"
                ? "+84901234591"
                : "+84901234592",
            PhoneNumberConfirmed = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        Assert.True((await userManager.CreateAsync(user)).Succeeded);
        var token = await tokenService.GenerateAccessTokenAsync(
            user,
            new[] { "Customer" });
        return token.Token;
    }
}
