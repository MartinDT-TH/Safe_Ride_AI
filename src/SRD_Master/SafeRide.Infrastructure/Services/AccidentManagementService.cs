using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Realtime;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.RiskProtection;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class AccidentManagementService : IAccidentManagementService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IClaimSettlementCalculator _claimCalculator;
    private readonly RiskFundLedgerService _riskFundLedger;
    private readonly IInsuranceProvider _insuranceProvider;
    private readonly IAccidentRealtimeService _realtime;
    private readonly ILogger<AccidentManagementService> _logger;

    public AccidentManagementService(
        ApplicationDbContext dbContext,
        IClaimSettlementCalculator claimCalculator,
        RiskFundLedgerService riskFundLedger,
        IInsuranceProvider insuranceProvider,
        IAccidentRealtimeService realtime,
        ILogger<AccidentManagementService> logger)
    {
        _dbContext = dbContext;
        _claimCalculator = claimCalculator;
        _riskFundLedger = riskFundLedger;
        _insuranceProvider = insuranceProvider;
        _realtime = realtime;
        _logger = logger;
    }

    public async Task<AccidentResponse> CreateAsync(
        Guid userId, bool isManagement, long tripId, CreateAccidentRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Category)) throw Invalid("Loại tai nạn không hợp lệ.");
        var description = request.Description?.Trim();
        var policeReference = request.PoliceReportReference?.Trim();
        if (string.IsNullOrWhiteSpace(description)) throw Invalid("Mô tả tai nạn là bắt buộc.");
        if (description.Length > 4000) throw Invalid("Mô tả tai nạn không được vượt quá 4.000 ký tự.");
        if (policeReference?.Length > 200) throw Invalid("Tham chiếu biên bản cảnh sát không được vượt quá 200 ký tự.");
        ValidateCoordinates(request.Latitude, request.Longitude);
        var occurredAtUtc = NormalizeUtc(request.OccurredAtUtc);
        var now = DateTime.UtcNow;
        if (occurredAtUtc == default || occurredAtUtc > now.AddMinutes(5)) throw Invalid("Thời điểm tai nạn không hợp lệ.");
        var trip = await _dbContext.Trips
            .Include(x => x.Booking)
            .SingleOrDefaultAsync(x => x.Id == tripId, cancellationToken)
            ?? throw new BookingException("trip.not_found", "Không tìm thấy chuyến đi.", StatusCodes.Status404NotFound);
        if (!isManagement && trip.DriverId != userId && trip.Booking.CustomerId != userId)
            throw new BookingException("trip.not_found", "Không tìm thấy chuyến đi.", StatusCodes.Status404NotFound);
        if (trip.StartedAt is null)
            throw Conflict("Chỉ có thể báo tai nạn sau khi chuyến đi đã bắt đầu.");
        if (occurredAtUtc < NormalizeUtc(trip.StartedAt.Value).AddMinutes(-5))
            throw Invalid("Thời điểm tai nạn phải thuộc chuyến đi đã bắt đầu.");
        var tripEndedAt = trip.SafetyTerminatedAt ?? trip.EndedAt ?? trip.CompletedAt;
        if (tripEndedAt is not null && occurredAtUtc > NormalizeUtc(tripEndedAt.Value).AddMinutes(5))
            throw Invalid("Thời điểm tai nạn không được nằm sau khi chuyến đi đã kết thúc.");
        var hasEligibleCoverage = await _dbContext.TripProtectionCoverages
            .AsNoTracking()
            .Include(x => x.PolicyVersion)
            .AnyAsync(x => x.TripId == tripId
                && x.ActivatedAtUtc <= occurredAtUtc
                && x.ProtectionLimit > 0m
                && x.PolicyVersion.RiskFundEnabled,
                cancellationToken);
        if (!hasEligibleCoverage)
            throw Conflict("Chuyến đi không có protection coverage hợp lệ tại thời điểm tai nạn.");

        var accident = new AccidentReport
        {
            TripId = tripId,
            ReportedByUserId = userId,
            Category = request.Category,
            Status = AccidentStatus.REPORTED,
            OccurredAtUtc = occurredAtUtc,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Description = description,
            PoliceReportReference = string.IsNullOrWhiteSpace(policeReference) ? null : policeReference,
            CreatedAtUtc = now
        };
        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            _dbContext.AccidentReports.Add(accident);
            await _dbContext.SaveChangesAsync(cancellationToken);
            var recipients = isManagement
                ? new[] { trip.DriverId, trip.Booking.CustomerId }
                : new[] { trip.DriverId == userId ? trip.Booking.CustomerId : trip.DriverId };
            _dbContext.Notifications.AddRange(recipients.Distinct().Select(recipientId => new Notification
            {
                UserId = recipientId,
                Title = "Đã ghi nhận báo cáo tai nạn",
                Content = "Một bên tham gia chuyến đi đã gửi báo cáo tai nạn. Vui lòng theo dõi cập nhật trong hồ sơ.",
                NotificationType = "AccidentReported",
                ReferenceId = accident.Id,
                SentAt = now
            }));
            await _dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        try
        {
            await _realtime.PublishAccidentCreatedAsync(new AccidentCreatedEvent(
                accident.Id, accident.TripId, accident.ReportedByUserId, accident.Category,
                accident.Status, accident.OccurredAtUtc, accident.CreatedAtUtc), cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Could not publish realtime notification for accident {AccidentId}.", accident.Id);
        }
        return Map(accident);
    }

    public async Task<AccidentResponse> GetAsync(
        Guid userId, bool isManagement, long accidentId, CancellationToken cancellationToken)
    {
        var accident = await _dbContext.AccidentReports.AsNoTracking()
            .Include(x => x.Trip).ThenInclude(x => x.Booking)
            .Include(x => x.Evidence)
            .Include(x => x.ProtectionClaim)
            .Include(x => x.LiabilityAssessment).ThenInclude(x => x!.Causes)
            .Include(x => x.LiabilityAssessment).ThenInclude(x => x!.Disputes)
                .ThenInclude(x => x.Evidence)
            .SingleOrDefaultAsync(x => x.Id == accidentId, cancellationToken)
            ?? throw NotFound();
        if (!isManagement && accident.Trip.DriverId != userId && accident.Trip.Booking.CustomerId != userId)
            throw NotFound();
        var canSeeAssessment = isManagement
            || accident.LiabilityAssessment?.Status is LiabilityAssessmentStatus.CONFIRMED
                or LiabilityAssessmentStatus.DISPUTED;
        return Map(accident, canSeeAssessment);
    }

    public async Task EnsureCanUploadEvidenceAsync(
        Guid userId, bool isManagement, long accidentId, CancellationToken cancellationToken)
    {
        var accident = await _dbContext.AccidentReports.AsNoTracking()
            .Include(x => x.Trip).ThenInclude(x => x.Booking)
            .SingleOrDefaultAsync(x => x.Id == accidentId, cancellationToken)
            ?? throw NotFound();
        if (!isManagement && accident.Trip.DriverId != userId && accident.Trip.Booking.CustomerId != userId)
            throw NotFound();
        EnsureEvidenceCollectionOpen(accident.Status);
        if (await _dbContext.AccidentEvidence.CountAsync(
                x => x.AccidentReportId == accidentId, cancellationToken) >= 20)
            throw Conflict(
                "accident.evidence_limit_reached",
                "Mỗi hồ sơ tai nạn chỉ nhận tối đa 20 tệp bằng chứng.");
        if (await _dbContext.AccidentEvidence.CountAsync(
                x => x.AccidentReportId == accidentId, cancellationToken) >= 20)
            throw Conflict("Mỗi accident chỉ hỗ trợ tối đa 20 tệp bằng chứng trong MVP.");
    }

    public async Task<AccidentEvidenceResponse> AddEvidenceAsync(
        Guid userId, bool isManagement, long accidentId, AddAccidentEvidenceRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.EvidenceType)) throw Invalid("Loại bằng chứng không hợp lệ.");
        if (string.IsNullOrWhiteSpace(request.FileUrl) || string.IsNullOrWhiteSpace(request.OriginalFileName)
            || string.IsNullOrWhiteSpace(request.ContentType)
            || string.IsNullOrWhiteSpace(request.StoragePublicId)
            || request.FileSizeBytes is null or <= 0)
            throw Invalid("Tệp bằng chứng và content type là bắt buộc.");
        if (request.FileSizeBytes > 10_000_000)
            throw Invalid("Tệp bằng chứng không được vượt quá 10 MB.");
        if (!IsHttpUrl(request.FileUrl))
            throw Invalid("Đường dẫn lưu trữ bằng chứng không hợp lệ.");
        var allowed = IsAllowedEvidenceContentType(request.ContentType);
        if (!allowed) throw Invalid("MVP chỉ hỗ trợ hình ảnh và tài liệu PDF.");
        if (request.FileUrl.Length > 1000 || request.OriginalFileName.Length > 255
            || request.StoragePublicId?.Length > 500 || request.ContentType.Length > 100
            || request.Description?.Trim().Length > 1000)
            throw Invalid("Metadata bằng chứng vượt quá độ dài cho phép.");
        ValidateCoordinates(request.Latitude, request.Longitude);
        DateTime? capturedAtUtc = request.CapturedAtUtc is null
            ? null
            : NormalizeUtc(request.CapturedAtUtc.Value);
        if (capturedAtUtc is not null
            && (capturedAtUtc.Value == default || capturedAtUtc.Value > DateTime.UtcNow.AddMinutes(5)))
            throw Invalid("Thời điểm ghi nhận bằng chứng không hợp lệ.");
        var accident = await _dbContext.AccidentReports.Include(x => x.Trip).ThenInclude(x => x.Booking)
            .SingleOrDefaultAsync(x => x.Id == accidentId, cancellationToken) ?? throw NotFound();
        if (!isManagement && accident.Trip.DriverId != userId && accident.Trip.Booking.CustomerId != userId)
            throw NotFound();
        EnsureEvidenceCollectionOpen(accident.Status);
        var occupiedSlots = await _dbContext.AccidentEvidence
            .Where(x => x.AccidentReportId == accidentId)
            .Select(x => x.SequenceNumber)
            .ToListAsync(cancellationToken);
        var sequenceNumber = Enumerable.Range(1, 20)
            .FirstOrDefault(slot => !occupiedSlots.Contains(slot));
        if (sequenceNumber == 0)
            throw Conflict(
                "accident.evidence_limit_reached",
                "Mỗi hồ sơ tai nạn chỉ nhận tối đa 20 tệp bằng chứng.");
        if (sequenceNumber == 0)
            throw Conflict("Mỗi accident chỉ hỗ trợ tối đa 20 tệp bằng chứng trong MVP.");
        var evidence = new AccidentEvidence
        {
            AccidentReportId = accidentId,
            SequenceNumber = sequenceNumber,
            UploadedByUserId = userId,
            EvidenceType = request.EvidenceType,
            FileUrl = request.FileUrl.Trim(),
            OriginalFileName = request.OriginalFileName.Trim(),
            ContentType = request.ContentType.Trim(),
            StoragePublicId = request.StoragePublicId?.Trim(),
            FileSizeBytes = request.FileSizeBytes,
            CapturedAtUtc = capturedAtUtc,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Description = request.Description?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
        _dbContext.AccidentEvidence.Add(evidence);
        if (accident.Status == AccidentStatus.REPORTED) accident.Status = AccidentStatus.EVIDENCE_COLLECTION;
        accident.UpdatedAtUtc = DateTime.UtcNow;
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            throw Conflict(
                "accident.evidence_limit_reached",
                "Không thể nhận thêm bằng chứng vì giới hạn tải lên đồng thời đã đạt.");
        }
        return Map(evidence);
    }

    public async Task<IReadOnlyList<AccidentResponse>> GetStaffQueueAsync(
        AccidentQueueFilter filter, CancellationToken cancellationToken)
    {
        if (filter.Limit is < 1 or > 200) throw Invalid("Giới hạn hàng đợi phải từ 1 đến 200.");
        if (filter.FromUtc is not null && filter.ToUtc is not null && filter.FromUtc > filter.ToUtc)
            throw Invalid("Khoảng thời gian lọc không hợp lệ.");
        return await _dbContext.AccidentReports.AsNoTracking()
            .Where(x => filter.Status == null || x.Status == filter.Status)
            .Where(x => filter.Category == null || x.Category == filter.Category)
            .Where(x => filter.TripId == null || x.TripId == filter.TripId)
            .Where(x => filter.FromUtc == null || x.CreatedAtUtc >= filter.FromUtc)
            .Where(x => filter.ToUtc == null || x.CreatedAtUtc <= filter.ToUtc)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(filter.Limit)
            .Select(x => new AccidentResponse(x.Id, x.TripId, x.ReportedByUserId, x.Category, x.Status,
                x.OccurredAtUtc, x.Latitude, x.Longitude, x.Description, x.PoliceReportReference,
                x.CreatedAtUtc, x.ProtectionClaim == null ? null : x.ProtectionClaim.Id,
                x.ProtectionClaim == null ? null : x.ProtectionClaim.Status, null, null, null))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProtectionClaimResponse> SaveAssessmentAsync(
        Guid staffUserId, long accidentId, LiabilityAssessmentRequest request, bool confirm,
        CancellationToken cancellationToken)
    {
        ValidateAssessment(request);
        var accident = await _dbContext.AccidentReports
            .Include(x => x.LiabilityAssessment).ThenInclude(x => x!.Causes)
            .Include(x => x.ProtectionClaim)
            .Include(x => x.Trip).ThenInclude(x => x.Booking)
            .SingleOrDefaultAsync(x => x.Id == accidentId, cancellationToken) ?? throw NotFound();
        var assessment = accident.LiabilityAssessment;
        if (assessment?.Status == LiabilityAssessmentStatus.CONFIRMED)
            throw Conflict("Liability assessment đã được xác nhận và không thể ghi đè.");
        if (assessment is not null)
            ApplyExpectedRowVersion(assessment, request.RowVersion);
        assessment ??= new AccidentLiabilityAssessment
        {
            AccidentReportId = accidentId,
            CreatedAtUtc = DateTime.UtcNow
        };
        if (assessment.Id == 0) _dbContext.AccidentLiabilityAssessments.Add(assessment);
        assessment.DriverFaultPercentage = request.DriverFaultPercentage;
        assessment.CustomerFaultPercentage = request.CustomerFaultPercentage;
        assessment.ThirdPartyFaultPercentage = request.ThirdPartyFaultPercentage;
        assessment.VehicleFailurePercentage = request.VehicleFailurePercentage;
        assessment.ObjectiveCausePercentage = request.ObjectiveCausePercentage;
        assessment.DriverFaultLevel = request.DriverFaultLevel;
        assessment.VehicleDefectAwareness = request.VehicleDefectAwareness;
        assessment.Status = confirm ? LiabilityAssessmentStatus.CONFIRMED : LiabilityAssessmentStatus.DRAFT;
        assessment.ConfirmedByUserId = confirm ? staffUserId : null;
        assessment.ConfirmedAtUtc = confirm ? DateTime.UtcNow : null;
        assessment.UpdatedAtUtc = DateTime.UtcNow;
        _dbContext.AccidentLiabilityCauses.RemoveRange(assessment.Causes);
        assessment.Causes = request.Causes.Select(x => new AccidentLiabilityCause
        {
            RootCause = x.RootCause,
            ResponsibleParty = x.ResponsibleParty,
            Percentage = x.Percentage
        }).ToList();
        accident.Status = confirm ? AccidentStatus.SETTLEMENT : AccidentStatus.LIABILITY_PENDING;
        accident.UpdatedAtUtc = DateTime.UtcNow;
        var claim = accident.ProtectionClaim ?? new ProtectionClaim
        {
            AccidentReportId = accidentId,
            Status = confirm ? ProtectionClaimStatus.UNDER_REVIEW : ProtectionClaimStatus.DRAFT,
            CreatedAtUtc = DateTime.UtcNow
        };
        if (claim.Id == 0) _dbContext.ProtectionClaims.Add(claim);
        else claim.Status = confirm ? ProtectionClaimStatus.UNDER_REVIEW : ProtectionClaimStatus.DRAFT;
        if (confirm)
            QueueParticipantNotifications(
                accident, "AccidentLiabilityConfirmed", "Kết quả đánh giá trách nhiệm",
                "Đánh giá trách nhiệm của sự cố đã được Staff xác nhận.");
        await SaveChangesWithConcurrencyAsync(cancellationToken);
        return Map(claim, assessment.RowVersion);
    }

    public async Task<ProtectionClaimResponse> CalculateClaimAsync(
        Guid staffUserId, long claimId, CalculateClaimRequest request, CancellationToken cancellationToken)
    {
        if (request.TotalDamageAmount < 0 || request.EligibleDamageAmount < 0
            || request.EligibleDamageAmount > request.TotalDamageAmount
            || request.RequestedInsuranceAmount < 0 || request.RequestedRiskFundAmount < 0)
            throw Invalid("Các giá trị settlement không hợp lệ.");
        if (!Enum.IsDefined(request.InsurancePaymentDestination))
            throw Invalid("Hình thức thanh toán bảo hiểm không hợp lệ.");
        var claim = await _dbContext.ProtectionClaims
            .Include(x => x.AccidentReport).ThenInclude(x => x.LiabilityAssessment)
            .Include(x => x.AccidentReport).ThenInclude(x => x.Trip).ThenInclude(x => x.Booking)
            .Include(x => x.DriverLiabilities)
            .SingleOrDefaultAsync(x => x.Id == claimId, cancellationToken) ?? throw ClaimNotFound();
        var assessment = claim.AccidentReport.LiabilityAssessment;
        if (assessment?.Status != LiabilityAssessmentStatus.CONFIRMED)
            throw Conflict("Phải xác nhận liability assessment trước khi tính claim.");
        if (claim.Status is ProtectionClaimStatus.FUNDED
            or ProtectionClaimStatus.RECOVERY_IN_PROGRESS
            or ProtectionClaimStatus.SETTLED
            or ProtectionClaimStatus.CLOSED)
            throw Conflict("Không thể tính lại claim sau khi đã cấp vốn hoặc ghi nhận recovery.");
        ApplyExpectedRowVersion(claim, request.RowVersion);
        var coverage = await _dbContext.TripProtectionCoverages
            .Include(x => x.PolicyVersion)
            .SingleOrDefaultAsync(x => x.TripId == claim.AccidentReport.TripId, cancellationToken)
            ?? throw Conflict("Chuyến đi không có protection coverage snapshot hợp lệ.");
        var policy = coverage.PolicyVersion;
        if (request.RequestedRiskFundAmount > coverage.ProtectionLimit)
            throw Invalid("Khoản yêu cầu Risk Fund vượt protection limit đã snapshot của chuyến đi.");
        var liabilities = _claimCalculator.CalculateLiabilities(new ClaimLiabilityCalculationInput(
            request.EligibleDamageAmount,
            assessment.DriverFaultPercentage,
            assessment.CustomerFaultPercentage,
            assessment.ThirdPartyFaultPercentage,
            assessment.DriverFaultLevel,
            policy.DriverOrdinaryNegligenceRate, policy.DriverOrdinaryNegligenceCap,
            policy.DriverGrossNegligenceRate, policy.DriverGrossNegligenceCap));

        claim.TotalDamageAmount = Round(request.TotalDamageAmount);
        claim.EligibleDamageAmount = Round(request.EligibleDamageAmount);
        claim.InsuranceRequestedAmount = Round(request.RequestedInsuranceAmount);
        claim.InsurancePaymentDestination = request.InsurancePaymentDestination;
        claim.InsuranceApprovedAmount = 0m;
        claim.InsurancePaidDirectToClaimant = 0m;
        claim.InsuranceReimbursedToRiskFund = 0m;
        claim.DriverLiabilityAmount = liabilities.Driver.LiabilityAmount;
        claim.CustomerLiabilityAmount = liabilities.CustomerLiabilityAmount;
        claim.ThirdPartyLiabilityAmount = liabilities.ThirdPartyLiabilityAmount;
        var requestedRiskFundAmount = Round(request.RequestedRiskFundAmount);
        var recoverableLiability = Round(liabilities.TotalRecoverableLiabilityAmount);
        claim.RiskFundAdvanceAmount = request.IsPermanentRiskFundLoss
            ? 0m
            : Math.Min(requestedRiskFundAmount, recoverableLiability);
        claim.RiskFundPermanentLossAmount = request.IsPermanentRiskFundLoss
            ? requestedRiskFundAmount
            : requestedRiskFundAmount - claim.RiskFundAdvanceAmount;
        claim.RecoveredAmount = 0m;
        claim.WrittenOffAdvanceAmount = 0m;
        claim.OutstandingRecoveryAmount = Math.Min(
            claim.RiskFundAdvanceAmount,
            liabilities.TotalRecoverableLiabilityAmount);
        claim.UpdatedAtUtc = DateTime.UtcNow;

        var liability = claim.DriverLiabilities.SingleOrDefault();
        liability ??= new DriverLiability
        {
            ProtectionClaimId = claim.Id,
            DriverId = claim.AccidentReport.Trip.DriverId,
            CreatedAtUtc = DateTime.UtcNow
        };
        if (liability.Id == 0) _dbContext.DriverLiabilities.Add(liability);
        liability.DriverAttributableEligibleDamage = liabilities.Driver.DriverAttributableEligibleDamage;
        liability.FaultLevel = assessment.DriverFaultLevel;
        liability.AppliedRate = liabilities.Driver.AppliedRate;
        liability.AppliedCap = liabilities.Driver.AppliedCap;
        liability.ConfirmedAmount = liabilities.Driver.LiabilityAmount;
        liability.OutstandingAmount = Math.Max(0m, liabilities.Driver.LiabilityAmount - liability.PaidAmount);
        liability.Status = liability.OutstandingAmount == 0 ? DriverLiabilityStatus.PAID : DriverLiabilityStatus.CONFIRMED;
        liability.DisputeReason = null;
        liability.UpdatedAtUtc = DateTime.UtcNow;

        decimal insuranceAllocationForValidation = 0m;
        decimal insuranceRecoveryCapacity = 0m;
        if (claim.InsuranceRequestedAmount > 0)
        {
            var calculation = await _insuranceProvider.CalculateClaimAsync(
                CreateInsuranceCalculationContext(claim, coverage, policy),
                cancellationToken);
            AddInsuranceCalculationAudit(claim, calculation, staffUserId);
            if (request.InsurancePaymentDestination == InsurancePaymentDestination.REIMBURSE_RISK_FUND)
                insuranceRecoveryCapacity = calculation.ApprovedAmount;
            var submission = await _insuranceProvider.SubmitClaimAsync(
                CreateInsuranceSubmissionContext(claim, coverage, policy),
                cancellationToken);
            claim.InsuranceStatus = submission.Status;
            claim.InsuranceReference = submission.Reference;
            insuranceAllocationForValidation = request.InsurancePaymentDestination
                == InsurancePaymentDestination.DIRECT_TO_CLAIMANT
                ? submission.ApprovedAmount
                : 0m;
            if (submission.Status == InsuranceClaimStatus.APPROVED)
            {
                claim.InsuranceApprovedAmount = submission.ApprovedAmount;
                claim.InsurancePaidDirectToClaimant = request.InsurancePaymentDestination
                    == InsurancePaymentDestination.DIRECT_TO_CLAIMANT
                    ? submission.ApprovedAmount
                    : 0m;
            }
            AddInsuranceAudit(claim, InsuranceProviderOperation.SUBMIT, submission, staffUserId);
        }
        else
        {
            claim.InsuranceStatus = InsuranceClaimStatus.NOT_SUBMITTED;
            claim.InsuranceReference = null;
        }

        if (!request.IsPermanentRiskFundLoss)
        {
            claim.RiskFundAdvanceAmount = Math.Min(
                requestedRiskFundAmount,
                Round(liabilities.TotalRecoverableLiabilityAmount + insuranceRecoveryCapacity));
            claim.RiskFundPermanentLossAmount = requestedRiskFundAmount - claim.RiskFundAdvanceAmount;
        }
        var totalRequestedFundAmount = claim.RiskFundAdvanceAmount + claim.RiskFundPermanentLossAmount;
        if (insuranceAllocationForValidation + totalRequestedFundAmount > claim.EligibleDamageAmount)
            throw Invalid("Tổng nguồn chi trả từ bảo hiểm và Risk Fund không được vượt EligibleDamage.");
        claim.OutstandingRecoveryAmount = Math.Min(
            claim.RiskFundAdvanceAmount,
            liabilities.TotalRecoverableLiabilityAmount + insuranceRecoveryCapacity);

        claim.Status = claim.InsuranceStatus == InsuranceClaimStatus.PENDING
            ? ProtectionClaimStatus.UNDER_REVIEW
            : ProtectionClaimStatus.APPROVED;
        claim.TotalPaidToClaimant = claim.InsurancePaidDirectToClaimant;
        QueueParticipantNotifications(
            claim.AccidentReport, "ProtectionClaimCalculated", "Hồ sơ bảo vệ đã được cập nhật",
            "Staff đã hoàn tất bước tính toán hồ sơ bảo vệ. Vui lòng mở chi tiết sự cố để xem trạng thái.");
        await SaveChangesWithConcurrencyAsync(cancellationToken);
        return Map(claim, assessment.RowVersion);
    }

    public async Task<ProtectionClaimResponse> ReviewMockInsuranceAsync(
        Guid staffUserId,
        long claimId,
        InsuranceReviewRequest request,
        bool approve,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reference)
            || string.IsNullOrWhiteSpace(request.Reason)
            || request.ApprovedAmount < 0)
            throw Invalid("Kết quả bảo hiểm phải có tham chiếu, lý do và số tiền hợp lệ.");
        if (!Enum.IsDefined(request.InsurancePaymentDestination))
            throw Invalid("Hình thức thanh toán bảo hiểm không hợp lệ.");
        var claim = await _dbContext.ProtectionClaims
            .Include(x => x.AccidentReport).ThenInclude(x => x.Trip).ThenInclude(x => x.Booking)
            .SingleOrDefaultAsync(x => x.Id == claimId, cancellationToken)
            ?? throw ClaimNotFound();
        if (claim.InsuranceStatus != InsuranceClaimStatus.PENDING)
            throw Conflict("Hồ sơ bảo hiểm không ở trạng thái chờ Staff duyệt.");
        ApplyExpectedRowVersion(claim, request.RowVersion);

        var coverage = await _dbContext.TripProtectionCoverages.AsNoTracking()
            .Include(x => x.PolicyVersion)
            .SingleOrDefaultAsync(x => x.TripId == claim.AccidentReport.TripId, cancellationToken);
        if (coverage is null)
            throw Conflict("Chuyến đi không có protection coverage snapshot hợp lệ.");
        var policy = coverage.PolicyVersion;
        var insuranceLimit = Math.Max(0m,
            (coverage.InsuranceCoverageSnapshot ?? 0m)
            - (coverage.InsuranceDeductibleSnapshot ?? 0m));
        var allowed = Math.Min(claim.InsuranceRequestedAmount,
            Math.Min(claim.EligibleDamageAmount,
                Math.Min(policy.MockInsuranceCoverageLimit, insuranceLimit)));
        if (approve && (request.ApprovedAmount <= 0m || request.ApprovedAmount > allowed))
            throw Invalid("Số tiền duyệt vượt yêu cầu, coverage snapshot hoặc giới hạn mock insurer.");
        var directPayment = approve
            && request.InsurancePaymentDestination == InsurancePaymentDestination.DIRECT_TO_CLAIMANT
            ? request.ApprovedAmount
            : 0m;
        if (directPayment
            + claim.RiskFundAdvanceAmount + claim.RiskFundPermanentLossAmount
            > claim.EligibleDamageAmount)
            throw Invalid("Tổng nguồn chi trả từ bảo hiểm và Risk Fund không được vượt EligibleDamage.");

        var review = await _insuranceProvider.ReviewClaimAsync(
            new InsuranceClaimReviewContext(
                claim.Id,
                claim.InsuranceRequestedAmount,
                claim.EligibleDamageAmount,
                coverage.InsuranceCoverageSnapshot,
                coverage.InsuranceDeductibleSnapshot,
                policy.MockInsuranceCoverageLimit,
                request.ApprovedAmount,
                request.Reference,
                request.Reason),
            approve,
            cancellationToken);
        claim.InsuranceStatus = review.Status;
        claim.InsurancePaymentDestination = request.InsurancePaymentDestination;
        claim.InsuranceApprovedAmount = review.ApprovedAmount;
        claim.InsurancePaidDirectToClaimant = approve
            && request.InsurancePaymentDestination == InsurancePaymentDestination.DIRECT_TO_CLAIMANT
            ? review.ApprovedAmount
            : 0m;
        claim.InsuranceReimbursedToRiskFund = 0m;
        claim.TotalPaidToClaimant = claim.InsurancePaidDirectToClaimant;
        if (approve && request.InsurancePaymentDestination == InsurancePaymentDestination.REIMBURSE_RISK_FUND)
        {
            var liabilityRecoveryCapacity = claim.DriverLiabilityAmount
                + claim.CustomerLiabilityAmount
                + claim.ThirdPartyLiabilityAmount;
            claim.OutstandingRecoveryAmount = Math.Min(
                claim.RiskFundAdvanceAmount,
                liabilityRecoveryCapacity + review.ApprovedAmount);
        }
        claim.InsuranceReference = review.Reference;
        claim.Status = ProtectionClaimStatus.APPROVED;
        AddInsuranceAudit(
            claim,
            approve ? InsuranceProviderOperation.APPROVE : InsuranceProviderOperation.REJECT,
            review,
            staffUserId);
        claim.UpdatedAtUtc = DateTime.UtcNow;
        QueueParticipantNotifications(
            claim.AccidentReport, "ProtectionClaimInsuranceReviewed", "Bảo hiểm đã phản hồi hồ sơ",
            approve
                ? "Khoản bảo hiểm mô phỏng đã được Staff phê duyệt."
                : "Khoản bảo hiểm mô phỏng đã bị từ chối; Staff sẽ tiếp tục xử lý hồ sơ.");
        await SaveChangesWithConcurrencyAsync(cancellationToken);
        return Map(claim);
    }

    public async Task<ProtectionClaimResponse> RefreshMockInsuranceStatusAsync(
        Guid staffUserId,
        long claimId,
        string rowVersion,
        CancellationToken cancellationToken)
    {
        var claim = await _dbContext.ProtectionClaims
            .Include(x => x.AccidentReport).ThenInclude(x => x.Trip)
            .SingleOrDefaultAsync(x => x.Id == claimId, cancellationToken)
            ?? throw ClaimNotFound();
        if (claim.InsuranceStatus == InsuranceClaimStatus.NOT_SUBMITTED
            || string.IsNullOrWhiteSpace(claim.InsuranceReference))
            throw Conflict("Claim chưa được gửi tới nhà cung cấp bảo hiểm.");
        ApplyExpectedRowVersion(claim, rowVersion);

        var coverage = await _dbContext.TripProtectionCoverages.AsNoTracking()
            .Include(x => x.PolicyVersion)
            .SingleOrDefaultAsync(x => x.TripId == claim.AccidentReport.TripId, cancellationToken)
            ?? throw Conflict("Chuyến đi không có protection coverage snapshot hợp lệ.");
        var result = await _insuranceProvider.GetClaimStatusAsync(
            new InsuranceClaimStatusContext(
                claim.Id,
                claim.InsuranceReference,
                claim.InsuranceStatus,
                claim.InsuranceRequestedAmount,
                claim.InsuranceApprovedAmount,
                claim.EligibleDamageAmount,
                coverage.InsuranceCoverageSnapshot,
                coverage.InsuranceDeductibleSnapshot,
                coverage.PolicyVersion.MockInsuranceCoverageLimit),
            cancellationToken);

        claim.InsuranceStatus = result.Status;
        claim.InsuranceReference = result.Reference;
        claim.UpdatedAtUtc = DateTime.UtcNow;
        AddInsuranceAudit(claim, InsuranceProviderOperation.GET_STATUS, result, staffUserId);
        await SaveChangesWithConcurrencyAsync(cancellationToken);
        return Map(claim);
    }

    public async Task<IReadOnlyList<InsuranceProviderAuditResponse>> GetInsuranceAuditsAsync(
        long claimId,
        CancellationToken cancellationToken)
    {
        if (!await _dbContext.ProtectionClaims.AsNoTracking()
                .AnyAsync(x => x.Id == claimId, cancellationToken))
            throw ClaimNotFound();

        return await _dbContext.InsuranceClaimProviderAudits.AsNoTracking()
            .Where(x => x.ProtectionClaimId == claimId)
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .Select(x => new InsuranceProviderAuditResponse(
                x.Id,
                x.Operation,
                x.ResultStatus,
                x.RequestedAmount,
                x.ApprovedAmount,
                x.ProviderReference,
                x.RequestPayload,
                x.ResponsePayload,
                x.PerformedByUserId,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProtectionClaimResponse> FundClaimAsync(
        Guid staffUserId, long claimId, string idempotencyKey, string rowVersion, CancellationToken cancellationToken)
        => await ExecuteRiskFundMutationAsync(
            () => FundClaimCoreAsync(staffUserId, claimId, idempotencyKey, rowVersion, cancellationToken),
            cancellationToken);

    // Kept for focused in-memory service tests; production callers use the interface
    // overload that requires an expected RowVersion.
    public Task<ProtectionClaimResponse> FundClaimAsync(
        Guid staffUserId, long claimId, string idempotencyKey, CancellationToken cancellationToken) =>
        FundClaimAsync(staffUserId, claimId, idempotencyKey, string.Empty, cancellationToken);

    private async Task<ProtectionClaimResponse> FundClaimCoreAsync(
        Guid staffUserId, long claimId, string idempotencyKey, string rowVersion, CancellationToken cancellationToken)
    {
        idempotencyKey = NormalizeRequired(idempotencyKey, 90, "Idempotency key");
        var claim = await _dbContext.ProtectionClaims
            .Include(x => x.AccidentReport).ThenInclude(x => x.Trip).ThenInclude(x => x.Booking)
            .SingleOrDefaultAsync(x => x.Id == claimId, cancellationToken)
            ?? throw ClaimNotFound();

        var amount = Round(claim.RiskFundAdvanceAmount + claim.RiskFundPermanentLossAmount);
        var type = claim.RiskFundAdvanceAmount > 0
            ? RiskFundTransactionType.CLAIM_ADVANCE
            : RiskFundTransactionType.CLAIM_PAYOUT;
        var existingFunding = await _dbContext.RiskFundTransactions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
        if (existingFunding is not null)
        {
            if (existingFunding.ProtectionClaimId != claim.Id
                || existingFunding.TransactionType != type
                || existingFunding.Direction != LedgerDirection.DEBIT
                || existingFunding.Amount != amount
                || existingFunding.PerformedByUserId != staffUserId)
            {
                throw Conflict(
                    "risk_protection.funding_idempotency_conflict",
                    "Idempotency key đã được dùng cho một lệnh cấp vốn khác.");
            }

            if (claim.Status is ProtectionClaimStatus.APPROVED or ProtectionClaimStatus.PENDING_FUNDING)
            {
                CompleteFunding(claim, amount);
                QueueParticipantNotifications(
                    claim.AccidentReport, "ProtectionClaimFunded", "Hồ sơ bảo vệ đã được cấp vốn",
                    "Hồ sơ bảo vệ đã hoàn tất bước cấp vốn.");
                await SaveChangesWithConcurrencyAsync(cancellationToken);
            }

            return Map(claim);
        }

        if (claim.Status is ProtectionClaimStatus.FUNDED or ProtectionClaimStatus.RECOVERY_IN_PROGRESS
            or ProtectionClaimStatus.SETTLED or ProtectionClaimStatus.CLOSED) return Map(claim);
        if (claim.Status is not (ProtectionClaimStatus.APPROVED or ProtectionClaimStatus.PENDING_FUNDING))
            throw Conflict("Claim chưa sẵn sàng để cấp vốn.");
        ApplyExpectedRowVersion(claim, rowVersion);
        if (amount <= 0)
        {
            claim.Status = ProtectionClaimStatus.FUNDED;
            claim.UpdatedAtUtc = DateTime.UtcNow;
            QueueParticipantNotifications(
                claim.AccidentReport, "ProtectionClaimFunded", "Hồ sơ bảo vệ đã được cấp vốn",
                "Hồ sơ bảo vệ đã hoàn tất bước cấp vốn.");
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Map(claim);
        }
        var wasPendingFunding = claim.Status == ProtectionClaimStatus.PENDING_FUNDING;
        var applied = await _riskFundLedger.ApplyClaimFundingAsync(
            claim.Id, claim.RiskFundAdvanceAmount, claim.RiskFundPermanentLossAmount,
            staffUserId, idempotencyKey, cancellationToken);
        if (!applied)
        {
            claim.Status = ProtectionClaimStatus.PENDING_FUNDING;
            claim.UpdatedAtUtc = DateTime.UtcNow;
            if (!wasPendingFunding)
            {
                QueueParticipantNotifications(
                    claim.AccidentReport, "ProtectionClaimPendingFunding", "Hồ sơ đang chờ nguồn quỹ",
                    "Risk Fund hiện chưa đủ số dư; hồ sơ được giữ ở trạng thái chờ cấp vốn.");
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Map(claim);
        }
        CompleteFunding(claim, amount);
        QueueParticipantNotifications(
            claim.AccidentReport, "ProtectionClaimFunded", "Hồ sơ bảo vệ đã được cấp vốn",
            "Hồ sơ bảo vệ đã hoàn tất bước cấp vốn.");
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(claim);
    }

    public async Task<ProtectionClaimResponse> RecordRecoveryAsync(
        Guid staffUserId, long claimId, ClaimRecoveryRequest request, CancellationToken cancellationToken)
        => await ExecuteRiskFundMutationAsync(
            () => RecordRecoveryCoreAsync(staffUserId, claimId, request, cancellationToken),
            cancellationToken);

    public async Task EnsureCanRecordRecoveryEvidenceAsync(
        long claimId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var normalizedKey = NormalizeRequired(idempotencyKey, 95, "Idempotency key");
        if (await _dbContext.ClaimRecoveries.AsNoTracking().AnyAsync(
                x => x.ProtectionClaimId == claimId && x.IdempotencyKey == normalizedKey,
                cancellationToken))
            return;
        var status = await _dbContext.ProtectionClaims.AsNoTracking()
            .Where(x => x.Id == claimId)
            .Select(x => (ProtectionClaimStatus?)x.Status)
            .SingleOrDefaultAsync(cancellationToken) ?? throw ClaimNotFound();
        if (status != ProtectionClaimStatus.RECOVERY_IN_PROGRESS)
            throw Conflict(
                "risk_protection.recovery_not_funded",
                "Chỉ được tải bằng chứng recovery sau khi Risk Fund đã cấp vốn cho claim.");
    }

    private async Task<ProtectionClaimResponse> RecordRecoveryCoreAsync(
        Guid staffUserId, long claimId, ClaimRecoveryRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.SourceType)) throw Invalid("Nguồn recovery không hợp lệ.");
        var amount = Round(request.Amount);
        if (amount <= 0) throw Invalid("Số tiền recovery phải lớn hơn 0.");
        var payerReference = NormalizeRequired(request.PayerReference, 200, "Payer");
        var paymentReference = NormalizeRequired(request.PaymentReference, 200, "Payment reference");
        var evidenceUrl = NormalizeRequired(request.Evidence.EvidenceUrl, 1000, "Bằng chứng");
        var evidencePublicId = NormalizeRequired(request.Evidence.StoragePublicId, 500, "Evidence storage id");
        var evidenceFileName = NormalizeRequired(request.Evidence.OriginalFileName, 255, "Evidence file name");
        var evidenceContentType = NormalizeRequired(request.Evidence.ContentType, 100, "Evidence content type");
        if (request.Evidence.FileSizeBytes <= 0)
            throw Invalid("Bằng chứng recovery phải có metadata dung lượng tin cậy.");
        if (!IsHttpUrl(evidenceUrl)) throw Invalid("Bằng chứng recovery phải là URL HTTP hoặc HTTPS hợp lệ.");
        var idempotencyKey = NormalizeRequired(request.IdempotencyKey, 95, "Idempotency key");
        var ledgerKey = $"fund:{idempotencyKey}";

        var existing = await _dbContext.ClaimRecoveries.AsNoTracking()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
        var claim = await _dbContext.ProtectionClaims
            .Include(x => x.Recoveries)
            .Include(x => x.DriverLiabilities)
            .Include(x => x.AccidentReport).ThenInclude(x => x.Trip).ThenInclude(x => x.Booking)
            .SingleOrDefaultAsync(x => x.Id == claimId, cancellationToken) ?? throw ClaimNotFound();
        if (existing is not null)
        {
            if (existing.ProtectionClaimId != claim.Id
                || existing.SourceType != request.SourceType
                || existing.PayerReference != payerReference
                || existing.Amount != amount
                || existing.PaymentReference != paymentReference
                || existing.EvidenceUrl != evidenceUrl
                || existing.RecordedByUserId != staffUserId)
            {
                throw Conflict(
                    "risk_protection.recovery_idempotency_conflict",
                    "Idempotency key đã được dùng cho một khoản recovery khác.");
            }

            if (!await _dbContext.RiskFundTransactions.AsNoTracking().AnyAsync(
                    x => x.ClaimRecoveryId == existing.Id
                        && x.ProtectionClaimId == claim.Id
                        && x.IdempotencyKey == ledgerKey,
                    cancellationToken))
            {
                throw Conflict(
                    "risk_protection.recovery_ledger_missing",
                    "Khoản recovery đã tồn tại nhưng chưa có giao dịch Risk Fund tương ứng.");
            }

            return Map(claim);
        }

        if (await _dbContext.RiskFundTransactions.AsNoTracking().AnyAsync(
                x => x.IdempotencyKey == ledgerKey,
                cancellationToken))
        {
            throw Conflict(
                "risk_protection.recovery_idempotency_conflict",
                "Idempotency key đã được dùng cho một giao dịch recovery khác.");
        }

        if (claim.Status != ProtectionClaimStatus.RECOVERY_IN_PROGRESS)
        {
            throw Conflict(
                "risk_protection.recovery_not_funded",
                "Chỉ được ghi recovery sau khi Risk Fund đã cấp vốn cho claim.");
        }

        ApplyExpectedRowVersion(claim, request.RowVersion);
        var driverLiability = claim.DriverLiabilities.SingleOrDefault();
        if (request.SourceType == RecoverySourceType.DRIVER
            && (driverLiability is null
                || !payerReference.Equals(
                    driverLiability.DriverId.ToString(), StringComparison.OrdinalIgnoreCase)))
            throw Conflict(
                "risk_protection.recovery_payer_mismatch",
                "Payer không khớp với tài xế có nghĩa vụ trong claim.");
        if (request.SourceType == RecoverySourceType.CUSTOMER
            && !payerReference.Equals(
                claim.AccidentReport.Trip.Booking.CustomerId.ToString(),
                StringComparison.OrdinalIgnoreCase))
            throw Conflict(
                "risk_protection.recovery_payer_mismatch",
                "Payer không khớp với khách hàng có nghĩa vụ trong claim.");
        var sourceRecovered = claim.Recoveries.Where(x => x.SourceType == request.SourceType).Sum(x => x.Amount);
        var sourceLimit = request.SourceType switch
        {
            RecoverySourceType.DRIVER => driverLiability!.ConfirmedAmount,
            RecoverySourceType.CUSTOMER => claim.CustomerLiabilityAmount,
            RecoverySourceType.THIRD_PARTY => claim.ThirdPartyLiabilityAmount,
            RecoverySourceType.INSURANCE => claim.InsurancePaidDirectToClaimant > 0m
                ? 0m
                : claim.InsuranceApprovedAmount,
            _ => 0m
        };
        var fundDebited = await _dbContext.RiskFundTransactions.AsNoTracking()
            .Where(x => x.ProtectionClaimId == claim.Id
                && (x.TransactionType == RiskFundTransactionType.CLAIM_ADVANCE
                    || x.TransactionType == RiskFundTransactionType.CLAIM_PAYOUT))
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var fundRecovered = await _dbContext.RiskFundTransactions.AsNoTracking()
            .Where(x => x.ProtectionClaimId == claim.Id
                && (x.TransactionType == RiskFundTransactionType.DRIVER_RECOVERY
                    || x.TransactionType == RiskFundTransactionType.CUSTOMER_RECOVERY
                    || x.TransactionType == RiskFundTransactionType.THIRD_PARTY_RECOVERY
                    || x.TransactionType == RiskFundTransactionType.INSURANCE_RECOVERY))
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
        var outstandingFundExposure = Math.Max(
            0m,
            Math.Min(claim.RiskFundAdvanceAmount, fundDebited)
                - fundRecovered - claim.WrittenOffAdvanceAmount);
        if (amount > sourceLimit - sourceRecovered
            || amount > claim.OutstandingRecoveryAmount
            || amount > outstandingFundExposure)
        {
            throw Conflict(
                "risk_protection.recovery_exceeds_outstanding",
                "Recovery vượt quá nghĩa vụ hoặc phần Risk Fund thực tế còn cần thu hồi.");
        }

        var recovery = new ClaimRecovery
        {
            ProtectionClaimId = claim.Id,
            SourceType = request.SourceType,
            PayerReference = payerReference,
            Amount = amount,
            PaymentReference = paymentReference,
            EvidenceUrl = evidenceUrl,
            EvidenceStoragePublicId = evidencePublicId,
            EvidenceOriginalFileName = evidenceFileName,
            EvidenceContentType = evidenceContentType,
            EvidenceFileSizeBytes = request.Evidence.FileSizeBytes,
            RecordedByUserId = staffUserId,
            IdempotencyKey = idempotencyKey,
            RecordedAtUtc = DateTime.UtcNow
        };
        _dbContext.ClaimRecoveries.Add(recovery);
        claim.RecoveredAmount += amount;
        claim.OutstandingRecoveryAmount -= amount;
        if (request.SourceType == RecoverySourceType.INSURANCE)
            claim.InsuranceReimbursedToRiskFund += amount;
        UpdateReconciliationStatus(claim);
        claim.UpdatedAtUtc = DateTime.UtcNow;
        if (request.SourceType == RecoverySourceType.DRIVER)
        {
            var liability = claim.DriverLiabilities.SingleOrDefault();
            if (liability is not null)
            {
                liability.PaidAmount += amount;
                liability.OutstandingAmount = Math.Max(0m, liability.ConfirmedAmount - liability.PaidAmount);
                liability.Status = liability.OutstandingAmount == 0
                    ? DriverLiabilityStatus.PAID
                    : DriverLiabilityStatus.PARTIALLY_PAID;
                liability.UpdatedAtUtc = DateTime.UtcNow;
            }
        }
        await SaveChangesWithConcurrencyAsync(cancellationToken);
        await _riskFundLedger.ApplyAsync(ToLedgerType(request.SourceType), LedgerDirection.CREDIT,
            amount, null, claim.Id, recovery.Id, staffUserId, paymentReference, evidenceUrl,
            $"Manual {request.SourceType} recovery", ledgerKey, cancellationToken);
        QueueParticipantNotifications(
            claim.AccidentReport, "ProtectionClaimRecoveryRecorded", "Hồ sơ thu hồi đã được cập nhật",
            "Staff đã ghi nhận một khoản thu hồi thủ công cho hồ sơ bảo vệ.");
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(claim);
    }

    public async Task<ProtectionClaimResponse> WriteOffAdvanceAsync(
        Guid staffUserId, long claimId, ClaimWriteOffRequest request, CancellationToken cancellationToken)
        => await ExecuteRiskFundMutationAsync(async () =>
        {
            var amount = Round(request.Amount);
            if (amount <= 0) throw Invalid("Số tiền write-off phải lớn hơn 0.");
            var reason = NormalizeRequired(request.Reason, 2000, "Lý do write-off");
            var idempotencyKey = NormalizeRequired(request.IdempotencyKey, 100, "Idempotency key");
            var evidence = ValidateTrustedEvidence(request.Evidence);
            var claim = await _dbContext.ProtectionClaims
                .Include(x => x.AccidentReport).ThenInclude(x => x.Trip).ThenInclude(x => x.Booking)
                .Include(x => x.ReconciliationRecords)
                .SingleOrDefaultAsync(x => x.Id == claimId, cancellationToken) ?? throw ClaimNotFound();
            var existing = claim.ReconciliationRecords.SingleOrDefault(x => x.IdempotencyKey == idempotencyKey);
            if (existing is not null)
            {
                if (existing.ReconciliationType != ClaimReconciliationType.ADVANCE_WRITE_OFF
                    || existing.Amount != amount
                    || existing.Reason != reason
                    || existing.EvidenceUrl != evidence.EvidenceUrl
                    || existing.RecordedByUserId != staffUserId)
                    throw Conflict(
                        "risk_protection.write_off_idempotency_conflict",
                        "Idempotency key đã được dùng cho một lệnh write-off khác.");
                return Map(claim);
            }
            if (claim.Status is not (ProtectionClaimStatus.RECOVERY_IN_PROGRESS or ProtectionClaimStatus.SETTLED))
                throw Conflict(
                    "risk_protection.write_off_not_funded",
                    "Chỉ được write-off khoản ứng sau khi Risk Fund đã cấp vốn.");
            ApplyExpectedRowVersion(claim, request.RowVersion);

            var advanceDebited = await _dbContext.RiskFundTransactions.AsNoTracking()
                .Where(x => x.ProtectionClaimId == claim.Id
                    && x.TransactionType == RiskFundTransactionType.CLAIM_ADVANCE)
                .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
            var recoveries = await RecoveryLedgerTotalAsync(claim.Id, cancellationToken);
            var actualExposure = Math.Max(0m,
                Math.Min(claim.RiskFundAdvanceAmount, advanceDebited)
                    - recoveries - claim.WrittenOffAdvanceAmount);
            if (amount > claim.OutstandingRecoveryAmount || amount > actualExposure)
                throw Conflict(
                    "risk_protection.write_off_exceeds_exposure",
                    "Write-off vượt quá khoản ứng còn phải thu hồi thực tế.");

            claim.ReconciliationRecords.Add(new ClaimReconciliationRecord
            {
                ReconciliationType = ClaimReconciliationType.ADVANCE_WRITE_OFF,
                Amount = amount,
                Reason = reason,
                EvidenceUrl = evidence.EvidenceUrl,
                EvidenceStoragePublicId = evidence.StoragePublicId,
                EvidenceOriginalFileName = evidence.OriginalFileName,
                EvidenceContentType = evidence.ContentType,
                EvidenceFileSizeBytes = evidence.FileSizeBytes,
                RecordedByUserId = staffUserId,
                IdempotencyKey = idempotencyKey,
                RecordedAtUtc = DateTime.UtcNow
            });
            claim.WrittenOffAdvanceAmount += amount;
            claim.OutstandingRecoveryAmount -= amount;
            claim.UpdatedAtUtc = DateTime.UtcNow;
            UpdateReconciliationStatus(claim);
            await SaveChangesWithConcurrencyAsync(cancellationToken);
            return Map(claim);
        }, cancellationToken);

    public async Task EnsureCanWriteOffEvidenceAsync(
        long claimId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var normalizedKey = NormalizeRequired(idempotencyKey, 100, "Idempotency key");
        if (await _dbContext.ClaimReconciliationRecords.AsNoTracking().AnyAsync(
                x => x.ProtectionClaimId == claimId
                    && x.ReconciliationType == ClaimReconciliationType.ADVANCE_WRITE_OFF
                    && x.IdempotencyKey == normalizedKey,
                cancellationToken))
            return;
        var status = await _dbContext.ProtectionClaims.AsNoTracking()
            .Where(x => x.Id == claimId)
            .Select(x => (ProtectionClaimStatus?)x.Status)
            .SingleOrDefaultAsync(cancellationToken) ?? throw ClaimNotFound();
        if (status is not (ProtectionClaimStatus.RECOVERY_IN_PROGRESS or ProtectionClaimStatus.SETTLED))
            throw Conflict(
                "risk_protection.write_off_not_funded",
                "Chỉ được tải bằng chứng write-off sau khi Risk Fund đã cấp vốn.");
    }

    public async Task<ProtectionClaimResponse> CloseClaimAsync(
        Guid staffUserId, long claimId, CloseClaimRequest request, CancellationToken cancellationToken)
        => await ExecuteRiskFundMutationAsync(async () =>
        {
            var claim = await _dbContext.ProtectionClaims
                .Include(x => x.AccidentReport).ThenInclude(x => x.LiabilityAssessment)
                .Include(x => x.AccidentReport).ThenInclude(x => x.Trip).ThenInclude(x => x.Booking)
                .Include(x => x.Recoveries)
                .Include(x => x.ReconciliationRecords)
                .Include(x => x.DriverLiabilities)
                .SingleOrDefaultAsync(x => x.Id == claimId, cancellationToken) ?? throw ClaimNotFound();
            if (claim.Status == ProtectionClaimStatus.CLOSED) return Map(claim);
            ApplyExpectedRowVersion(claim, request.RowVersion);

            var funded = await _dbContext.RiskFundTransactions.AsNoTracking()
                .Where(x => x.ProtectionClaimId == claim.Id
                    && (x.TransactionType == RiskFundTransactionType.CLAIM_ADVANCE
                        || x.TransactionType == RiskFundTransactionType.CLAIM_PAYOUT))
                .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;
            var ledgerRecoveries = await RecoveryLedgerTotalAsync(claim.Id, cancellationToken);
            var recordedRecoveries = claim.Recoveries.Sum(x => x.Amount);
            var expectedFunding = claim.RiskFundAdvanceAmount + claim.RiskFundPermanentLossAmount;
            var reconciled = IsReconciled(claim)
                && claim.OutstandingRecoveryAmount == 0m
                && recordedRecoveries == claim.RecoveredAmount
                && ledgerRecoveries == claim.RecoveredAmount;
            var hasPendingWork = claim.InsuranceStatus == InsuranceClaimStatus.PENDING
                || claim.Status is not (ProtectionClaimStatus.FUNDED or ProtectionClaimStatus.SETTLED)
                || claim.AccidentReport.LiabilityAssessment?.Status == LiabilityAssessmentStatus.DISPUTED
                || claim.DriverLiabilities.Any(x => x.Status == DriverLiabilityStatus.DISPUTED);
            if (claim.TotalPaidToClaimant > claim.EligibleDamageAmount
                || funded != expectedFunding
                || !reconciled
                || hasPendingWork)
                throw Conflict(
                    "risk_protection.claim_not_reconciled",
                    "Claim chưa cân bằng funding, recovery/write-off hoặc vẫn còn nghiệp vụ đang chờ.");

            claim.Status = ProtectionClaimStatus.CLOSED;
            claim.ClosedByUserId = staffUserId;
            claim.ClosedAtUtc = DateTime.UtcNow;
            claim.UpdatedAtUtc = claim.ClosedAtUtc;
            claim.AccidentReport.Status = AccidentStatus.CLOSED;
            claim.AccidentReport.UpdatedAtUtc = claim.ClosedAtUtc;
            await SaveChangesWithConcurrencyAsync(cancellationToken);
            return Map(claim);
        }, cancellationToken);

    public async Task<IReadOnlyList<DriverLiabilityResponse>> GetDriverLiabilitiesAsync(
        Guid driverId, CancellationToken cancellationToken)
    {
        var liabilities = await _dbContext.DriverLiabilities.AsNoTracking()
            .Include(x => x.ProtectionClaim)
                .ThenInclude(x => x.Recoveries)
            .Where(x => x.DriverId == driverId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return liabilities.Select(x => new DriverLiabilityResponse(x.Id, x.ProtectionClaimId,
                x.DriverAttributableEligibleDamage, x.FaultLevel, x.ConfirmedAmount,
                x.PaidAmount, x.OutstandingAmount, x.Status,
                x.ProtectionClaim.Recoveries
                    .Where(recovery => recovery.SourceType == RecoverySourceType.DRIVER)
                    .OrderByDescending(recovery => recovery.RecordedAtUtc)
                    .ThenByDescending(recovery => recovery.Id)
                    .Select(recovery => new ClaimRecoveryHistoryResponse(
                        recovery.Id,
                        recovery.SourceType,
                        recovery.Amount,
                        MaskPaymentReference(recovery.PaymentReference),
                        recovery.RecordedAtUtc))
                    .ToList(),
                x.ProtectionClaim.AccidentReportId,
                x.ProtectionClaim.Status))
            .ToList();
    }

    public async Task DisputeLiabilityAsync(
        Guid userId,
        long accidentId,
        LiabilityDisputeRequest request,
        CancellationToken cancellationToken)
    {
        var reason = request.Reason;
        var evidenceIds = request.EvidenceIds?.Distinct().ToArray() ?? [];
        if (reason?.Trim().Length > 2000)
            throw Invalid("Lý do tranh chấp không được vượt quá 2.000 ký tự.");
        if (evidenceIds.Length is < 1 or > 20)
            throw Invalid("Tranh chấp phải liên kết từ 1 đến 20 bằng chứng của người gửi.");
        if (string.IsNullOrWhiteSpace(reason)) throw Invalid("Lý do tranh chấp là bắt buộc.");
        var accident = await _dbContext.AccidentReports.Include(x => x.Trip).ThenInclude(x => x.Booking)
            .Include(x => x.LiabilityAssessment)
            .Include(x => x.ProtectionClaim).ThenInclude(x => x!.DriverLiabilities)
            .SingleOrDefaultAsync(x => x.Id == accidentId, cancellationToken) ?? throw NotFound();
        if (accident.Trip.DriverId != userId && accident.Trip.Booking.CustomerId != userId) throw NotFound();
        if (accident.LiabilityAssessment?.Status != LiabilityAssessmentStatus.CONFIRMED)
            throw Conflict("Chưa có liability assessment đã xác nhận để tranh chấp.");
        if (accident.ProtectionClaim?.Status is ProtectionClaimStatus.FUNDED
            or ProtectionClaimStatus.RECOVERY_IN_PROGRESS
            or ProtectionClaimStatus.SETTLED
            or ProtectionClaimStatus.CLOSED)
            throw Conflict("Không thể mở tranh chấp liability sau khi claim đã được cấp vốn.");
        var evidence = await _dbContext.AccidentEvidence
            .Where(x => x.AccidentReportId == accidentId
                && evidenceIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        if (evidence.Count != evidenceIds.Length)
            throw Invalid("Bằng chứng tranh chấp không thuộc hồ sơ tai nạn.");
        var disputedAtUtc = DateTime.UtcNow;
        _dbContext.LiabilityDisputeAudits.Add(new LiabilityDisputeAudit
        {
            AssessmentId = accident.LiabilityAssessment.Id,
            DisputedByUserId = userId,
            DisputedAtUtc = disputedAtUtc,
            Reason = reason.Trim(),
            Evidence = evidence.Select(item => new LiabilityDisputeEvidence
            {
                AccidentEvidenceId = item.Id
            }).ToList()
        });
        accident.LiabilityAssessment.Status = LiabilityAssessmentStatus.DISPUTED;
        accident.LiabilityAssessment.DisputeReason = reason.Trim();
        accident.LiabilityAssessment.UpdatedAtUtc = disputedAtUtc;
        accident.Status = AccidentStatus.LIABILITY_PENDING;
        if (accident.ProtectionClaim is not null)
        {
            if (accident.ProtectionClaim.Status is ProtectionClaimStatus.FUNDED
                or ProtectionClaimStatus.RECOVERY_IN_PROGRESS
                or ProtectionClaimStatus.SETTLED
                or ProtectionClaimStatus.CLOSED)
                throw Conflict("Không thể mở tranh chấp liability sau khi claim đã được cấp vốn.");
            accident.ProtectionClaim.Status = ProtectionClaimStatus.UNDER_REVIEW;
            accident.ProtectionClaim.UpdatedAtUtc = DateTime.UtcNow;
            foreach (var liability in accident.ProtectionClaim.DriverLiabilities)
            {
                liability.Status = DriverLiabilityStatus.DISPUTED;
                liability.DisputeReason = reason.Trim();
                liability.UpdatedAtUtc = DateTime.UtcNow;
            }
        }
        QueueParticipantNotifications(
            accident, "AccidentLiabilityDisputed", "Đánh giá trách nhiệm đang được xem xét lại",
            "Một bên tham gia chuyến đi đã gửi yêu cầu xem xét lại đánh giá trách nhiệm.");
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Trip> GetParticipantTripAsync(Guid userId, long tripId, CancellationToken cancellationToken)
        => await _dbContext.Trips.Include(x => x.Booking)
            .SingleOrDefaultAsync(x => x.Id == tripId && (x.DriverId == userId || x.Booking.CustomerId == userId), cancellationToken)
            ?? throw new BookingException("trip.not_found", "Không tìm thấy chuyến đi.", StatusCodes.Status404NotFound);

    private static void ValidateAssessment(LiabilityAssessmentRequest request)
    {
        if (!Enum.IsDefined(request.DriverFaultLevel)
            || !Enum.IsDefined(request.VehicleDefectAwareness)
            || request.Causes is null
            || request.Causes.Any(x => !Enum.IsDefined(x.RootCause)
                || !Enum.IsDefined(x.ResponsibleParty)))
            throw Invalid("Thông tin phân loại liability assessment không hợp lệ.");
        var total = request.DriverFaultPercentage + request.CustomerFaultPercentage
            + request.ThirdPartyFaultPercentage + request.VehicleFailurePercentage
            + request.ObjectiveCausePercentage;
        if (new[] { request.DriverFaultPercentage, request.CustomerFaultPercentage, request.ThirdPartyFaultPercentage, request.VehicleFailurePercentage, request.ObjectiveCausePercentage }.Any(x => x is < 0 or > 100)
            || total != 100m)
            throw Invalid("Tổng tỷ lệ liability phải bằng 100%.");
        if (request.Causes.Count == 0 || request.Causes.Any(x => x.Percentage is <= 0 or > 100)
            || request.Causes.Sum(x => x.Percentage) != 100m
            || request.Causes.GroupBy(x => new { x.RootCause, x.ResponsibleParty }).Any(x => x.Count() > 1))
            throw Invalid("Phân bổ root cause phải có tổng bằng 100%.");

        var expectedByParty = new Dictionary<ResponsiblePartyType, decimal>
        {
            [ResponsiblePartyType.DRIVER] = request.DriverFaultPercentage,
            [ResponsiblePartyType.CUSTOMER] = request.CustomerFaultPercentage,
            [ResponsiblePartyType.THIRD_PARTY] = request.ThirdPartyFaultPercentage,
            [ResponsiblePartyType.VEHICLE] = request.VehicleFailurePercentage,
            [ResponsiblePartyType.OBJECTIVE] = request.ObjectiveCausePercentage
        };
        if (expectedByParty.Any(expected =>
                request.Causes
                    .Where(x => x.ResponsibleParty == expected.Key)
                    .Sum(x => x.Percentage) != expected.Value))
            throw Invalid("Phân bổ root cause theo từng bên phải khớp tỷ lệ liability assessment.");

        if ((request.DriverFaultPercentage == 0m) != (request.DriverFaultLevel == DriverFaultLevel.NO_FAULT))
            throw Invalid("DriverFaultLevel phải là NO_FAULT khi và chỉ khi tỷ lệ lỗi Driver bằng 0%.");

        var customerIntoxication = request.Causes.Any(x =>
            x.RootCause == AccidentRootCause.CUSTOMER_INTOXICATION);
        var customerUnsafeBehavior = request.Causes.Any(x =>
            x.RootCause == AccidentRootCause.CUSTOMER_INTERFERENCE
            && x.ResponsibleParty == ResponsiblePartyType.CUSTOMER);
        if (request.Causes.Any(x => x.RootCause == AccidentRootCause.CUSTOMER_INTOXICATION
                && x.ResponsibleParty != ResponsiblePartyType.CUSTOMER)
            || customerIntoxication && !customerUnsafeBehavior)
            throw Invalid("Customer intoxication chỉ được phân bổ lỗi khi có unsafe behavior/root cause của Customer.");

        var customerKnewDefect = request.Causes.Any(x =>
            x.RootCause == AccidentRootCause.VEHICLE_PRE_EXISTING_DEFECT
            && x.ResponsibleParty == ResponsiblePartyType.CUSTOMER);
        var driverKnewDefect = request.Causes.Any(x =>
            x.RootCause == AccidentRootCause.VEHICLE_PRE_EXISTING_DEFECT
            && x.ResponsibleParty == ResponsiblePartyType.DRIVER);
        var vehicleHiddenDefect = request.Causes.Any(x =>
            x.RootCause == AccidentRootCause.VEHICLE_PRE_EXISTING_DEFECT
            && x.ResponsibleParty == ResponsiblePartyType.VEHICLE);
        var awarenessIsConsistent = request.VehicleDefectAwareness switch
        {
            VehicleDefectAwareness.UNKNOWN => !customerKnewDefect && !driverKnewDefect,
            VehicleDefectAwareness.CUSTOMER_KNEW => customerKnewDefect && !driverKnewDefect,
            VehicleDefectAwareness.DRIVER_KNEW => driverKnewDefect && !customerKnewDefect,
            VehicleDefectAwareness.BOTH_KNEW => customerKnewDefect && driverKnewDefect,
            VehicleDefectAwareness.NEITHER_COULD_REASONABLY_KNOW =>
                vehicleHiddenDefect && !customerKnewDefect && !driverKnewDefect,
            _ => false
        };
        if (!awarenessIsConsistent)
            throw Invalid("Mức độ nhận biết lỗi ẩn phải phù hợp root cause và allocation của từng bên.");
    }

    private static AccidentResponse Map(AccidentReport x, bool includeAssessment = false) => new(x.Id, x.TripId, x.ReportedByUserId,
        x.Category, x.Status, x.OccurredAtUtc, x.Latitude, x.Longitude, x.Description,
        x.PoliceReportReference, x.CreatedAtUtc, x.ProtectionClaim?.Id, x.ProtectionClaim?.Status,
        x.Evidence.OrderByDescending(e => e.CreatedAtUtc).Select(e => new AccidentEvidenceResponse(
            e.Id, e.UploadedByUserId, e.EvidenceType, e.FileUrl, e.OriginalFileName,
            e.ContentType, e.FileSizeBytes, e.CapturedAtUtc, e.Latitude, e.Longitude,
            e.Description, e.CreatedAtUtc)).ToList(),
        includeAssessment && x.LiabilityAssessment is not null
            ? Map(x.LiabilityAssessment)
            : null,
        includeAssessment && x.ProtectionClaim is not null
            ? Map(x.ProtectionClaim)
            : null);

    private static LiabilityAssessmentResponse Map(AccidentLiabilityAssessment assessment)
    {
        var dispute = assessment.Disputes
            .OrderByDescending(item => item.DisputedAtUtc)
            .ThenByDescending(item => item.Id)
            .FirstOrDefault();
        return new LiabilityAssessmentResponse(
            assessment.Id,
            assessment.DriverFaultPercentage,
            assessment.CustomerFaultPercentage,
            assessment.ThirdPartyFaultPercentage,
            assessment.VehicleFailurePercentage,
            assessment.ObjectiveCausePercentage,
            assessment.DriverFaultLevel,
            assessment.VehicleDefectAwareness,
            assessment.Status,
            assessment.ConfirmedByUserId,
            assessment.ConfirmedAtUtc,
            assessment.DisputeReason,
            dispute?.DisputedByUserId,
            dispute?.DisputedAtUtc,
            dispute?.Evidence.Select(item => item.AccidentEvidenceId).Order().ToList() ?? [],
            Convert.ToBase64String(assessment.RowVersion),
            assessment.Causes
                .OrderBy(cause => cause.Id)
                .Select(cause => new LiabilityCauseResponse(
                    cause.RootCause,
                    cause.ResponsibleParty,
                    cause.Percentage))
                .ToList());
    }

    private static AccidentEvidenceResponse Map(AccidentEvidence evidence) => new(
        evidence.Id,
        evidence.UploadedByUserId,
        evidence.EvidenceType,
        evidence.FileUrl,
        evidence.OriginalFileName,
        evidence.ContentType,
        evidence.FileSizeBytes,
        evidence.CapturedAtUtc,
        evidence.Latitude,
        evidence.Longitude,
        evidence.Description,
        evidence.CreatedAtUtc);

    private static ProtectionClaimResponse Map(ProtectionClaim x, byte[]? assessmentRowVersion = null) => new(
        x.Id,
        x.AccidentReportId,
        Convert.ToBase64String(x.RowVersion),
        assessmentRowVersion is null ? null : Convert.ToBase64String(assessmentRowVersion),
        x.Status, x.InsuranceStatus, x.InsurancePaymentDestination, x.InsuranceRequestedAmount,
        x.TotalDamageAmount, x.EligibleDamageAmount,
        x.InsuranceApprovedAmount, x.InsurancePaidDirectToClaimant, x.InsuranceReimbursedToRiskFund,
        x.RiskFundAdvanceAmount, x.RiskFundPermanentLossAmount,
        x.DriverLiabilityAmount, x.CustomerLiabilityAmount, x.ThirdPartyLiabilityAmount,
        x.TotalPaidToClaimant, x.RecoveredAmount, x.OutstandingRecoveryAmount,
        x.WrittenOffAdvanceAmount,
        Math.Max(0m, x.RiskFundAdvanceAmount - x.RecoveredAmount - x.WrittenOffAdvanceAmount),
        IsReconciled(x));

    private static InsuranceClaimSubmissionContext CreateInsuranceSubmissionContext(
        ProtectionClaim claim,
        TripProtectionCoverage coverage,
        RiskProtectionPolicyVersion policy) => new(
            claim.Id,
            claim.InsuranceRequestedAmount,
            claim.EligibleDamageAmount,
            coverage.InsuranceCoverageSnapshot,
            coverage.InsuranceDeductibleSnapshot,
            policy.MockInsuranceCoverageLimit,
            policy.ClaimAutoApprovalThreshold);

    private static InsuranceClaimCalculationContext CreateInsuranceCalculationContext(
        ProtectionClaim claim,
        TripProtectionCoverage coverage,
        RiskProtectionPolicyVersion policy) => new(
            claim.Id,
            claim.InsuranceRequestedAmount,
            claim.EligibleDamageAmount,
            coverage.InsuranceCoverageSnapshot,
            coverage.InsuranceDeductibleSnapshot,
            policy.MockInsuranceCoverageLimit);

    private void AddInsuranceCalculationAudit(
        ProtectionClaim claim,
        InsuranceCalculationResult result,
        Guid? performedByUserId)
    {
        _dbContext.InsuranceClaimProviderAudits.Add(new InsuranceClaimProviderAudit
        {
            ProtectionClaimId = claim.Id,
            Operation = InsuranceProviderOperation.CALCULATE,
            ResultStatus = InsuranceClaimStatus.NOT_SUBMITTED,
            RequestedAmount = result.RequestedAmount,
            ApprovedAmount = result.ApprovedAmount,
            ProviderReference = result.Reference,
            RequestPayload = result.RequestPayload,
            ResponsePayload = result.ResponsePayload,
            PerformedByUserId = performedByUserId,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private void AddInsuranceAudit(
        ProtectionClaim claim,
        InsuranceProviderOperation operation,
        InsuranceSubmissionResult result,
        Guid? performedByUserId)
    {
        _dbContext.InsuranceClaimProviderAudits.Add(new InsuranceClaimProviderAudit
        {
            ProtectionClaimId = claim.Id,
            Operation = operation,
            ResultStatus = result.Status,
            RequestedAmount = result.RequestedAmount,
            ApprovedAmount = result.ApprovedAmount,
            ProviderReference = result.Reference,
            RequestPayload = result.RequestPayload,
            ResponsePayload = result.ResponsePayload,
            PerformedByUserId = performedByUserId,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private void ApplyExpectedRowVersion<TEntity>(TEntity entity, string? rowVersion)
        where TEntity : class
    {
        if (!_dbContext.Database.IsRelational()) return;
        if (string.IsNullOrWhiteSpace(rowVersion))
            throw Invalid("RowVersion là bắt buộc để tránh ghi đè thay đổi đồng thời.");

        byte[] expected;
        try
        {
            expected = Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            throw Invalid("RowVersion không hợp lệ.");
        }
        if (expected.Length == 0) throw Invalid("RowVersion không hợp lệ.");
        _dbContext.Entry(entity).Property("RowVersion").OriginalValue = expected;
    }

    private async Task SaveChangesWithConcurrencyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Conflict(
                "risk_protection.concurrency_conflict",
                "Dữ liệu đã được Staff khác cập nhật. Vui lòng tải lại trước khi tiếp tục.");
        }
    }

    private static RiskFundTransactionType ToLedgerType(RecoverySourceType source) => source switch
    {
        RecoverySourceType.DRIVER => RiskFundTransactionType.DRIVER_RECOVERY,
        RecoverySourceType.CUSTOMER => RiskFundTransactionType.CUSTOMER_RECOVERY,
        RecoverySourceType.THIRD_PARTY => RiskFundTransactionType.THIRD_PARTY_RECOVERY,
        RecoverySourceType.INSURANCE => RiskFundTransactionType.INSURANCE_RECOVERY,
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };

    private static void CompleteFunding(ProtectionClaim claim, decimal amount)
    {
        claim.Status = claim.OutstandingRecoveryAmount > 0
            ? ProtectionClaimStatus.RECOVERY_IN_PROGRESS
            : ProtectionClaimStatus.FUNDED;
        claim.TotalPaidToClaimant = Math.Min(
            claim.EligibleDamageAmount,
            claim.TotalPaidToClaimant + amount);
        claim.UpdatedAtUtc = DateTime.UtcNow;
    }

    private async Task<decimal> RecoveryLedgerTotalAsync(
        long claimId, CancellationToken cancellationToken) =>
        await _dbContext.RiskFundTransactions.AsNoTracking()
            .Where(x => x.ProtectionClaimId == claimId
                && (x.TransactionType == RiskFundTransactionType.DRIVER_RECOVERY
                    || x.TransactionType == RiskFundTransactionType.CUSTOMER_RECOVERY
                    || x.TransactionType == RiskFundTransactionType.THIRD_PARTY_RECOVERY
                    || x.TransactionType == RiskFundTransactionType.INSURANCE_RECOVERY))
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

    private static bool IsReconciled(ProtectionClaim claim) =>
        claim.RiskFundAdvanceAmount
            == claim.RecoveredAmount
                + claim.OutstandingRecoveryAmount
                + claim.WrittenOffAdvanceAmount;

    private static void UpdateReconciliationStatus(ProtectionClaim claim)
    {
        claim.Status = claim.OutstandingRecoveryAmount == 0m && IsReconciled(claim)
            ? ProtectionClaimStatus.SETTLED
            : ProtectionClaimStatus.RECOVERY_IN_PROGRESS;
    }

    private static TrustedClaimEvidence ValidateTrustedEvidence(TrustedClaimEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var url = NormalizeRequired(evidence.EvidenceUrl, 1000, "Evidence URL");
        if (!IsHttpUrl(url)) throw Invalid("Evidence URL không hợp lệ.");
        var publicId = NormalizeRequired(evidence.StoragePublicId, 500, "Evidence storage id");
        var fileName = NormalizeRequired(evidence.OriginalFileName, 255, "Evidence file name");
        var contentType = NormalizeRequired(evidence.ContentType, 100, "Evidence content type");
        if (evidence.FileSizeBytes <= 0) throw Invalid("Evidence file size không hợp lệ.");
        return new TrustedClaimEvidence(url, publicId, fileName, contentType, evidence.FileSizeBytes);
    }

    private async Task<T> ExecuteRiskFundMutationAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        if (!_dbContext.Database.IsRelational()) return await operation();
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            if (_dbContext.Database.CurrentTransaction.GetDbTransaction().IsolationLevel
                != IsolationLevel.Serializable)
                throw new InvalidOperationException(
                    "Risk Fund mutations require an existing serializable transaction.");
            return await operation();
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            var result = await operation();
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private static string NormalizeRequired(string value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw Invalid($"{fieldName} là bắt buộc.");

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw Invalid($"{fieldName} không được vượt quá {maxLength} ký tự.");

        return normalized;
    }

    private static string MaskPaymentReference(string paymentReference)
    {
        if (paymentReference.Length <= 4) return new string('*', paymentReference.Length);
        return $"{new string('*', paymentReference.Length - 4)}{paymentReference[^4..]}";
    }

    private void QueueParticipantNotifications(
        AccidentReport accident, string type, string title, string content)
    {
        var recipients = new[]
        {
            accident.Trip.DriverId,
            accident.Trip.Booking.CustomerId
        }.Distinct();
        _dbContext.Notifications.AddRange(recipients.Select(userId => new Notification
        {
            UserId = userId,
            Title = title,
            Content = content,
            NotificationType = type,
            ReferenceId = accident.Id,
            SentAt = DateTime.UtcNow
        }));
    }

    private static bool IsAllowedEvidenceContentType(string contentType) =>
        contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
        || contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase)
        || contentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase)
        || contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException && sqlException.Number is 2601 or 2627)
                return true;
        }
        return false;
    }

    private static void EnsureEvidenceCollectionOpen(AccidentStatus status)
    {
        if (status is AccidentStatus.CLOSED or AccidentStatus.REJECTED)
            throw Conflict("Hồ sơ tai nạn đã kết thúc và không nhận thêm bằng chứng.");
    }

    private static void ValidateCoordinates(decimal? latitude, decimal? longitude)
    {
        if (latitude.HasValue != longitude.HasValue
            || latitude is < -90m or > 90m
            || longitude is < -180m or > 180m)
            throw Invalid("Vĩ độ và kinh độ phải được cung cấp cùng nhau và nằm trong phạm vi hợp lệ.");
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private static decimal Round(decimal value) => decimal.Round(value, 0, MidpointRounding.AwayFromZero);
    private static BookingException Invalid(string detail) => new("risk_protection.invalid_request", detail, StatusCodes.Status400BadRequest);
    private static BookingException Conflict(string detail) => new("risk_protection.conflict", detail, StatusCodes.Status409Conflict);
    private static BookingException Conflict(string code, string detail) => new(code, detail, StatusCodes.Status409Conflict);
    private static BookingException NotFound() => new("accident.not_found", "Không tìm thấy báo cáo tai nạn.", StatusCodes.Status404NotFound);
    private static BookingException ClaimNotFound() => new("claim.not_found", "Không tìm thấy hồ sơ yêu cầu bảo vệ.", StatusCodes.Status404NotFound);
}
