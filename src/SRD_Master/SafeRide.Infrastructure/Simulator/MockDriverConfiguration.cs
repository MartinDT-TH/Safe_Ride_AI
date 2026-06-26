namespace SafeRide.Infrastructure.Simulator;

/// <summary>
/// Configuration for 5 mock drivers with different behaviors.
/// </summary>
public sealed class MockDriverConfiguration
{
    private const double BaseLat = 16.070697;
    private const double BaseLng = 108.213630;
    
    /// <summary>
    /// Gets the 5 pre-configured mock drivers.
    /// </summary>
    public static List<MockDriver> GetMockDrivers()
    {
        return new List<MockDriver>
        {
            new MockDriver
            {
                DriverId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                PhoneNumber = "0901000001",
                Name = "Nguyễn Văn A",
                AcceptanceRatePercent = 100,
                ResponseDelaySeconds = 1,
                CurrentLat = BaseLat + 0.001,
                CurrentLng = BaseLng + 0.001,
                IsActive = true
            },
            new MockDriver
            {
                DriverId = Guid.Parse("10000000-0000-0000-0000-000000000002"),
                PhoneNumber = "0901000002",
                Name = "Trần Văn B",
                AcceptanceRatePercent = 100,
                ResponseDelaySeconds = 1,
                CurrentLat = BaseLat - 0.001,
                CurrentLng = BaseLng - 0.001,
                IsActive = true
            },
            new MockDriver
            {
                DriverId = Guid.Parse("10000000-0000-0000-0000-000000000003"),
                PhoneNumber = "0901000003",
                Name = "Lê Văn C",
                AcceptanceRatePercent = 100,
                ResponseDelaySeconds = 1,
                CurrentLat = BaseLat + 0.002,
                CurrentLng = BaseLng - 0.002,
                IsActive = true
            },
            new MockDriver
            {
                DriverId = Guid.Parse("10000000-0000-0000-0000-000000000004"),
                PhoneNumber = "0901000004",
                Name = "Phạm Văn D",
                AcceptanceRatePercent = 100,
                ResponseDelaySeconds = 1,
                CurrentLat = BaseLat - 0.0015,
                CurrentLng = BaseLng + 0.0015,
                IsActive = true
            },
            new MockDriver
            {
                DriverId = Guid.Parse("10000000-0000-0000-0000-000000000005"),
                PhoneNumber = "0901000005",
                Name = "Hoàng Văn E",
                AcceptanceRatePercent = 100,
                ResponseDelaySeconds = 1,
                CurrentLat = BaseLat + 0.0005,
                CurrentLng = BaseLng - 0.0005,
                IsActive = true
            }
        };
    }
}
