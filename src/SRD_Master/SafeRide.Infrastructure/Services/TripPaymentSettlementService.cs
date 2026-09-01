using SafeRide.Application.Common.Interfaces;
using SafeRide.Domain.Entities;

namespace SafeRide.Infrastructure.Services;

public sealed class TripPaymentSettlementService
{
    private readonly ITripFinancialSettlementService _financialSettlementService;

    public TripPaymentSettlementService(ITripFinancialSettlementService financialSettlementService)
    {
        _financialSettlementService = financialSettlementService;
    }

    public async Task SettleSuccessfulQrPaymentAsync(
        Trip trip,
        string? providerReference,
        CancellationToken cancellationToken)
    {
        await _financialSettlementService.SettleQrDriverEarningAsync(
            trip,
            providerReference,
            cancellationToken);
    }
}
