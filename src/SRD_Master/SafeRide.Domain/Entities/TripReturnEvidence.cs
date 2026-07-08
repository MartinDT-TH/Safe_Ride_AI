using System;

namespace SafeRide.Domain.Entities;

public partial class TripReturnEvidence
{
    public long Id { get; set; }

    public long TripReturnConfirmationId { get; set; }

    public string ImageUrl { get; set; } = null!;

    public string? ImagePublicId { get; set; }

    public string? OriginalFileName { get; set; }

    public string? ContentType { get; set; }

    public long? FileSizeBytes { get; set; }

    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual TripReturnConfirmation TripReturnConfirmation { get; set; } = null!;
}
