using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.RiskProtection;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class PreTripVehicleCheckService : IPreTripVehicleCheckService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IRiskProtectionPolicyProvider _policyProvider;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PreTripVehicleCheckService(
        ApplicationDbContext dbContext,
        IRiskProtectionPolicyProvider policyProvider,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _policyProvider = policyProvider;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<PreTripVehicleCheckResponse> CreateAsync(
        Guid driverId,
        long tripId,
        PreTripVehicleCheckRequest request,
        StoredPreTripVehicleCheckEvidence? evidence,
        CancellationToken cancellationToken)
    {
        await EnsureCanCreateAsync(driverId, tripId, cancellationToken);

        var passed = request.BrakeResponsePassed
            && request.FrontRearLightsPassed
            && request.TurnSignalsPassed
            && request.VisibleTiresPassed
            && request.DashboardWarningPassed
            && request.WindshieldVisibilityPassed
            && request.NoMajorVisibleIssue;
        if (!passed && request.FaultType is null)
        {
            throw new BookingException(
                "pretrip.fault_type_required",
                "Phải chọn loại lỗi khi kiểm tra không đạt.",
                StatusCodes.Status400BadRequest);
        }
        if (request.FaultType is not null && !Enum.IsDefined(request.FaultType.Value))
        {
            throw InvalidRequest("Loại lỗi kiểm tra an toàn không hợp lệ.");
        }

        var note = NormalizeOptional(request.Note);
        if (!string.IsNullOrWhiteSpace(request.EvidenceUrl))
        {
            throw new BookingException(
                "pretrip.external_evidence_not_allowed",
                "Bằng chứng phải được tải lên trực tiếp để hệ thống xác minh tệp.",
                StatusCodes.Status400BadRequest);
        }
        if (note?.Length > 1000)
        {
            throw InvalidRequest("Ghi chú kiểm tra an toàn không được vượt quá 1.000 ký tự.");
        }

        var check = new PreTripVehicleCheck
        {
            TripId = tripId,
            DriverId = driverId,
            BrakeResponsePassed = request.BrakeResponsePassed,
            FrontRearLightsPassed = request.FrontRearLightsPassed,
            TurnSignalsPassed = request.TurnSignalsPassed,
            VisibleTiresPassed = request.VisibleTiresPassed,
            DashboardWarningPassed = request.DashboardWarningPassed,
            WindshieldVisibilityPassed = request.WindshieldVisibilityPassed,
            NoMajorVisibleIssue = request.NoMajorVisibleIssue,
            Result = passed ? PreTripCheckResult.PASS : PreTripCheckResult.FAIL,
            FaultType = passed ? null : request.FaultType,
            Note = note,
            EvidenceUrl = evidence?.FileUrl,
            EvidenceStoragePublicId = evidence?.StoragePublicId,
            EvidenceOriginalFileName = evidence?.OriginalFileName,
            EvidenceContentType = evidence?.ContentType,
            EvidenceFileSizeBytes = evidence?.FileSizeBytes,
            CheckedAtUtc = _dateTimeProvider.UtcNow
        };
        _dbContext.PreTripVehicleChecks.Add(check);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(check);
    }

    public async Task EnsureCanCreateAsync(
        Guid driverId,
        long tripId,
        CancellationToken cancellationToken)
    {
        var trip = await _dbContext.Trips.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == tripId && x.DriverId == driverId,
            cancellationToken);
        if (trip is null) throw NotFound();
        if (trip.TripStatus != TripStatus.ARRIVED)
        {
            throw Conflict(
                "Chỉ được kiểm tra an toàn sau khi tài xế đã đến và trước khi bắt đầu chuyến.");
        }
    }

    public async Task<IReadOnlyList<PreTripVehicleCheckResponse>> GetAsync(
        Guid userId,
        bool isManagement,
        long tripId,
        CancellationToken cancellationToken)
    {
        var canAccess = await _dbContext.Trips.AnyAsync(
            x => x.Id == tripId
                && (isManagement || x.DriverId == userId || x.Booking.CustomerId == userId),
            cancellationToken);
        if (!canAccess) throw NotFound();

        return await _dbContext.PreTripVehicleChecks.AsNoTracking()
            .Where(x => x.TripId == tripId)
            .OrderByDescending(x => x.CheckedAtUtc)
            .ThenByDescending(x => x.Id)
            .Select(x => new PreTripVehicleCheckResponse(
                x.Id,
                x.TripId,
                x.DriverId,
                x.BrakeResponsePassed,
                x.FrontRearLightsPassed,
                x.TurnSignalsPassed,
                x.VisibleTiresPassed,
                x.DashboardWarningPassed,
                x.WindshieldVisibilityPassed,
                x.NoMajorVisibleIssue,
                x.Result,
                x.FaultType,
                x.Note,
                x.EvidenceUrl,
                x.EvidenceOriginalFileName,
                x.EvidenceContentType,
                x.EvidenceFileSizeBytes,
                x.CheckedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task EnsureCanStartAndActivateCoverageAsync(
        Guid driverId,
        Trip trip,
        DateTime startedAtUtc,
        CancellationToken cancellationToken)
    {
        if (trip.TripStatus != TripStatus.ARRIVED)
        {
            throw Conflict(
                "Chỉ được kích hoạt bảo vệ khi chuyến đi chuyển từ trạng thái đã đến sang đang thực hiện.");
        }

        var policy = await _policyProvider.GetEffectivePolicyAsync(startedAtUtc, cancellationToken);
        if (!policy.RiskFundEnabled) return;

        var check = await _dbContext.PreTripVehicleChecks
            .Where(x => x.TripId == trip.Id && x.DriverId == driverId)
            .OrderByDescending(x => x.CheckedAtUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (check?.Result != PreTripCheckResult.PASS)
        {
            throw new BookingException(
                "pretrip.pass_required",
                "Tài xế phải hoàn tất kiểm tra an toàn đạt yêu cầu trước khi bắt đầu chuyến.",
                StatusCodes.Status409Conflict);
        }

        if (await _dbContext.TripProtectionCoverages.AnyAsync(
                x => x.TripId == trip.Id,
                cancellationToken))
        {
            return;
        }

        var insurance = await _dbContext.VehicleInsurancePolicies
            .Where(x => x.VehicleId == trip.Booking.VehicleId
                && !x.IsDeleted
                && x.VerificationStatus == InsuranceVerificationStatus.VERIFIED
                && x.EffectiveFromUtc <= startedAtUtc
                && x.ExpiresAtUtc > startedAtUtc)
            .OrderByDescending(x => x.CoverageAmount)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        _dbContext.TripProtectionCoverages.Add(new TripProtectionCoverage
        {
            TripId = trip.Id,
            PolicyVersionId = policy.Id,
            PreTripVehicleCheckId = check.Id,
            ProtectionLimit = policy.DefaultProtectionLimit,
            VehicleInsurancePolicyId = insurance?.Id,
            InsuranceProviderSnapshot = insurance?.Provider,
            PolicyNumberSnapshot = insurance?.PolicyNumber,
            InsuranceCoverageSnapshot = insurance?.CoverageAmount,
            InsuranceDeductibleSnapshot = insurance?.Deductible,
            ActivatedAtUtc = startedAtUtc
        });
    }

    private static PreTripVehicleCheckResponse Map(PreTripVehicleCheck check) =>
        new(
            check.Id,
            check.TripId,
            check.DriverId,
            check.BrakeResponsePassed,
            check.FrontRearLightsPassed,
            check.TurnSignalsPassed,
            check.VisibleTiresPassed,
            check.DashboardWarningPassed,
            check.WindshieldVisibilityPassed,
            check.NoMajorVisibleIssue,
            check.Result,
            check.FaultType,
            check.Note,
            check.EvidenceUrl,
            check.EvidenceOriginalFileName,
            check.EvidenceContentType,
            check.EvidenceFileSizeBytes,
            check.CheckedAtUtc);

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static BookingException InvalidRequest(string detail) => new(
        "pretrip.invalid_request",
        detail,
        StatusCodes.Status400BadRequest);

    private static BookingException NotFound() => new(
        "trip.not_found",
        "Không tìm thấy chuyến đi.",
        StatusCodes.Status404NotFound);

    private static BookingException Conflict(string detail) => new(
        "pretrip.invalid_trip_status",
        detail,
        StatusCodes.Status409Conflict);
}
