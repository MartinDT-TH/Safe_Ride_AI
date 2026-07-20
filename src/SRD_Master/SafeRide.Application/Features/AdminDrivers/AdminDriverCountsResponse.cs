namespace SafeRide.Application.Features.AdminDrivers;

public sealed record AdminDriverCountsResponse(
    int All,
    int Active,
    int Busy,
    int PendingKyc,
    int Blocked);
