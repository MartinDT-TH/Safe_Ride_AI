using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRiskProtectionAndAccidentManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SafetyTerminatedAt",
                table: "Trips",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SafetyTerminationReason",
                table: "Trips",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TerminationCategory",
                table: "Trips",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EscalationRequested",
                table: "Reports",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "Reports",
                type: "decimal(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "Reports",
                type: "decimal(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OccurredAtUtc",
                table: "Reports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PreTripVehicleCheckId",
                table: "Reports",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonCode",
                table: "Reports",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportType",
                table: "Reports",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "GENERAL");

            migrationBuilder.CreateTable(
                name: "AccidentReports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TripId = table.Column<long>(type: "bigint", nullable: false),
                    ReportedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    PoliceReportReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccidentReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccidentReports_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PreTripVehicleChecks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TripId = table.Column<long>(type: "bigint", nullable: false),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrakeResponsePassed = table.Column<bool>(type: "bit", nullable: false),
                    FrontRearLightsPassed = table.Column<bool>(type: "bit", nullable: false),
                    TurnSignalsPassed = table.Column<bool>(type: "bit", nullable: false),
                    VisibleTiresPassed = table.Column<bool>(type: "bit", nullable: false),
                    DashboardWarningPassed = table.Column<bool>(type: "bit", nullable: false),
                    WindshieldVisibilityPassed = table.Column<bool>(type: "bit", nullable: false),
                    NoMajorVisibleIssue = table.Column<bool>(type: "bit", nullable: false),
                    Result = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FaultType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EvidenceUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CheckedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreTripVehicleChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PreTripVehicleChecks_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskFundAccounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskFundAccounts", x => x.Id);
                    table.CheckConstraint("CK_RiskFundAccounts_Balance", "[CurrentBalance] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "RiskProtectionPolicyVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BasePlatformCommissionRate = table.Column<decimal>(type: "decimal(7,6)", nullable: false),
                    RiskReserveRate = table.Column<decimal>(type: "decimal(7,6)", nullable: false),
                    DefaultProtectionLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DriverOrdinaryNegligenceRate = table.Column<decimal>(type: "decimal(7,6)", nullable: false),
                    DriverOrdinaryNegligenceCap = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DriverGrossNegligenceRate = table.Column<decimal>(type: "decimal(7,6)", nullable: false),
                    DriverGrossNegligenceCap = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MockInsuranceCoverageLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ClaimAutoApprovalThreshold = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RiskFundEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskProtectionPolicyVersions", x => x.Id);
                    table.CheckConstraint("CK_RiskProtectionPolicy_CommissionRate", "[BasePlatformCommissionRate] >= 0 AND [BasePlatformCommissionRate] <= 1");
                    table.CheckConstraint("CK_RiskProtectionPolicy_NegligenceRates", "[DriverOrdinaryNegligenceRate] >= 0 AND [DriverOrdinaryNegligenceRate] <= 1 AND [DriverGrossNegligenceRate] >= 0 AND [DriverGrossNegligenceRate] <= 1");
                    table.CheckConstraint("CK_RiskProtectionPolicy_ReserveRate", "[RiskReserveRate] >= 0 AND [RiskReserveRate] <= 1");
                });

            migrationBuilder.CreateTable(
                name: "VehicleInsurancePolicies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VehicleId = table.Column<long>(type: "bigint", nullable: false),
                    InsuranceType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PolicyNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CoverageAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Deductible = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DocumentUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    VerificationStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleInsurancePolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleInsurancePolicies_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccidentEvidence",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccidentReportId = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvidenceType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FileUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccidentEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccidentEvidence_AccidentReports_AccidentReportId",
                        column: x => x.AccidentReportId,
                        principalTable: "AccidentReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AccidentLiabilityAssessments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccidentReportId = table.Column<long>(type: "bigint", nullable: false),
                    DriverFaultPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    CustomerFaultPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    ThirdPartyFaultPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    VehicleFailurePercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    ObjectiveCausePercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    DriverFaultLevel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    VehicleDefectAwareness = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ConfirmedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DisputeReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccidentLiabilityAssessments", x => x.Id);
                    table.CheckConstraint("CK_AccidentLiabilityAssessment_Total", "[DriverFaultPercentage] + [CustomerFaultPercentage] + [ThirdPartyFaultPercentage] + [VehicleFailurePercentage] + [ObjectiveCausePercentage] = 100");
                    table.ForeignKey(
                        name: "FK_AccidentLiabilityAssessments_AccidentReports_AccidentReportId",
                        column: x => x.AccidentReportId,
                        principalTable: "AccidentReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProtectionClaims",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccidentReportId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InsuranceStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InsuranceReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TotalDamageAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EligibleDamageAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InsuranceCoveredAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RiskFundAdvanceAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RiskFundPermanentLossAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DriverLiabilityAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CustomerLiabilityAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ThirdPartyLiabilityAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPaidToClaimant = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RecoveredAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OutstandingRecoveryAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProtectionClaims", x => x.Id);
                    table.CheckConstraint("CK_ProtectionClaims_Amounts", "[TotalDamageAmount] >= 0 AND [EligibleDamageAmount] >= 0 AND [TotalPaidToClaimant] >= 0 AND [RecoveredAmount] >= 0 AND [OutstandingRecoveryAmount] >= 0");
                    table.ForeignKey(
                        name: "FK_ProtectionClaims_AccidentReports_AccidentReportId",
                        column: x => x.AccidentReportId,
                        principalTable: "AccidentReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TripFinancialSettlements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TripId = table.Column<long>(type: "bigint", nullable: false),
                    PolicyVersionId = table.Column<long>(type: "bigint", nullable: false),
                    CommissionBase = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PromotionExpense = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CustomerPayableAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PlatformCommissionRate = table.Column<decimal>(type: "decimal(7,6)", nullable: false),
                    GrossPlatformCommission = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DriverEarning = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetPlatformCommission = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RiskReserveRate = table.Column<decimal>(type: "decimal(7,6)", nullable: false),
                    RiskContribution = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetOperatingRevenue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsRiskContributionEligible = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SettledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripFinancialSettlements", x => x.Id);
                    table.CheckConstraint("CK_TripFinancialSettlements_NonNegative", "[CommissionBase] >= 0 AND [PromotionExpense] >= 0 AND [CustomerPayableAmount] >= 0 AND [GrossPlatformCommission] >= 0 AND [DriverEarning] >= 0 AND [RiskContribution] >= 0");
                    table.ForeignKey(
                        name: "FK_TripFinancialSettlements_RiskProtectionPolicyVersions_PolicyVersionId",
                        column: x => x.PolicyVersionId,
                        principalTable: "RiskProtectionPolicyVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TripFinancialSettlements_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TripProtectionCoverages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TripId = table.Column<long>(type: "bigint", nullable: false),
                    PolicyVersionId = table.Column<long>(type: "bigint", nullable: false),
                    PreTripVehicleCheckId = table.Column<long>(type: "bigint", nullable: false),
                    ProtectionLimit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    VehicleInsurancePolicyId = table.Column<long>(type: "bigint", nullable: true),
                    InsuranceProviderSnapshot = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PolicyNumberSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InsuranceCoverageSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    InsuranceDeductibleSnapshot = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ActivatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripProtectionCoverages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TripProtectionCoverages_PreTripVehicleChecks_PreTripVehicleCheckId",
                        column: x => x.PreTripVehicleCheckId,
                        principalTable: "PreTripVehicleChecks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TripProtectionCoverages_RiskProtectionPolicyVersions_PolicyVersionId",
                        column: x => x.PolicyVersionId,
                        principalTable: "RiskProtectionPolicyVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TripProtectionCoverages_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TripProtectionCoverages_VehicleInsurancePolicies_VehicleInsurancePolicyId",
                        column: x => x.VehicleInsurancePolicyId,
                        principalTable: "VehicleInsurancePolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AccidentLiabilityCauses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssessmentId = table.Column<long>(type: "bigint", nullable: false),
                    RootCause = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ResponsibleParty = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Percentage = table.Column<decimal>(type: "decimal(5,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccidentLiabilityCauses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccidentLiabilityCauses_AccidentLiabilityAssessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "AccidentLiabilityAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClaimRecoveries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProtectionClaimId = table.Column<long>(type: "bigint", nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EvidenceUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimRecoveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimRecoveries_ProtectionClaims_ProtectionClaimId",
                        column: x => x.ProtectionClaimId,
                        principalTable: "ProtectionClaims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DriverLiabilities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProtectionClaimId = table.Column<long>(type: "bigint", nullable: false),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverAttributableEligibleDamage = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FaultLevel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AppliedRate = table.Column<decimal>(type: "decimal(7,6)", nullable: false),
                    AppliedCap = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ConfirmedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OutstandingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DisputeReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverLiabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DriverLiabilities_ProtectionClaims_ProtectionClaimId",
                        column: x => x.ProtectionClaimId,
                        principalTable: "ProtectionClaims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskFundTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RiskFundAccountId = table.Column<long>(type: "bigint", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceBefore = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TripId = table.Column<long>(type: "bigint", nullable: true),
                    ProtectionClaimId = table.Column<long>(type: "bigint", nullable: true),
                    ClaimRecoveryId = table.Column<long>(type: "bigint", nullable: true),
                    PerformedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExternalReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EvidenceUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskFundTransactions", x => x.Id);
                    table.CheckConstraint("CK_RiskFundTransactions_AdministrativeAudit", "[TransactionType] NOT IN ('OPENING_BALANCE','ADJUSTMENT') OR ([PerformedByUserId] IS NOT NULL AND [ExternalReference] IS NOT NULL AND [ExternalReference] <> '' AND [EvidenceUrl] IS NOT NULL AND [EvidenceUrl] <> '')");
                    table.CheckConstraint("CK_RiskFundTransactions_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_RiskFundTransactions_Balance", "[BalanceBefore] >= 0 AND [BalanceAfter] >= 0");
                    table.CheckConstraint("CK_RiskFundTransactions_BalanceMovement", "([Direction] = 'CREDIT' AND [BalanceAfter] = [BalanceBefore] + [Amount]) OR ([Direction] = 'DEBIT' AND [BalanceAfter] = [BalanceBefore] - [Amount])");
                    table.CheckConstraint("CK_RiskFundTransactions_TypeDirection", "([TransactionType] IN ('OPENING_BALANCE','CONTRIBUTION','DRIVER_RECOVERY','CUSTOMER_RECOVERY','THIRD_PARTY_RECOVERY','INSURANCE_RECOVERY') AND [Direction] = 'CREDIT') OR ([TransactionType] IN ('CLAIM_ADVANCE','CLAIM_PAYOUT') AND [Direction] = 'DEBIT') OR [TransactionType] = 'ADJUSTMENT'");
                    table.CheckConstraint("CK_RiskFundTransactions_TypeLinks", "([TransactionType] IN ('OPENING_BALANCE','ADJUSTMENT') AND [TripId] IS NULL AND [ProtectionClaimId] IS NULL AND [ClaimRecoveryId] IS NULL) OR ([TransactionType] = 'CONTRIBUTION' AND [TripId] IS NOT NULL AND [ProtectionClaimId] IS NULL AND [ClaimRecoveryId] IS NULL) OR ([TransactionType] IN ('CLAIM_ADVANCE','CLAIM_PAYOUT') AND [TripId] IS NULL AND [ProtectionClaimId] IS NOT NULL AND [ClaimRecoveryId] IS NULL) OR ([TransactionType] IN ('DRIVER_RECOVERY','CUSTOMER_RECOVERY','THIRD_PARTY_RECOVERY','INSURANCE_RECOVERY') AND [TripId] IS NULL AND [ProtectionClaimId] IS NOT NULL AND [ClaimRecoveryId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_RiskFundTransactions_ClaimRecoveries_ClaimRecoveryId",
                        column: x => x.ClaimRecoveryId,
                        principalTable: "ClaimRecoveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskFundTransactions_ProtectionClaims_ProtectionClaimId",
                        column: x => x.ProtectionClaimId,
                        principalTable: "ProtectionClaims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskFundTransactions_RiskFundAccounts_RiskFundAccountId",
                        column: x => x.RiskFundAccountId,
                        principalTable: "RiskFundAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RiskFundTransactions_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "RiskFundAccounts",
                columns: new[] { "Id", "CurrentBalance", "UpdatedAtUtc" },
                values: new object[] { 1L, 0m, new DateTime(2000, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "RiskProtectionPolicyVersions",
                columns: new[] { "Id", "BasePlatformCommissionRate", "ChangeReason", "ClaimAutoApprovalThreshold", "CreatedAtUtc", "CreatedByUserId", "DefaultProtectionLimit", "DriverGrossNegligenceCap", "DriverGrossNegligenceRate", "DriverOrdinaryNegligenceCap", "DriverOrdinaryNegligenceRate", "EffectiveFromUtc", "MockInsuranceCoverageLimit", "RiskFundEnabled", "RiskReserveRate" },
                values: new object[] { 1L, 0.30m, "Legacy 30 percent commission baseline; risk protection disabled", 0m, new DateTime(2000, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 0m, 0m, 0m, 0m, 0m, new DateTime(2000, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0m, false, 0m });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_PreTripVehicleCheckId",
                table: "Reports",
                column: "PreTripVehicleCheckId");

            migrationBuilder.CreateIndex(
                name: "IX_AccidentEvidence_AccidentReportId",
                table: "AccidentEvidence",
                column: "AccidentReportId");

            migrationBuilder.CreateIndex(
                name: "IX_AccidentLiabilityAssessments_AccidentReportId",
                table: "AccidentLiabilityAssessments",
                column: "AccidentReportId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccidentLiabilityCauses_AssessmentId_RootCause_ResponsibleParty",
                table: "AccidentLiabilityCauses",
                columns: new[] { "AssessmentId", "RootCause", "ResponsibleParty" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccidentReports_TripId_Status_CreatedAtUtc",
                table: "AccidentReports",
                columns: new[] { "TripId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ClaimRecoveries_IdempotencyKey",
                table: "ClaimRecoveries",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimRecoveries_ProtectionClaimId",
                table: "ClaimRecoveries",
                column: "ProtectionClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverLiabilities_ProtectionClaimId_DriverId",
                table: "DriverLiabilities",
                columns: new[] { "ProtectionClaimId", "DriverId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreTripVehicleChecks_TripId_CheckedAtUtc",
                table: "PreTripVehicleChecks",
                columns: new[] { "TripId", "CheckedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProtectionClaims_AccidentReportId",
                table: "ProtectionClaims",
                column: "AccidentReportId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiskFundTransactions_ClaimRecoveryId",
                table: "RiskFundTransactions",
                column: "ClaimRecoveryId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskFundTransactions_CreatedAtUtc",
                table: "RiskFundTransactions",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RiskFundTransactions_IdempotencyKey",
                table: "RiskFundTransactions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RiskFundTransactions_ProtectionClaimId",
                table: "RiskFundTransactions",
                column: "ProtectionClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskFundTransactions_RiskFundAccountId_TransactionType",
                table: "RiskFundTransactions",
                columns: new[] { "RiskFundAccountId", "TransactionType" },
                unique: true,
                filter: "[TransactionType] = 'OPENING_BALANCE'");

            migrationBuilder.CreateIndex(
                name: "IX_RiskFundTransactions_TripId_TransactionType",
                table: "RiskFundTransactions",
                columns: new[] { "TripId", "TransactionType" },
                unique: true,
                filter: "[TripId] IS NOT NULL AND [TransactionType] = 'CONTRIBUTION'");

            migrationBuilder.CreateIndex(
                name: "IX_RiskProtectionPolicyVersions_EffectiveFromUtc",
                table: "RiskProtectionPolicyVersions",
                column: "EffectiveFromUtc",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripFinancialSettlements_PolicyVersionId",
                table: "TripFinancialSettlements",
                column: "PolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_TripFinancialSettlements_TripId",
                table: "TripFinancialSettlements",
                column: "TripId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripProtectionCoverages_PolicyVersionId",
                table: "TripProtectionCoverages",
                column: "PolicyVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_TripProtectionCoverages_PreTripVehicleCheckId",
                table: "TripProtectionCoverages",
                column: "PreTripVehicleCheckId");

            migrationBuilder.CreateIndex(
                name: "IX_TripProtectionCoverages_TripId",
                table: "TripProtectionCoverages",
                column: "TripId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripProtectionCoverages_VehicleInsurancePolicyId",
                table: "TripProtectionCoverages",
                column: "VehicleInsurancePolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleInsurancePolicies_Provider_PolicyNumber",
                table: "VehicleInsurancePolicies",
                columns: new[] { "Provider", "PolicyNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleInsurancePolicies_VehicleId",
                table: "VehicleInsurancePolicies",
                column: "VehicleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_PreTripVehicleChecks_PreTripVehicleCheckId",
                table: "Reports",
                column: "PreTripVehicleCheckId",
                principalTable: "PreTripVehicleChecks",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reports_PreTripVehicleChecks_PreTripVehicleCheckId",
                table: "Reports");

            migrationBuilder.DropTable(
                name: "AccidentEvidence");

            migrationBuilder.DropTable(
                name: "AccidentLiabilityCauses");

            migrationBuilder.DropTable(
                name: "DriverLiabilities");

            migrationBuilder.DropTable(
                name: "RiskFundTransactions");

            migrationBuilder.DropTable(
                name: "TripFinancialSettlements");

            migrationBuilder.DropTable(
                name: "TripProtectionCoverages");

            migrationBuilder.DropTable(
                name: "AccidentLiabilityAssessments");

            migrationBuilder.DropTable(
                name: "ClaimRecoveries");

            migrationBuilder.DropTable(
                name: "RiskFundAccounts");

            migrationBuilder.DropTable(
                name: "PreTripVehicleChecks");

            migrationBuilder.DropTable(
                name: "RiskProtectionPolicyVersions");

            migrationBuilder.DropTable(
                name: "VehicleInsurancePolicies");

            migrationBuilder.DropTable(
                name: "ProtectionClaims");

            migrationBuilder.DropTable(
                name: "AccidentReports");

            migrationBuilder.DropIndex(
                name: "IX_Reports_PreTripVehicleCheckId",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "SafetyTerminatedAt",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "SafetyTerminationReason",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "TerminationCategory",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "EscalationRequested",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "OccurredAtUtc",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "PreTripVehicleCheckId",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "ReportType",
                table: "Reports");
        }
    }
}
