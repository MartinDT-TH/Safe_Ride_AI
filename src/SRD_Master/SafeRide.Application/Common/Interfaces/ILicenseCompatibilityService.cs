using SafeRide.Domain.Enums;

namespace SafeRide.Application.Common.Interfaces;

public interface ILicenseCompatibilityService
{
    bool CanDrive(
        LicenseClass driverLicense,
        RequiredLicenseClass vehicleRequirement);
}