using Microsoft.EntityFrameworkCore;
using SafeRide.Domain.Entities;

namespace SafeRide.Infrastructure.Persistence.Configurations;

internal static class RiskProtectionModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        ConfigureExistingEntities(modelBuilder);
        ConfigurePolicyAndSettlement(modelBuilder);
        ConfigureSafetyAndCoverage(modelBuilder);
        ConfigureAccidentsAndClaims(modelBuilder);
        ConfigureRiskFund(modelBuilder);
        ConfigureSafetyPaymentReconciliation(modelBuilder);
    }

    private static void ConfigureSafetyPaymentReconciliation(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SafetyTerminationEvidence>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TripId, x.CreatedAtUtc });
            entity.Property(x => x.EvidenceUrl).HasMaxLength(1000);
            entity.Property(x => x.StoragePublicId).HasMaxLength(500);
            entity.Property(x => x.OriginalFileName).HasMaxLength(255);
            entity.Property(x => x.ContentType).HasMaxLength(100);
            entity.HasOne(x => x.Trip).WithMany().HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AspNetUser>().WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_SafetyTerminationEvidence_TrustedMetadata",
                "[EvidenceUrl] <> '' AND [StoragePublicId] <> '' AND [OriginalFileName] <> '' AND [ContentType] <> '' AND [FileSizeBytes] > 0 AND [UploadedByUserId] <> '00000000-0000-0000-0000-000000000000'"));
        });

        modelBuilder.Entity<SafetyPaymentReconciliation>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TripId).IsUnique();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.RowVersion).IsRowVersion();
            foreach (var property in new[] { nameof(SafetyPaymentReconciliation.CustomerPayableAmount), nameof(SafetyPaymentReconciliation.SuccessfulPaymentAmount), nameof(SafetyPaymentReconciliation.RemainingPayableAmount), nameof(SafetyPaymentReconciliation.RefundObligationAmount), nameof(SafetyPaymentReconciliation.DriverCreditedAmount) })
                entity.Property(property).HasColumnType("decimal(18,2)");
            entity.HasOne(x => x.Trip).WithOne(x => x.SafetyPaymentReconciliation).HasForeignKey<SafetyPaymentReconciliation>(x => x.TripId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_SafetyPaymentReconciliations_Amounts",
                "[CustomerPayableAmount] >= 0 AND [SuccessfulPaymentAmount] >= 0 AND [RemainingPayableAmount] >= 0 AND [RefundObligationAmount] >= 0 AND [DriverCreditedAmount] >= 0 AND NOT ([RemainingPayableAmount] > 0 AND [RefundObligationAmount] > 0)"));
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_SafetyPaymentReconciliations_Identity",
                "[SuccessfulPaymentAmount] + [RemainingPayableAmount] = [CustomerPayableAmount] + [RefundObligationAmount]"));
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_SafetyPaymentReconciliations_Status",
                "[Status] IN ('NOT_REQUIRED','PAYMENT_PENDING','PAID','REFUND_PENDING','REFUNDED')"));
        });

        modelBuilder.Entity<ManualPaymentRefund>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.SafetyPaymentReconciliationId).IsUnique();
            entity.HasIndex(x => x.ConfirmationIdempotencyKey).IsUnique().HasFilter("[ConfirmationIdempotencyKey] IS NOT NULL");
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.PaymentReference).HasMaxLength(200);
            entity.Property(x => x.EvidenceUrl).HasMaxLength(1000);
            entity.Property(x => x.ConfirmationIdempotencyKey).HasMaxLength(100);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasOne(x => x.Reconciliation).WithOne(x => x.Refund).HasForeignKey<ManualPaymentRefund>(x => x.SafetyPaymentReconciliationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AspNetUser>().WithMany().HasForeignKey(x => x.RefundedByUserId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_ManualPaymentRefunds_Amount", "[Amount] > 0");
                table.HasCheckConstraint("CK_ManualPaymentRefunds_Status", "[Status] IN ('REFUND_PENDING','REFUNDED')");
                table.HasCheckConstraint("CK_ManualPaymentRefunds_EvidenceOnRefund", "[Status] = 'REFUND_PENDING' OR ([PaymentReference] IS NOT NULL AND LTRIM(RTRIM([PaymentReference])) <> '' AND [EvidenceUrl] IS NOT NULL AND LTRIM(RTRIM([EvidenceUrl])) <> '' AND [RefundedByUserId] IS NOT NULL AND [RefundedByUserId] <> '00000000-0000-0000-0000-000000000000' AND [RefundedAtUtc] IS NOT NULL)");
            });
        });
    }

    private static void ConfigureExistingEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Trip>(entity =>
        {
            entity.Property(x => x.TerminationCategory).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.EndReason).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.PlannedRouteProgress).HasColumnType("decimal(7,6)");
            entity.Property(x => x.FinalizationLatitude).HasColumnType("decimal(9,6)");
            entity.Property(x => x.FinalizationLongitude).HasColumnType("decimal(9,6)");
            entity.Property(x => x.SafetyTerminationReason).HasMaxLength(500);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_Trips_PlannedRouteProgress",
                "[PlannedRouteProgress] IS NULL OR ([PlannedRouteProgress] >= 0 AND [PlannedRouteProgress] <= 1)"));
        });

        modelBuilder.Entity<Report>(entity =>
        {
            entity.Property(x => x.ReportType)
                .HasConversion<string>()
                .HasMaxLength(30)
                .HasSentinel((SafeRide.Domain.Enums.SafetyReportType)(-1))
                .HasDefaultValue(SafeRide.Domain.Enums.SafetyReportType.GENERAL);
            entity.Property(x => x.ReasonCode).HasMaxLength(50);
            entity.Property(x => x.Latitude).HasColumnType("decimal(9,6)");
            entity.Property(x => x.Longitude).HasColumnType("decimal(9,6)");
            entity.HasOne<PreTripVehicleCheck>()
                .WithMany()
                .HasForeignKey(x => x.PreTripVehicleCheckId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private static void ConfigurePolicyAndSettlement(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RiskProtectionPolicyVersion>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.EffectiveFromUtc).IsUnique();
            entity.Property(x => x.BasePlatformCommissionRate).HasColumnType("decimal(7,6)");
            entity.Property(x => x.RiskReserveRate).HasColumnType("decimal(7,6)");
            Money(entity.Property(x => x.DefaultProtectionLimit));
            entity.Property(x => x.DriverOrdinaryNegligenceRate).HasColumnType("decimal(7,6)");
            Money(entity.Property(x => x.DriverOrdinaryNegligenceCap));
            entity.Property(x => x.DriverGrossNegligenceRate).HasColumnType("decimal(7,6)");
            Money(entity.Property(x => x.DriverGrossNegligenceCap));
            Money(entity.Property(x => x.MockInsuranceCoverageLimit));
            Money(entity.Property(x => x.ClaimAutoApprovalThreshold));
            entity.Property(x => x.ChangeReason).HasMaxLength(500);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasData(new RiskProtectionPolicyVersion
            {
                Id = 1,
                EffectiveFromUtc = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                BasePlatformCommissionRate = 0.30m,
                RiskReserveRate = 0m,
                DefaultProtectionLimit = 0m,
                DriverOrdinaryNegligenceRate = 0m,
                DriverOrdinaryNegligenceCap = 0m,
                DriverGrossNegligenceRate = 0m,
                DriverGrossNegligenceCap = 0m,
                MockInsuranceCoverageLimit = 0m,
                ClaimAutoApprovalThreshold = 0m,
                RiskFundEnabled = false,
                CreatedByUserId = null,
                ChangeReason = "Legacy 30 percent commission baseline; risk protection disabled",
                CreatedAtUtc = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_RiskProtectionPolicyVersions_ImmutableWhenReferenced");
                table.HasCheckConstraint("CK_RiskProtectionPolicy_CommissionRate", "[BasePlatformCommissionRate] >= 0 AND [BasePlatformCommissionRate] <= 1");
                table.HasCheckConstraint("CK_RiskProtectionPolicy_ReserveRate", "[RiskReserveRate] >= 0 AND [RiskReserveRate] <= 1");
                table.HasCheckConstraint("CK_RiskProtectionPolicy_NegligenceRates", "[DriverOrdinaryNegligenceRate] >= 0 AND [DriverOrdinaryNegligenceRate] <= 1 AND [DriverGrossNegligenceRate] >= 0 AND [DriverGrossNegligenceRate] <= 1");
            });
        });

        modelBuilder.Entity<TripFinancialSettlement>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TripId).IsUnique();
            entity.Property(x => x.RowVersion).IsRowVersion();
            foreach (var property in new[]
            {
                nameof(TripFinancialSettlement.CommissionBase), nameof(TripFinancialSettlement.PromotionExpense),
                nameof(TripFinancialSettlement.CustomerPayableAmount), nameof(TripFinancialSettlement.GrossPlatformCommission),
                nameof(TripFinancialSettlement.DriverEarning), nameof(TripFinancialSettlement.NetPlatformCommission),
                nameof(TripFinancialSettlement.RiskContribution), nameof(TripFinancialSettlement.NetOperatingRevenue),
                nameof(TripFinancialSettlement.GrossFare), nameof(TripFinancialSettlement.FareComponent),
                nameof(TripFinancialSettlement.LongDistanceComponent), nameof(TripFinancialSettlement.SnapshotPromotionDiscount),
                nameof(TripFinancialSettlement.AppliedPromotionDiscount), nameof(TripFinancialSettlement.DriverFareEarning),
                nameof(TripFinancialSettlement.LongDistanceEarning), nameof(TripFinancialSettlement.LongPickupCompensation),
                nameof(TripFinancialSettlement.DriverPayout)
            }) entity.Property(property).HasColumnType("decimal(18,2)");
            entity.Property(x => x.PlatformCommissionRate).HasColumnType("decimal(7,6)");
            entity.Property(x => x.RiskReserveRate).HasColumnType("decimal(7,6)");
            entity.HasOne(x => x.Trip).WithOne().HasForeignKey<TripFinancialSettlement>(x => x.TripId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PolicyVersion).WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_TripFinancialSettlements_NonNegative", "[CommissionBase] >= 0 AND [PromotionExpense] >= 0 AND [CustomerPayableAmount] >= 0 AND [GrossPlatformCommission] >= 0 AND [DriverEarning] >= 0 AND [RiskContribution] >= 0 AND ([ComponentBreakdownVersion] IS NULL OR ([GrossFare] >= 0 AND [FareComponent] >= 0 AND [LongDistanceComponent] >= 0 AND [SnapshotPromotionDiscount] >= 0 AND [AppliedPromotionDiscount] >= 0 AND [DriverFareEarning] >= 0 AND [LongDistanceEarning] >= 0 AND [LongPickupCompensation] >= 0 AND [DriverPayout] >= 0))");
                table.HasCheckConstraint("CK_TripFinancialSettlements_ComponentIdentity", "[ComponentBreakdownVersion] IS NULL OR ([ComponentBreakdownVersion] = 1 AND [GrossFare] = [FareComponent] + [LongDistanceComponent] AND [CommissionBase] = [FareComponent] AND [DriverFareEarning] = [FareComponent] - [GrossPlatformCommission] AND [LongDistanceEarning] = [LongDistanceComponent] AND [DriverPayout] = [DriverFareEarning] + [LongDistanceEarning] + [LongPickupCompensation] AND [DriverEarning] = [DriverPayout] AND [PromotionExpense] = [AppliedPromotionDiscount] AND [AppliedPromotionDiscount] <= [GrossFare] AND [AppliedPromotionDiscount] <= [SnapshotPromotionDiscount] AND [CustomerPayableAmount] = [GrossFare] - [AppliedPromotionDiscount] AND [NetPlatformCommission] = [GrossPlatformCommission] - [PromotionExpense] AND [NetOperatingRevenue] = [NetPlatformCommission] - [RiskContribution] - [LongPickupCompensation])");
                table.HasCheckConstraint("CK_TripFinancialSettlements_ComponentNullability", "([ComponentBreakdownVersion] IS NULL AND [GrossFare] IS NULL AND [FareComponent] IS NULL AND [LongDistanceComponent] IS NULL AND [SnapshotPromotionDiscount] IS NULL AND [AppliedPromotionDiscount] IS NULL AND [DriverFareEarning] IS NULL AND [LongDistanceEarning] IS NULL AND [LongPickupCompensation] IS NULL AND [DriverPayout] IS NULL) OR ([ComponentBreakdownVersion] IS NOT NULL AND [GrossFare] IS NOT NULL AND [FareComponent] IS NOT NULL AND [LongDistanceComponent] IS NOT NULL AND [SnapshotPromotionDiscount] IS NOT NULL AND [AppliedPromotionDiscount] IS NOT NULL AND [DriverFareEarning] IS NOT NULL AND [LongDistanceEarning] IS NOT NULL AND [LongPickupCompensation] IS NOT NULL AND [DriverPayout] IS NOT NULL)");
            });
        });
    }

    private static void ConfigureSafetyAndCoverage(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PreTripVehicleCheck>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TripId, x.CheckedAtUtc })
                .IsDescending(false, true);
            entity.Property(x => x.Result).HasConversion<string>().HasMaxLength(10);
            entity.Property(x => x.FaultType).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Note).HasMaxLength(1000);
            entity.Property(x => x.EvidenceUrl).HasMaxLength(1000);
            entity.Property(x => x.EvidenceStoragePublicId).HasMaxLength(500);
            entity.Property(x => x.EvidenceOriginalFileName).HasMaxLength(255);
            entity.Property(x => x.EvidenceContentType).HasMaxLength(100);
            entity.HasOne(x => x.Trip).WithMany().HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_PreTripVehicleChecks_EvidenceFileSize",
                "[EvidenceFileSizeBytes] IS NULL OR ([EvidenceFileSizeBytes] > 0 AND [EvidenceFileSizeBytes] <= 10000000)"));
        });

        modelBuilder.Entity<VehicleInsurancePolicy>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Provider, x.PolicyNumber }).IsUnique().HasFilter("[IsDeleted] = 0");
            entity.Property(x => x.InsuranceType).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.VerificationStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Provider).HasMaxLength(200);
            entity.Property(x => x.PolicyNumber).HasMaxLength(100);
            entity.Property(x => x.DocumentUrl).HasMaxLength(1000);
            Money(entity.Property(x => x.CoverageAmount));
            Money(entity.Property(x => x.Deductible));
            entity.HasOne(x => x.Vehicle)
                .WithMany(x => x.VehicleInsurancePolicies)
                .HasForeignKey(x => x.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TripProtectionCoverage>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TripId).IsUnique();
            Money(entity.Property(x => x.ProtectionLimit));
            Money(entity.Property(x => x.InsuranceCoverageSnapshot));
            Money(entity.Property(x => x.InsuranceDeductibleSnapshot));
            entity.Property(x => x.InsuranceProviderSnapshot).HasMaxLength(200);
            entity.Property(x => x.PolicyNumberSnapshot).HasMaxLength(100);
            entity.HasOne(x => x.Trip).WithOne().HasForeignKey<TripProtectionCoverage>(x => x.TripId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PolicyVersion).WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PreTripVehicleCheck).WithMany().HasForeignKey(x => x.PreTripVehicleCheckId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.VehicleInsurancePolicy).WithMany().HasForeignKey(x => x.VehicleInsurancePolicyId).OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureAccidentsAndClaims(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccidentReport>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TripId, x.Status, x.CreatedAtUtc });
            entity.Property(x => x.Category).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Latitude).HasColumnType("decimal(9,6)");
            entity.Property(x => x.Longitude).HasColumnType("decimal(9,6)");
            entity.Property(x => x.Description).HasMaxLength(4000);
            entity.Property(x => x.PoliceReportReference).HasMaxLength(200);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasOne(x => x.Trip).WithMany().HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AccidentEvidence>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.AccidentReportId, x.SequenceNumber }).IsUnique();
            entity.Property(x => x.EvidenceType).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.FileUrl).HasMaxLength(1000);
            entity.Property(x => x.OriginalFileName).HasMaxLength(255);
            entity.Property(x => x.ContentType).HasMaxLength(100);
            entity.Property(x => x.StoragePublicId).HasMaxLength(500);
            entity.Property(x => x.Latitude).HasColumnType("decimal(9,6)");
            entity.Property(x => x.Longitude).HasColumnType("decimal(9,6)");
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.HasOne(x => x.AccidentReport).WithMany(x => x.Evidence).HasForeignKey(x => x.AccidentReportId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_AccidentEvidence_FileSize",
                    "[FileSizeBytes] IS NULL OR ([FileSizeBytes] > 0 AND [FileSizeBytes] <= 10000000)");
                table.HasCheckConstraint(
                    "CK_AccidentEvidence_SequenceNumber",
                    "[SequenceNumber] BETWEEN 1 AND 20");
            });
        });

        modelBuilder.Entity<AccidentLiabilityAssessment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.AccidentReportId).IsUnique();
            entity.Property(x => x.DriverFaultLevel).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.VehicleDefectAwareness).HasConversion<string>().HasMaxLength(50);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.DisputeReason).HasMaxLength(2000);
            entity.Property(x => x.RowVersion).IsRowVersion();
            foreach (var property in new[] { nameof(AccidentLiabilityAssessment.DriverFaultPercentage), nameof(AccidentLiabilityAssessment.CustomerFaultPercentage), nameof(AccidentLiabilityAssessment.ThirdPartyFaultPercentage), nameof(AccidentLiabilityAssessment.VehicleFailurePercentage), nameof(AccidentLiabilityAssessment.ObjectiveCausePercentage) }) entity.Property(property).HasColumnType("decimal(5,2)");
            entity.HasOne(x => x.AccidentReport).WithOne(x => x.LiabilityAssessment).HasForeignKey<AccidentLiabilityAssessment>(x => x.AccidentReportId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(table => table.HasCheckConstraint("CK_AccidentLiabilityAssessment_Total", "[DriverFaultPercentage] + [CustomerFaultPercentage] + [ThirdPartyFaultPercentage] + [VehicleFailurePercentage] + [ObjectiveCausePercentage] = 100"));
        });

        modelBuilder.Entity<AccidentLiabilityCause>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.AssessmentId, x.RootCause, x.ResponsibleParty }).IsUnique();
            entity.Property(x => x.RootCause).HasConversion<string>().HasMaxLength(50);
            entity.Property(x => x.ResponsibleParty).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.Percentage).HasColumnType("decimal(5,2)");
            entity.HasOne(x => x.Assessment).WithMany(x => x.Causes).HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LiabilityDisputeAudit>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.AssessmentId, x.DisputedAtUtc });
            entity.Property(x => x.Reason).HasMaxLength(2000);
            entity.HasOne(x => x.Assessment)
                .WithMany(x => x.Disputes)
                .HasForeignKey(x => x.AssessmentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LiabilityDisputeEvidence>(entity =>
        {
            entity.HasKey(x => new { x.LiabilityDisputeAuditId, x.AccidentEvidenceId });
            entity.HasOne(x => x.LiabilityDisputeAudit)
                .WithMany(x => x.Evidence)
                .HasForeignKey(x => x.LiabilityDisputeAuditId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.AccidentEvidence)
                .WithMany(x => x.LiabilityDisputes)
                .HasForeignKey(x => x.AccidentEvidenceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProtectionClaim>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.AccidentReportId).IsUnique();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.InsuranceStatus).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.InsurancePaymentDestination).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.InsuranceReference).HasMaxLength(200);
            entity.Property(x => x.RowVersion).IsRowVersion();
            foreach (var property in typeof(ProtectionClaim).GetProperties().Where(x => x.PropertyType == typeof(decimal))) entity.Property(property.Name).HasColumnType("decimal(18,2)");
            entity.HasOne(x => x.AccidentReport).WithOne(x => x.ProtectionClaim).HasForeignKey<ProtectionClaim>(x => x.AccidentReportId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_ProtectionClaims_Amounts",
                "[TotalDamageAmount] >= 0 AND [EligibleDamageAmount] >= 0 AND [EligibleDamageAmount] <= [TotalDamageAmount] " +
                "AND [InsuranceRequestedAmount] >= 0 AND [InsuranceApprovedAmount] >= 0 AND [InsuranceApprovedAmount] <= [InsuranceRequestedAmount] " +
                "AND [InsurancePaidDirectToClaimant] >= 0 AND [InsuranceReimbursedToRiskFund] >= 0 " +
                "AND [InsurancePaidDirectToClaimant] + [InsuranceReimbursedToRiskFund] <= [InsuranceApprovedAmount] " +
                "AND ([InsurancePaidDirectToClaimant] = 0 OR [InsuranceReimbursedToRiskFund] = 0) " +
                "AND (([InsurancePaymentDestination] = 'DIRECT_TO_CLAIMANT' AND [InsuranceReimbursedToRiskFund] = 0) OR ([InsurancePaymentDestination] = 'REIMBURSE_RISK_FUND' AND [InsurancePaidDirectToClaimant] = 0)) " +
                "AND [RiskFundAdvanceAmount] >= 0 AND [RiskFundPermanentLossAmount] >= 0 " +
                "AND [DriverLiabilityAmount] >= 0 AND [CustomerLiabilityAmount] >= 0 AND [ThirdPartyLiabilityAmount] >= 0 " +
                "AND [TotalPaidToClaimant] >= 0 AND [TotalPaidToClaimant] <= [EligibleDamageAmount] " +
                "AND [RecoveredAmount] >= 0 AND [OutstandingRecoveryAmount] >= 0 AND [WrittenOffAdvanceAmount] >= 0 " +
                "AND [RecoveredAmount] + [OutstandingRecoveryAmount] + [WrittenOffAdvanceAmount] <= [RiskFundAdvanceAmount]"));
        });

        modelBuilder.Entity<DriverLiability>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ProtectionClaimId, x.DriverId }).IsUnique();
            entity.Property(x => x.FaultLevel).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.DisputeReason).HasMaxLength(2000);
            entity.Property(x => x.AppliedRate).HasColumnType("decimal(7,6)");
            entity.Property(x => x.RowVersion).IsRowVersion();
            foreach (var property in new[] { nameof(DriverLiability.DriverAttributableEligibleDamage), nameof(DriverLiability.AppliedCap), nameof(DriverLiability.ConfirmedAmount), nameof(DriverLiability.PaidAmount), nameof(DriverLiability.OutstandingAmount) }) entity.Property(property).HasColumnType("decimal(18,2)");
            entity.HasOne(x => x.ProtectionClaim).WithMany(x => x.DriverLiabilities).HasForeignKey(x => x.ProtectionClaimId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ClaimRecovery>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.IdempotencyKey).IsUnique();
            entity.Property(x => x.SourceType).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.PayerReference).HasMaxLength(200);
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.PaymentReference).HasMaxLength(200);
            entity.Property(x => x.EvidenceUrl).HasMaxLength(1000);
            entity.Property(x => x.EvidenceStoragePublicId).HasMaxLength(500);
            entity.Property(x => x.EvidenceOriginalFileName).HasMaxLength(255);
            entity.Property(x => x.EvidenceContentType).HasMaxLength(100);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(100);
            entity.HasOne(x => x.ProtectionClaim).WithMany(x => x.Recoveries).HasForeignKey(x => x.ProtectionClaimId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_ClaimRecoveries_Amount", "[Amount] > 0");
                table.HasCheckConstraint(
                    "CK_ClaimRecoveries_Audit",
                    "[PayerReference] <> '' AND [PaymentReference] <> '' AND [EvidenceUrl] <> '' AND [EvidenceStoragePublicId] <> '' AND [EvidenceOriginalFileName] <> '' AND [EvidenceContentType] <> '' AND [EvidenceFileSizeBytes] > 0 AND [RecordedByUserId] <> '00000000-0000-0000-0000-000000000000' AND [IdempotencyKey] <> ''");
            });
        });

        modelBuilder.Entity<ClaimReconciliationRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.IdempotencyKey).IsUnique();
            entity.HasIndex(x => new { x.ProtectionClaimId, x.RecordedAtUtc });
            entity.Property(x => x.ReconciliationType).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.Reason).HasMaxLength(2000);
            entity.Property(x => x.EvidenceUrl).HasMaxLength(1000);
            entity.Property(x => x.EvidenceStoragePublicId).HasMaxLength(500);
            entity.Property(x => x.EvidenceOriginalFileName).HasMaxLength(255);
            entity.Property(x => x.EvidenceContentType).HasMaxLength(100);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(100);
            entity.HasOne(x => x.ProtectionClaim).WithMany(x => x.ReconciliationRecords)
                .HasForeignKey(x => x.ProtectionClaimId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_ClaimReconciliationRecords_Amount", "[Amount] > 0");
                table.HasCheckConstraint(
                    "CK_ClaimReconciliationRecords_Audit",
                    "[Reason] <> '' AND [EvidenceUrl] <> '' AND [EvidenceStoragePublicId] <> '' AND [EvidenceOriginalFileName] <> '' AND [EvidenceContentType] <> '' AND [EvidenceFileSizeBytes] > 0 AND [RecordedByUserId] <> '00000000-0000-0000-0000-000000000000' AND [IdempotencyKey] <> ''");
            });
        });

        modelBuilder.Entity<InsuranceClaimProviderAudit>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ProtectionClaimId, x.CreatedAtUtc });
            entity.Property(x => x.Operation).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.ResultStatus).HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.RequestedAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.ApprovedAmount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.ProviderReference).HasMaxLength(200);
            entity.Property(x => x.RequestPayload).HasMaxLength(4000);
            entity.Property(x => x.ResponsePayload).HasMaxLength(4000);
            entity.HasOne(x => x.ProtectionClaim)
                .WithMany(x => x.InsuranceProviderAudits)
                .HasForeignKey(x => x.ProtectionClaimId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_InsuranceClaimProviderAudits_Amounts",
                "[RequestedAmount] >= 0 AND [ApprovedAmount] >= 0 AND [ApprovedAmount] <= [RequestedAmount]"));
        });
    }

    private static void ConfigureRiskFund(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RiskFundAccount>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.CurrentBalance).HasColumnType("decimal(18,2)");
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasData(new RiskFundAccount
            {
                Id = 1,
                CurrentBalance = 0m,
                UpdatedAtUtc = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            });
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_RiskFundAccounts_Balance", "[CurrentBalance] >= 0");
                table.HasCheckConstraint("CK_RiskFundAccounts_Singleton", "[Id] = 1");
            });
        });

        modelBuilder.Entity<RiskFundTransaction>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.IdempotencyKey).IsUnique();
            entity.HasIndex(x => new { x.TripId, x.TransactionType }).IsUnique().HasFilter("[TripId] IS NOT NULL AND [TransactionType] = 'CONTRIBUTION'");
            entity.HasIndex(x => new { x.RiskFundAccountId, x.TransactionType }).IsUnique().HasFilter("[TransactionType] = 'OPENING_BALANCE'");
            entity.HasIndex(x => x.CreatedAtUtc);
            entity.HasIndex(x => x.ProtectionClaimId);
            entity.HasIndex(x => x.ClaimRecoveryId);
            entity.Property(x => x.TransactionType).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Direction).HasConversion<string>().HasMaxLength(10);
            entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            entity.Property(x => x.BalanceBefore).HasColumnType("decimal(18,2)");
            entity.Property(x => x.BalanceAfter).HasColumnType("decimal(18,2)");
            entity.Property(x => x.ExternalReference).HasMaxLength(200);
            entity.Property(x => x.EvidenceUrl).HasMaxLength(1000);
            entity.Property(x => x.Reason).HasMaxLength(1000);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(100);
            entity.HasOne(x => x.RiskFundAccount).WithMany(x => x.Transactions).HasForeignKey(x => x.RiskFundAccountId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Trip>().WithMany().HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProtectionClaim>().WithMany().HasForeignKey(x => x.ProtectionClaimId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ClaimRecovery>().WithMany().HasForeignKey(x => x.ClaimRecoveryId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasTrigger("TR_RiskFundTransactions_AppendOnly");
                table.HasCheckConstraint("CK_RiskFundTransactions_Amount", "[Amount] > 0");
                table.HasCheckConstraint("CK_RiskFundTransactions_Balance", "[BalanceBefore] >= 0 AND [BalanceAfter] >= 0");
                table.HasCheckConstraint(
                    "CK_RiskFundTransactions_BalanceMovement",
                    "([Direction] = 'CREDIT' AND [BalanceAfter] = [BalanceBefore] + [Amount]) OR ([Direction] = 'DEBIT' AND [BalanceAfter] = [BalanceBefore] - [Amount])");
                table.HasCheckConstraint(
                    "CK_RiskFundTransactions_TypeDirection",
                    "([TransactionType] IN ('OPENING_BALANCE','CONTRIBUTION','DRIVER_RECOVERY','CUSTOMER_RECOVERY','THIRD_PARTY_RECOVERY','INSURANCE_RECOVERY') AND [Direction] = 'CREDIT') OR ([TransactionType] IN ('CLAIM_ADVANCE','CLAIM_PAYOUT') AND [Direction] = 'DEBIT') OR [TransactionType] = 'ADJUSTMENT'");
                table.HasCheckConstraint(
                    "CK_RiskFundTransactions_TypeLinks",
                    "([TransactionType] IN ('OPENING_BALANCE','ADJUSTMENT') AND [TripId] IS NULL AND [ProtectionClaimId] IS NULL AND [ClaimRecoveryId] IS NULL) OR ([TransactionType] = 'CONTRIBUTION' AND [TripId] IS NOT NULL AND [ProtectionClaimId] IS NULL AND [ClaimRecoveryId] IS NULL) OR ([TransactionType] IN ('CLAIM_ADVANCE','CLAIM_PAYOUT') AND [TripId] IS NULL AND [ProtectionClaimId] IS NOT NULL AND [ClaimRecoveryId] IS NULL) OR ([TransactionType] IN ('DRIVER_RECOVERY','CUSTOMER_RECOVERY','THIRD_PARTY_RECOVERY','INSURANCE_RECOVERY') AND [TripId] IS NULL AND [ProtectionClaimId] IS NOT NULL AND [ClaimRecoveryId] IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_RiskFundTransactions_AdministrativeAudit",
                    "[TransactionType] NOT IN ('OPENING_BALANCE','ADJUSTMENT') OR ([PerformedByUserId] IS NOT NULL AND [PerformedByUserId] <> '00000000-0000-0000-0000-000000000000' AND [ExternalReference] IS NOT NULL AND LTRIM(RTRIM([ExternalReference])) <> '' AND [EvidenceUrl] IS NOT NULL AND LTRIM(RTRIM([EvidenceUrl])) <> '' AND LTRIM(RTRIM([Reason])) <> '' AND LTRIM(RTRIM([IdempotencyKey])) <> '')");
            });
        });
    }

    private static void Money(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<decimal> property)
        => property.HasColumnType("decimal(18,2)");

    private static void Money(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<decimal?> property)
        => property.HasColumnType("decimal(18,2)");
}
