namespace SafeRide.Infrastructure.Simulator;

/// <summary>
/// Represents a simulated driver for testing booking acceptance and movement.
/// </summary>
public sealed class MockDriver
{
    public Guid DriverId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Acceptance rate (0-100). E.g., 90 means 90% chance to accept.
    /// </summary>
    public int AcceptanceRatePercent { get; set; }
    
    /// <summary>
    /// Delay in seconds before accepting an offer (1-5 seconds).
    /// </summary>
    public int ResponseDelaySeconds { get; set; }
    
    /// <summary>
    /// Current latitude
    /// </summary>
    public double CurrentLat { get; set; }
    
    /// <summary>
    /// Current longitude
    /// </summary>
    public double CurrentLng { get; set; }
    
    /// <summary>
    /// Whether this driver is currently online
    /// </summary>
    public bool IsActive { get; set; }
    
    /// <summary>
    /// IDs of offers currently being evaluated or accepted
    /// </summary>
    public HashSet<long> ProcessedOffers { get; } = new();

    /// <summary>
    /// Trip IDs that already have a movement simulation running.
    /// </summary>
    public HashSet<long> StartedTrips { get; } = new();
}
