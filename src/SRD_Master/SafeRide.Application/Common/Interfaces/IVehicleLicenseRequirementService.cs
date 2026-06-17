using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Common.Interfaces;

public interface IVehicleLicenseRequirementService
{
    bool TryResolveRequiredLicenseClass(
        VehicleType vehicleType,
        int? engineCapacityCc,
        out RequiredLicenseClass requiredLicenseClass);

    bool HasValidRequirement(Vehicle vehicle);
}
