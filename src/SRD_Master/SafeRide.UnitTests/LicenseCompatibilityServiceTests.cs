using SafeRide.Application.Features.Drivers.Services;
using SafeRide.Application.Features.Vehicles.Services;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.UnitTests;

public sealed class LicenseCompatibilityServiceTests
{
    [Theory]
    [InlineData(LicenseClass.A1, RequiredLicenseClass.A1, true)]
    [InlineData(LicenseClass.A, RequiredLicenseClass.A1, true)]
    [InlineData(LicenseClass.Old_A1, RequiredLicenseClass.A1, true)]
    [InlineData(LicenseClass.Old_A2, RequiredLicenseClass.A1, true)]
    [InlineData(LicenseClass.Old_B1, RequiredLicenseClass.A1, false)]
    [InlineData(LicenseClass.A1, RequiredLicenseClass.A, false)]
    [InlineData(LicenseClass.A, RequiredLicenseClass.A, true)]
    [InlineData(LicenseClass.Old_A2, RequiredLicenseClass.A, true)]
    [InlineData(LicenseClass.Old_A1, RequiredLicenseClass.A, false)]
    [InlineData(LicenseClass.B, RequiredLicenseClass.B, true)]
    [InlineData(LicenseClass.Old_B1, RequiredLicenseClass.B, true)]
    [InlineData(LicenseClass.Old_B2, RequiredLicenseClass.B, true)]
    [InlineData(LicenseClass.A, RequiredLicenseClass.B, false)]
    public void CanDrive_UsesSafeRideLicenseMatrix(
        LicenseClass driverLicense,
        RequiredLicenseClass requirement,
        bool expected)
    {
        var service = new LicenseCompatibilityService();

        Assert.Equal(expected, service.CanDrive(driverLicense, requirement));
    }

    [Theory]
    [InlineData(VehicleType.Motorbike, 125, RequiredLicenseClass.A1)]
    [InlineData(VehicleType.Motorbike, 126, RequiredLicenseClass.A)]
    [InlineData(VehicleType.Car, null, RequiredLicenseClass.B)]
    public void TryResolveRequiredLicenseClass_UsesVehicleRules(
        VehicleType vehicleType,
        int? engineCapacityCc,
        RequiredLicenseClass expected)
    {
        var service = new VehicleLicenseRequirementService();

        var succeeded = service.TryResolveRequiredLicenseClass(
            vehicleType,
            engineCapacityCc,
            out var actual);

        Assert.True(succeeded);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void HasValidRequirement_RejectsMotorbikeWithoutEngineCapacity()
    {
        var service = new VehicleLicenseRequirementService();
        var vehicle = new Vehicle
        {
            VehicleType = VehicleType.Motorbike,
            RequiredLicenseClass = RequiredLicenseClass.A1
        };

        Assert.False(service.HasValidRequirement(vehicle));
    }
}
