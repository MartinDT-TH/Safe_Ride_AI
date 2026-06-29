namespace SafeRide.Application.Common.Models;

public sealed class SimulatorOptions
{
    public const string SectionName = "Simulator";

    public bool EnableMockDrivers { get; set; } = true;
    public bool EnableMockCustomerService { get; set; } = true;

    public bool MockDriverAutoAcceptOffers { get; set; } = true;

    public bool MockDriverAutoProgressAfterConfirm { get; set; } = true;

    public bool MockDriverAutoCompleteTrips { get; set; } = true;

    public int MockDriverTtlRefreshSeconds { get; set; } = 60;

    public bool MockCustomerAutoConfirmDriver { get; set; } = true;
    public bool AutoConfirmRealCustomerBookings { get; set; } = false;

    public bool MockDriverSkipMovementDelay { get; set; } = false;

    public bool EnableSimulatorConsoleOutput { get; set; } = true;

    // Real Driver Flow Simulation Options (Demo-only behavior)
    // To test Real Driver manually, leave these as false.
    public bool RealDriverAutoAcceptOffers { get; set; } = false;
    public bool RealDriverAutoProgressTrips { get; set; } = true;
    public bool RealDriverSimulateMovement { get; set; } = true;

    // Mock Booking Generator Options
    public bool EnableMockBookingGenerator { get; set; } = false;
    public int MockBookingIntervalSeconds { get; set; } = 30;
    public int MaxConcurrentMockBookings { get; set; } = 5;
    
    // Default to Da Nang center
    public double MockBookingBaseLat { get; set; } = 16.0544;
    public double MockBookingBaseLng { get; set; } = 108.2022;

    public Guid? MockBookingCustomerId { get; set; }
}
