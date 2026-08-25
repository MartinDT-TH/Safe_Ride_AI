namespace SafeRide.Contracts.Requests.Trips;

public sealed record ResolveTripEndReconciliationRequest(bool Approved, string? ResolutionNote);
