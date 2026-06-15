﻿namespace SafeRide.Domain.Enums;

// ================================================================
// USER
// ================================================================
// SQL hiện tại Gender chưa có CHECK constraint.
// Nếu muốn dùng enum cho Gender thì nên thêm CHECK trong DB sau.
public enum Gender
{
    Male,
    Female,
    Other
}

// ================================================================
// DRIVER / KYC
// ================================================================

public enum DriverWorkStatus
{
    Online,
    Offline,
    Busy
}

public enum KycDocumentType
{
    ID_CARD,
    DRIVING_LICENSE,
    CRIMINAL_RECORD
}

public enum KycStatus
{
    Pending,
    Approved,
    Rejected
}

/// <summary>
/// Dùng cho DriverKyc.LicenseClass.
/// Có cả bằng cũ vì SQL đang cho phép Old_B1, Old_B2, Old_A1, Old_A2.
/// </summary>
public enum LicenseClass
{
    A1,
    A,
    B1,
    B,

    Old_B1,
    Old_B2,
    Old_A1,
    Old_A2
}

/// <summary>
/// Dùng cho Vehicles.RequiredLicenseClass và PricingRules.VehicleClass.
/// Không gồm B1 / Old_* vì SQL của RequiredLicenseClass và VehicleClass không cho phép.
/// </summary>
public enum RequiredLicenseClass
{
    A1,
    A,
    B
}

// ================================================================
// VEHICLE
// ================================================================

public enum VehicleType
{
    Motorbike,
    Car
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

// ================================================================
// BOOKING / TRIP
// ================================================================

public enum BookingType
{
    Now,
    Scheduled
}

public enum BookingStatus
{
    PendingSchedule,
    Searching,
    DriverAssigned,
    Cancelled,
    Expired,
    Completed
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
    DRIVER_ARRIVING,
    ARRIVED,
    IN_PROGRESS,
    COMPLETED,
    CANCELLED
}

public enum HandoverStatus
{
    Pending,
    CustomerConfirmed,
    DriverConfirmed,
    Disputed,
    Resolved
}

// ================================================================
// PAYMENT / WALLET
// ================================================================

public enum PaymentMethod
{
    QR,
    CASH
}

public enum PaymentStatus
{
    Pending,
    Success,
    Failed,
    Unpaid,
    Cancelled,
    Disputed,
    Refunded
}

public enum WalletTransactionType
{
    Income,
    Withdrawal,
    Penalty,
    Bonus
}

public enum WithdrawalRequestStatus
{
    Pending,
    Approved,
    Rejected
}

// ================================================================
// PROMOTION
// ================================================================

public enum DiscountType
{
    Percentage,
    Fixed
}

// ================================================================
// SAFETY / REPORT
// ================================================================

public enum SOSStatus
{
    Active,
    Resolved,
    Cancelled
}

public enum ReportStatus
{
    Pending,
    Resolved,
    Rejected
}
