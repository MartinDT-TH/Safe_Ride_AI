using SafeRide.Domain.Enums;

namespace SafeRide.Domain.Entities;

public sealed class InsuranceClaimProviderAudit
{
    public long Id { get; set; }
    public long ProtectionClaimId { get; set; }
    public InsuranceProviderOperation Operation { get; set; }
    public InsuranceClaimStatus ResultStatus { get; set; }
    public decimal RequestedAmount { get; set; }
    public decimal ApprovedAmount { get; set; }
    public string ProviderReference { get; set; } = null!;
    public string RequestPayload { get; set; } = null!;
    public string ResponsePayload { get; set; } = null!;
    public Guid? PerformedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ProtectionClaim ProtectionClaim { get; set; } = null!;
}
