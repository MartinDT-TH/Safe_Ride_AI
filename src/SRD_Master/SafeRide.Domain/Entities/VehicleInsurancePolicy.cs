using SafeRide.Domain.Enums;

namespace SafeRide.Domain.Entities;

public sealed class VehicleInsurancePolicy
{
    public long Id { get; set; }
    public long VehicleId { get; set; }
    public VehicleInsuranceType InsuranceType { get; set; }
    public string Provider { get; set; } = null!;
    public string PolicyNumber { get; set; } = null!;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public decimal CoverageAmount { get; set; }
    public decimal Deductible { get; set; }
    public string? DocumentUrl { get; set; }
    public InsuranceVerificationStatus VerificationStatus { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public ICollection<InsurancePolicyDocument> Documents { get; set; } = new List<InsurancePolicyDocument>();
}
