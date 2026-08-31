using Microsoft.EntityFrameworkCore;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Infrastructure.Persistence;

public static class DriverKycQueryExtensions
{
    /// <summary>
    /// Materializes encrypted driving-license fields before callers compare or sort them.
    /// Data Protection ciphertext is non-deterministic, so value comparisons and ordering
    /// for these fields must never be translated to SQL.
    /// </summary>
    public static async Task<IReadOnlyList<DriverLicenseKycSnapshot>>
        LoadApprovedDrivingLicensesAsync(
            this ApplicationDbContext dbContext,
            Guid driverId,
            CancellationToken cancellationToken)
    {
        return await dbContext.DriverKycs
            .AsNoTracking()
            .Where(kyc => kyc.DriverId == driverId
                && kyc.DocumentType == KycDocumentType.DRIVING_LICENSE
                && kyc.KycStatus == KycStatus.Approved)
            .Select(kyc => new DriverLicenseKycSnapshot(
                kyc.Id,
                kyc.LicenseClass,
                kyc.IssueDate,
                kyc.ExpiryDate,
                kyc.CreatedAt,
                kyc.VerifiedAt))
            .ToListAsync(cancellationToken);
    }

    public static DateOnly? GetLatestExpiryDate(
        this IEnumerable<DriverLicenseKycSnapshot> licenses)
    {
        return licenses
            .Where(license => license.LicenseClass.HasValue
                && license.ExpiryDate.HasValue)
            .Select(license => license.ExpiryDate)
            .OrderByDescending(expiryDate => expiryDate)
            .FirstOrDefault();
    }

    public static DateOnly? GetLatestExpiredExpiryDate(
        this IEnumerable<DriverLicenseKycSnapshot> licenses,
        DateOnly asOfDate)
    {
        return licenses
            .Where(license => license.LicenseClass.HasValue
                && license.ExpiryDate.HasValue
                && license.ExpiryDate.Value < asOfDate)
            .Select(license => license.ExpiryDate)
            .OrderByDescending(expiryDate => expiryDate)
            .FirstOrDefault();
    }

    public static bool IsUsableOn(
        this DriverLicenseKycSnapshot license,
        DateOnly asOfDate)
    {
        return license.LicenseClass.HasValue
            && (!license.ExpiryDate.HasValue
                || license.ExpiryDate.Value >= asOfDate);
    }
}

public sealed record DriverLicenseKycSnapshot(
    long Id,
    LicenseClass? LicenseClass,
    DateOnly? IssueDate,
    DateOnly? ExpiryDate,
    DateTime CreatedAt,
    DateTime? VerifiedAt);
