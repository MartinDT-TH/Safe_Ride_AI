using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.RiskProtection;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class VehicleInsurancePolicyService : IVehicleInsurancePolicyService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public VehicleInsurancePolicyService(
        ApplicationDbContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyList<VehicleInsurancePolicyResponse>> GetAsync(Guid ownerUserId, long vehicleId, CancellationToken cancellationToken)
    {
        await EnsureOwnerAsync(ownerUserId, vehicleId, cancellationToken);
        return await _dbContext.VehicleInsurancePolicies.AsNoTracking()
            .Where(x => x.VehicleId == vehicleId && !x.IsDeleted)
            .OrderByDescending(x => x.EffectiveFromUtc)
            .Select(x => Map(x)).ToListAsync(cancellationToken);
    }

    public async Task<VehicleInsurancePolicyResponse> CreateAsync(Guid ownerUserId, long vehicleId, VehicleInsurancePolicyRequest request, CancellationToken cancellationToken)
    {
        await EnsureOwnerAsync(ownerUserId, vehicleId, cancellationToken);
        Validate(request);
        var policy = new VehicleInsurancePolicy
        {
            VehicleId = vehicleId,
            InsuranceType = request.InsuranceType,
            Provider = request.Provider.Trim(),
            PolicyNumber = request.PolicyNumber.Trim(),
            EffectiveFromUtc = request.EffectiveFromUtc.ToUniversalTime(),
            ExpiresAtUtc = request.ExpiresAtUtc.ToUniversalTime(),
            CoverageAmount = request.CoverageAmount,
            Deductible = request.Deductible,
            DocumentUrl = request.DocumentUrl?.Trim(),
            VerificationStatus = InsuranceVerificationStatus.PENDING
        };
        _dbContext.VehicleInsurancePolicies.Add(policy);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(policy);
    }

    public async Task<VehicleInsurancePolicyResponse> UpdateAsync(
        Guid ownerUserId, long vehicleId, long policyId,
        VehicleInsurancePolicyRequest request, CancellationToken cancellationToken)
    {
        await EnsureOwnerAsync(ownerUserId, vehicleId, cancellationToken);
        Validate(request);
        var policy = await _dbContext.VehicleInsurancePolicies.SingleOrDefaultAsync(
            x => x.Id == policyId && x.VehicleId == vehicleId && !x.IsDeleted,
            cancellationToken) ?? throw NotFound();
        policy.InsuranceType = request.InsuranceType;
        policy.Provider = request.Provider.Trim();
        policy.PolicyNumber = request.PolicyNumber.Trim();
        policy.EffectiveFromUtc = request.EffectiveFromUtc.ToUniversalTime();
        policy.ExpiresAtUtc = request.ExpiresAtUtc.ToUniversalTime();
        policy.CoverageAmount = request.CoverageAmount;
        policy.Deductible = request.Deductible;
        policy.DocumentUrl = request.DocumentUrl?.Trim();
        policy.VerificationStatus = InsuranceVerificationStatus.PENDING;
        policy.ReviewedByUserId = null;
        policy.ReviewedAtUtc = null;
        policy.UpdatedAtUtc = _dateTimeProvider.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(policy);
    }

    public async Task<VehicleInsurancePolicyResponse> ReviewAsync(
        Guid staffUserId,
        long policyId,
        InsuranceVerificationStatus status,
        CancellationToken cancellationToken)
    {
        if (status is not (InsuranceVerificationStatus.VERIFIED or InsuranceVerificationStatus.REJECTED))
            throw new BookingException(
                "insurance.invalid_verification_status",
                "Staff chỉ có thể xác minh hoặc từ chối hợp đồng bảo hiểm.",
                StatusCodes.Status400BadRequest);
        var policy = await _dbContext.VehicleInsurancePolicies.SingleOrDefaultAsync(
            x => x.Id == policyId && !x.IsDeleted, cancellationToken) ?? throw NotFound();
        var reviewedAtUtc = _dateTimeProvider.UtcNow;
        policy.VerificationStatus = status;
        policy.ReviewedByUserId = staffUserId;
        policy.ReviewedAtUtc = reviewedAtUtc;
        policy.UpdatedAtUtc = reviewedAtUtc;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(policy);
    }

    public async Task DeleteAsync(Guid ownerUserId, long vehicleId, long policyId, CancellationToken cancellationToken)
    {
        await EnsureOwnerAsync(ownerUserId, vehicleId, cancellationToken);
        var policy = await _dbContext.VehicleInsurancePolicies.SingleOrDefaultAsync(x => x.Id == policyId && x.VehicleId == vehicleId && !x.IsDeleted, cancellationToken)
            ?? throw NotFound();
        policy.IsDeleted = true;
        policy.UpdatedAtUtc = _dateTimeProvider.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureOwnerAsync(Guid userId, long vehicleId, CancellationToken cancellationToken)
    {
        if (!await _dbContext.Vehicles.AnyAsync(x => x.Id == vehicleId && x.OwnerUserId == userId && !x.IsDeleted, cancellationToken))
            throw new BookingException("vehicle.not_found", "Không tìm thấy phương tiện.", StatusCodes.Status404NotFound);
    }

    private static void Validate(VehicleInsurancePolicyRequest request)
    {
        if (request.ExpiresAtUtc <= request.EffectiveFromUtc || request.CoverageAmount < 0 || request.Deductible < 0)
            throw new BookingException("insurance.invalid_policy", "Thông tin hợp đồng bảo hiểm không hợp lệ.", StatusCodes.Status400BadRequest);
        if (string.IsNullOrWhiteSpace(request.Provider) || string.IsNullOrWhiteSpace(request.PolicyNumber))
            throw new BookingException("insurance.required_fields", "Nhà cung cấp và số hợp đồng là bắt buộc.", StatusCodes.Status400BadRequest);
    }

    private static BookingException NotFound() => new(
        "insurance.not_found", "Không tìm thấy hợp đồng bảo hiểm.", StatusCodes.Status404NotFound);

    private static VehicleInsurancePolicyResponse Map(VehicleInsurancePolicy x) => new(
        x.Id,
        x.VehicleId,
        x.InsuranceType,
        x.Provider,
        x.PolicyNumber,
        x.EffectiveFromUtc,
        x.ExpiresAtUtc,
        x.CoverageAmount,
        x.Deductible,
        x.DocumentUrl,
        x.VerificationStatus,
        x.ReviewedByUserId,
        x.ReviewedAtUtc);
}
