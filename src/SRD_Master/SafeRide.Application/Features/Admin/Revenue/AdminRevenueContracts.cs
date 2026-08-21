namespace SafeRide.Application.Features.Admin.Revenue;

public interface IAdminRevenueQueryService
{
    Task<AdminRevenueQueryResult> GetAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminRevenueExportItem>> GetExportAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);
}

public sealed record AdminRevenueQueryResult(
    decimal TotalRevenue,
    int SuccessfulTrips,
    decimal PlatformRevenue,
    decimal PreviousRevenue,
    int PreviousTrips,
    IReadOnlyDictionary<DateOnly, decimal> RevenueByDate,
    IReadOnlyList<AdminRevenueServiceItem> Services);

public sealed record AdminRevenueServiceItem(
    string ServiceName,
    decimal Revenue,
    int Trips);

public sealed record AdminRevenueExportItem(
    DateTime PaidAt,
    long TripId,
    string ServiceName,
    string PaymentMethod,
    decimal Revenue,
    decimal PlatformRevenue);
