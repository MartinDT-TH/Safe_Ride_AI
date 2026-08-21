using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.RiskProtection;
using SafeRide.Domain.Entities;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class RiskProtectionPolicyProvider : IRiskProtectionPolicyProvider
{
    private readonly ApplicationDbContext _dbContext;

    public RiskProtectionPolicyProvider(ApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<RiskProtectionPolicyVersion> GetEffectivePolicyAsync(DateTime utcNow, CancellationToken cancellationToken)
        => await _dbContext.RiskProtectionPolicyVersions
            .Where(x => x.EffectiveFromUtc <= utcNow)
            .OrderByDescending(x => x.EffectiveFromUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BookingException(
                "risk_protection.configuration_missing",
                "SafeRide chưa có cấu hình tài chính hiệu lực.",
                StatusCodes.Status409Conflict);

    public async Task<RiskProtectionPolicyResponse?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var policy = await _dbContext.RiskProtectionPolicyVersions
            .AsNoTracking()
            .Where(x => x.EffectiveFromUtc <= DateTime.UtcNow)
            .OrderByDescending(x => x.EffectiveFromUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return policy is null ? null : Map(policy);
    }

    public async Task<IReadOnlyList<RiskProtectionPolicyResponse>> ListAsync(
        CancellationToken cancellationToken) =>
        await _dbContext.RiskProtectionPolicyVersions.AsNoTracking()
            .OrderByDescending(x => x.EffectiveFromUtc)
            .ThenByDescending(x => x.Id)
            .Select(x => new RiskProtectionPolicyResponse(
                x.Id, x.EffectiveFromUtc, x.BasePlatformCommissionRate, x.RiskReserveRate,
                x.DefaultProtectionLimit, x.DriverOrdinaryNegligenceRate,
                x.DriverOrdinaryNegligenceCap, x.DriverGrossNegligenceRate,
                x.DriverGrossNegligenceCap, x.MockInsuranceCoverageLimit,
                x.ClaimAutoApprovalThreshold, x.RiskFundEnabled,
                x.ChangeReason, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

    public async Task<RiskProtectionPolicyResponse> CreateAsync(
        Guid adminUserId,
        CreateRiskProtectionPolicyRequest request,
        CancellationToken cancellationToken)
    {
        if (!_dbContext.Database.IsRelational())
            return await CreateWithinTransactionAsync(adminUserId, request, cancellationToken);

        if (_dbContext.Database.CurrentTransaction is not null)
        {
            if (_dbContext.Database.CurrentTransaction.GetDbTransaction().IsolationLevel
                != IsolationLevel.Serializable)
                throw new InvalidOperationException(
                    "Risk Protection policy mutations require an existing serializable transaction.");
            return await CreateWithinTransactionAsync(adminUserId, request, cancellationToken);
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var result = await CreateWithinTransactionAsync(adminUserId, request, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private async Task<RiskProtectionPolicyResponse> CreateWithinTransactionAsync(
        Guid adminUserId,
        CreateRiskProtectionPolicyRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);
        if (await _dbContext.RiskProtectionPolicyVersions.AnyAsync(
                x => x.EffectiveFromUtc == request.EffectiveFromUtc, cancellationToken))
            throw new BookingException(
                "risk_protection.effective_time_exists",
                "Đã có cấu hình bắt đầu tại thời điểm này.",
                StatusCodes.Status409Conflict);

        var policy = new RiskProtectionPolicyVersion
        {
            EffectiveFromUtc = request.EffectiveFromUtc.ToUniversalTime(),
            BasePlatformCommissionRate = request.BasePlatformCommissionRate,
            RiskReserveRate = request.RiskReserveRate,
            DefaultProtectionLimit = request.DefaultProtectionLimit,
            DriverOrdinaryNegligenceRate = request.DriverOrdinaryNegligenceRate,
            DriverOrdinaryNegligenceCap = request.DriverOrdinaryNegligenceCap,
            DriverGrossNegligenceRate = request.DriverGrossNegligenceRate,
            DriverGrossNegligenceCap = request.DriverGrossNegligenceCap,
            MockInsuranceCoverageLimit = request.MockInsuranceCoverageLimit,
            ClaimAutoApprovalThreshold = request.ClaimAutoApprovalThreshold,
            RiskFundEnabled = request.RiskFundEnabled,
            CreatedByUserId = adminUserId,
            ChangeReason = request.ChangeReason.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
        _dbContext.RiskProtectionPolicyVersions.Add(policy);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(policy);
    }

    private static void Validate(CreateRiskProtectionPolicyRequest request)
    {
        if (request.EffectiveFromUtc.Kind == DateTimeKind.Unspecified)
            throw Invalid("Thời điểm hiệu lực phải có múi giờ UTC.");
        if (request.EffectiveFromUtc.ToUniversalTime() < DateTime.UtcNow.AddMinutes(-1))
            throw Invalid("Không được tạo policy version có hiệu lực hồi tố.");
        if (request.BasePlatformCommissionRate is < 0 or > 1 || request.RiskReserveRate is < 0 or > 1
            || request.DriverOrdinaryNegligenceRate is < 0 or > 1 || request.DriverGrossNegligenceRate is < 0 or > 1)
            throw Invalid("Các tỷ lệ phải nằm trong khoảng từ 0 đến 1.");
        if (request.DefaultProtectionLimit < 0 || request.DriverOrdinaryNegligenceCap < 0
            || request.DriverGrossNegligenceCap < 0 || request.MockInsuranceCoverageLimit < 0
            || request.ClaimAutoApprovalThreshold < 0)
            throw Invalid("Các hạn mức không được âm.");
        if (string.IsNullOrWhiteSpace(request.ChangeReason))
            throw Invalid("Lý do thay đổi cấu hình là bắt buộc.");
    }

    private static BookingException Invalid(string detail) => new(
        "risk_protection.invalid_configuration", detail, StatusCodes.Status400BadRequest);

    private static RiskProtectionPolicyResponse Map(RiskProtectionPolicyVersion policy) => new(
        policy.Id, policy.EffectiveFromUtc, policy.BasePlatformCommissionRate, policy.RiskReserveRate,
        policy.DefaultProtectionLimit, policy.DriverOrdinaryNegligenceRate,
        policy.DriverOrdinaryNegligenceCap, policy.DriverGrossNegligenceRate,
        policy.DriverGrossNegligenceCap, policy.MockInsuranceCoverageLimit,
        policy.ClaimAutoApprovalThreshold, policy.RiskFundEnabled,
        policy.ChangeReason, policy.CreatedAtUtc);
}
