using SafeRide.Domain.Enums;
using System;
using System.Collections.Generic;

namespace SafeRide.Domain.Entities;

public partial class TripReturnConfirmation
{
    public long Id { get; set; }

    public long TripId { get; set; }

    public Guid DriverId { get; set; }

    public Guid ConfirmedByUserId { get; set; }

    public HandoverStatus HandoverStatus { get; set; } = HandoverStatus.Pending;

    public DateTime ConfirmedAt { get; set; }

    public decimal? DriverLatitude { get; set; }

    public decimal? DriverLongitude { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Trip Trip { get; set; } = null!;

    public virtual DriverProfile Driver { get; set; } = null!;

    public virtual AspNetUser ConfirmedByUser { get; set; } = null!;

    public virtual ICollection<TripReturnEvidence> Evidence { get; set; } = new List<TripReturnEvidence>();
}
