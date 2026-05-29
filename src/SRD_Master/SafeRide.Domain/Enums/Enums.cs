namespace SafeRide.Domain.Enums;

public enum WorkStatus
{
    Online,
    Offline,
    Busy
}

public enum DocumentType
{
    ID_CARD,
    DRIVING_LICENSE,
    CRIMINAL_RECORD
}

/// <summary>
/// Full license class set — used in DriverKyc.
/// </summary>
public enum LicenseClass
{
    A1, A, B1, B, C1, C, D1, D2, D,
    Old_B1, Old_B2, Old_A1, Old_A2
}

/// <summary>
/// Subset used in Vehicles.RequiredLicenseClass and PricingRules.VehicleClass.
/// </summary>
public enum VehicleClass
{
    A1, A, B, C1, C, D1, D2, D
}

public enum KycStatus
{
    Pending,
    Approved,
    Rejected
}

public enum EngineType
{
    ICE,
    EV
}

public enum TransmissionType
{
    Manual,
    Automatic,
    None
}

public enum BookingType
{
    Now,
    Scheduled
}

public enum BookingStatus
{
    SEARCHING,
    DRIVER_ASSIGNED,
    CANCELLED,
    EXPIRED,
    COMPLETED
}

public enum BookingSource
{
    Manual,
    VoiceCommand,
    Scheduled
}

public enum TripStatus
{
    ACCEPTED,
    ARRIVED,
    IN_PROGRESS,
    COMPLETED,
    CANCELLED
}

public enum PaymentStatus
{
    Pending,
    Success,
    Failed
}

public enum TransactionType
{
    Income,
    Withdrawal,
    Penalty,
    Bonus
}

public enum WithdrawalStatus
{
    Pending,
    Approved,
    Rejected
}

public enum DiscountType
{
    Percentage,
    Fixed
}

public enum CallStatus
{
    Missed,
    Connected,
    Failed
}

public enum SOSAlertStatus
{
    Active,
    Resolved
}

public enum ReportStatus
{
    Pending,
    Resolved,
    Rejected
}

