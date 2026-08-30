using SafeRide.Domain.Enums;

namespace SafeRide.Domain.Entities;

/// <summary>SafeRide-managed private evidence for a vehicle insurance policy.</summary>
public sealed class InsurancePolicyDocument
{
    public long Id { get; set; }
    public long VehicleInsurancePolicyId { get; set; }
    public InsurancePolicyDocumentType DocumentType { get; set; }
    public string StorageObjectKey { get; set; } = null!;
    public string OriginalFileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long FileSizeBytes { get; set; }
    public string Sha256Hash { get; set; } = null!;
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    public VehicleInsurancePolicy VehicleInsurancePolicy { get; set; } = null!;
}
