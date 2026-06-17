using SafeRide.Application.Common.Interfaces;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Vehicles.Services;

public sealed class VehicleLicenseRequirementService
    : IVehicleLicenseRequirementService
{
    public bool TryResolveRequiredLicenseClass(
        VehicleType vehicleType,
        int? engineCapacityCc,
        out RequiredLicenseClass requiredLicenseClass)
    {
        requiredLicenseClass = default;

        if (!Enum.IsDefined(vehicleType))
        {
            return false;
        }

        if (vehicleType == VehicleType.Car)
        {
            requiredLicenseClass = RequiredLicenseClass.B;
            return true;
        }

        if (!engineCapacityCc.HasValue || engineCapacityCc.Value <= 0)
        {
            return false;
        }

        requiredLicenseClass = engineCapacityCc.Value <= 125
            ? RequiredLicenseClass.A1
            : RequiredLicenseClass.A;
        return true;
    }

    public bool HasValidRequirement(Vehicle vehicle)
    {
        if (!Enum.IsDefined(vehicle.RequiredLicenseClass)
            || !TryResolveRequiredLicenseClass(
                vehicle.VehicleType,
                vehicle.EngineCapacityCc,
                out var expectedRequirement))
        {
            return false;
        }

        return vehicle.RequiredLicenseClass == expectedRequirement;
    }
}
