using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Features.Bookings.Commands.CreateBooking;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Simulator;

/// <summary>
/// Background service that periodically generates mock bookings.
/// Uses the 5 Mock Driver IDs as Customer IDs to simulate incoming requests.
/// </summary>
public sealed class MockBookingGeneratorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MockBookingGeneratorService> _logger;
    private readonly IOptionsMonitor<SimulatorOptions> _simulatorOptionsMonitor;
    private readonly Random _random;

    public MockBookingGeneratorService(
        IServiceScopeFactory scopeFactory,
        ILogger<MockBookingGeneratorService> logger,
        IOptionsMonitor<SimulatorOptions> simulatorOptionsMonitor)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _simulatorOptionsMonitor = simulatorOptionsMonitor;
        _random = new Random();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MockBookingGeneratorService started");

        // Wait a bit before starting to ensure the system is up
        await Task.Delay(5000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var options = _simulatorOptionsMonitor.CurrentValue;
                if (!options.EnableMockBookingGenerator)
                {
                    await Task.Delay(2000, stoppingToken);
                    continue;
                }

                await GenerateMockBookingAsync(options, stoppingToken);

                // Delay for the configured interval
                await Task.Delay(TimeSpan.FromSeconds(options.MockBookingIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating mock booking");
                await Task.Delay(5000, stoppingToken);
            }
        }

        _logger.LogInformation("MockBookingGeneratorService stopped");
    }

    private async Task GenerateMockBookingAsync(SimulatorOptions options, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // 1. Check concurrent mock bookings
        var activeMockBookings = await dbContext.Bookings
            .Where(b => b.BookingStatus == BookingStatus.Searching || b.BookingStatus == BookingStatus.PendingSchedule)
            .CountAsync(cancellationToken);

        if (activeMockBookings >= options.MaxConcurrentMockBookings)
        {
            _logger.LogDebug("Mock booking generation skipped. Max concurrent bookings ({Max}) reached.", options.MaxConcurrentMockBookings);
            return;
        }

        // 2. Pick a random Mock Driver ID to act as the Customer
        var mockDrivers = MockDriverConfiguration.GetMockDrivers();
        var randomMockDriver = mockDrivers[_random.Next(mockDrivers.Count)];
        var customerId = randomMockDriver.DriverId;

        // 3. Ensure this customer has a vehicle in the database
        var vehicle = await dbContext.Vehicles.FirstOrDefaultAsync(v => v.OwnerUserId == customerId && !v.IsDeleted, cancellationToken);
        if (vehicle == null)
        {
            vehicle = new Vehicle
            {
                OwnerUserId = customerId,
                PlateNumber = $"43A-{_random.Next(10000, 99999)}",
                BrandModel = "Mock Toyota Vios",
                RequiredLicenseClass = RequiredLicenseClass.B,
                VehicleType = VehicleType.Car,
                EngineType = EngineType.ICE,
                TransmissionType = TransmissionType.Automatic,
                Color = "Trắng",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.Vehicles.Add(vehicle);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        long serviceTypeId = 1;

        // 4. Ensure a PricingRule exists for ServiceType 1
        var pricingRule = await dbContext.PricingRules
            .FirstOrDefaultAsync(pr => pr.ServiceTypeId == serviceTypeId && pr.VehicleClass == vehicle.RequiredLicenseClass, cancellationToken);

        if (pricingRule == null)
        {
            // Auto-create a pricing rule for mock purposes
            pricingRule = new PricingRule
            {
                ServiceTypeId = serviceTypeId,
                VehicleClass = vehicle.RequiredLicenseClass,
                BaseFare = 15000,
                PricePerKm = 10000,
                MinFare = 20000,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.PricingRules.Add(pricingRule);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var baseLat = options.MockBookingBaseLat;
        var baseLng = options.MockBookingBaseLng;

        var onlineDriver = await dbContext.DriverProfiles
            .Where(d => d.WorkStatus == DriverWorkStatus.Online)
            .OrderByDescending(d => d.LastActiveAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (onlineDriver != null)
        {
            var redisService = scope.ServiceProvider.GetRequiredService<SafeRide.Infrastructure.Redis.IRedisService>();
            var locationJson = await redisService.GetAsync(SafeRide.Infrastructure.Redis.RedisKeys.DriverLocation(onlineDriver.DriverId));
            if (!string.IsNullOrEmpty(locationJson))
            {
                var cache = System.Text.Json.JsonSerializer.Deserialize<SafeRide.Infrastructure.Redis.DriverLocationCache>(locationJson);
                if (cache != null)
                {
                    baseLat = cache.Latitude;
                    baseLng = cache.Longitude;
                    _logger.LogInformation("Using real online driver {DriverId} location as base for mock booking: {Lat}, {Lng}", onlineDriver.DriverId, baseLat, baseLng);
                }
            }
        }

        // 6. Generate random Pickup and Destination around base location
        double pickupLat = baseLat + (_random.NextDouble() * 0.04 - 0.02);
        double pickupLng = baseLng + (_random.NextDouble() * 0.04 - 0.02);
        double destLat = pickupLat + (_random.NextDouble() * 0.02 - 0.01);
        double destLng = pickupLng + (_random.NextDouble() * 0.02 - 0.01);

        var command = new CreateBookingCommand(
            CustomerId: customerId,
            VehicleId: vehicle.Id,
            ServiceTypeId: serviceTypeId,
            BookingType: BookingType.Now,
            ScheduledAt: null,
            PickupAddress: $"Điểm đón giả lập {Guid.NewGuid().ToString().Substring(0, 4)}",
            PickupLatitude: pickupLat,
            PickupLongitude: pickupLng,
            DestinationAddress: $"Điểm đến giả lập {Guid.NewGuid().ToString().Substring(0, 4)}",
            DestinationLatitude: destLat,
            DestinationLongitude: destLng,
            SpecialRequest: "Đây là cuốc xe tự động từ Simulator",
            EstimatedHours: null,
            PromotionCode: null
        );

        // 7. Send the command to Mediator
        try
        {
            _logger.LogInformation("Generating mock booking for CustomerId {CustomerId}", customerId);
            var response = await mediator.Send(command, cancellationToken);
            _logger.LogInformation("Successfully generated mock booking {BookingId}", response.BookingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create mock booking via mediator.");
        }
    }
}
