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
/// Có cả bằng cũ vì tài xế hiện hữu có thể vẫn dùng giấy phép cấp trước 2025.
/// </summary>
public enum LicenseClass
{
    // Current SafeRide-supported licenses.
    A1,
    A,
    B,

     // Legacy licenses issued before the 2025 license classification.
    Old_A1,
    Old_A2,
    Old_B1,
    Old_B2
}

/// <summary>
/// Dùng cho Vehicles.RequiredLicenseClass và PricingRules.VehicleClass.
/// SafeRide chỉ hỗ trợ xe máy và xe con, nên không gồm hạng xe tải/bus/đầu kéo.
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

public enum DriverOfferStatus
{
    Sent,
    DriverAccepted,
    CustomerConfirmed,
    Rejected,
    Expired,
    Cancelled
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
    WAITING_RETURN_CONFIRM,
    RETURN_CONFIRMED,
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


// ================================================================
// MAPS
// ================================================================


public enum MapProvider
{
    /// <summary>Tự động chọn theo cấu hình PrimaryProvider trong appsettings.</summary>
    Auto = 0,
    VietMap = 1,
    GoogleMaps = 2,
    OpenRouteService = 3
}

public enum MapTravelMode
{
    Car = 1,
    Motorcycle = 2,
    Bike = 3,
    Foot = 4
}
