using SafeRide.Application.Common.Interfaces;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Drivers.Services;

public sealed class LicenseCompatibilityService : ILicenseCompatibilityService
{
    public bool CanDrive(
        LicenseClass driverLicense,
        RequiredLicenseClass vehicleRequirement)
    {
        return vehicleRequirement switch
        {
            RequiredLicenseClass.A1 => driverLicense
                is LicenseClass.A1
                or LicenseClass.A
                or LicenseClass.Old_A1
                or LicenseClass.Old_A2,
            RequiredLicenseClass.A => driverLicense
                is LicenseClass.A
                or LicenseClass.Old_A2,
            RequiredLicenseClass.B => driverLicense
                is LicenseClass.B
                or LicenseClass.Old_B1
                or LicenseClass.Old_B2,
            _ => false
        };
    }
}
