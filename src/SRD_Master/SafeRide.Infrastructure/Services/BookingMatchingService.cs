using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class BookingMatchingService : IBookingMatchingService
{
    private readonly ILogger<BookingMatchingService> _logger;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILicenseCompatibilityService _licenseCompatibilityService;
    private readonly IVehicleLicenseRequirementService _vehicleLicenseRequirementService;

    public BookingMatchingService(
        ILogger<BookingMatchingService> logger,
        ApplicationDbContext dbContext,
        ILicenseCompatibilityService licenseCompatibilityService,
        IVehicleLicenseRequirementService vehicleLicenseRequirementService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _licenseCompatibilityService = licenseCompatibilityService;
        _vehicleLicenseRequirementService = vehicleLicenseRequirementService;
    }

    public async Task StartMatchingAsync(
        long bookingId,
        CancellationToken cancellationToken)
    {
        var booking = await _dbContext.Bookings
            .AsNoTracking()
            .Include(x => x.Vehicle)
            .FirstOrDefaultAsync(
                x => x.BookingId == bookingId,
                cancellationToken);
        if (booking is null)
        {
            _logger.LogWarning(
                "Matching skipped because booking {BookingId} was not found.",
                bookingId);
            return;
        }

        if (!_vehicleLicenseRequirementService.HasValidRequirement(booking.Vehicle))
        {
            _logger.LogWarning(
                "Matching skipped for booking {BookingId} because vehicle {VehicleId} has invalid license requirement.",
                bookingId,
                booking.VehicleId);
            return;
        }

        var approvedDriverLicenses = await _dbContext.DriverKycs
            .AsNoTracking()
            .Where(x =>
                x.DocumentType == KycDocumentType.DRIVING_LICENSE &&
                x.KycStatus == KycStatus.Approved &&
                x.LicenseClass.HasValue)
            .Join(
                _dbContext.DriverProfiles
                    .AsNoTracking()
                    .Where(x => x.WorkStatus == DriverWorkStatus.Online),
                kyc => kyc.DriverId,
                profile => profile.DriverId,
                (kyc, profile) => new
                {
                    kyc.DriverId,
                    LicenseClass = kyc.LicenseClass!.Value,
                    kyc.VerifiedAt,
                    kyc.CreatedAt
                })
            .ToListAsync(cancellationToken);

        var eligibleDriverIds = approvedDriverLicenses
            .GroupBy(x => x.DriverId)
            .Select(group => group
                .OrderByDescending(x => x.VerifiedAt ?? x.CreatedAt)
                .First())
            .Where(x => _licenseCompatibilityService.CanDrive(
                x.LicenseClass,
                booking.Vehicle.RequiredLicenseClass))
            .Select(x => x.DriverId)
            .ToList();

        _logger.LogInformation(
            "Matching requested for booking {BookingId}. {EligibleDriverCount} online drivers are license-compatible with {RequiredLicenseClass}.",
            bookingId,
            eligibleDriverIds.Count,
            booking.Vehicle.RequiredLicenseClass);
    }
}
