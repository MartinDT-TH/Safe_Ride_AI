namespace SafeRide.Application.Common.Models;

public sealed class SimulatorOptions
{
    public const string SectionName = "Simulator";

    public bool EnableMockDrivers { get; set; } = true;

    public bool MockDriverAutoAcceptOffers { get; set; } = true;

    public bool MockDriverAutoProgressAfterConfirm { get; set; } = true;

    public bool MockDriverAutoCompleteTrips { get; set; } = true;

    public int MockDriverTtlRefreshSeconds { get; set; } = 60;
}
