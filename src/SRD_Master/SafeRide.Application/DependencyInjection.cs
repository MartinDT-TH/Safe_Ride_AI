using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings.Commands.CreateBooking;
using SafeRide.Application.Features.Bookings.Services;
using SafeRide.Application.Features.Drivers.Services;
using SafeRide.Application.Features.Vehicles.Services;

namespace SafeRide.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssemblyContaining<CreateBookingCommand>());
        services.AddSingleton<IFareEstimationService, FareEstimationService>();
        services.AddSingleton<ILicenseCompatibilityService, LicenseCompatibilityService>();
        services.AddSingleton<IVehicleLicenseRequirementService, VehicleLicenseRequirementService>();

        return services;
    }
}
