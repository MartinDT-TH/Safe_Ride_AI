using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteClaimFundingReconciliationPhase7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProtectionClaims_Amounts",
                table: "ProtectionClaims");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ClaimRecoveries_Audit",
                table: "ClaimRecoveries");

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAtUtc",
                table: "ProtectionClaims",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClosedByUserId",
                table: "ProtectionClaims",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WrittenOffAdvanceAmount",
                table: "ProtectionClaims",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceContentType",
                table: "ClaimRecoveries",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "EvidenceFileSizeBytes",
                table: "ClaimRecoveries",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceOriginalFileName",
                table: "ClaimRecoveries",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EvidenceStoragePublicId",
                table: "ClaimRecoveries",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PayerReference",
                table: "ClaimRecoveries",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE [ClaimRecoveries]
                SET [PayerReference] = CONCAT('LEGACY-', [SourceType], '-', [Id]),
                    [EvidenceStoragePublicId] = CONCAT('legacy-unverified/claim-recovery/', [Id]),
                    [EvidenceOriginalFileName] = CONCAT('legacy-recovery-', [Id], '.bin'),
                    [EvidenceContentType] = 'application/octet-stream',
                    [EvidenceFileSizeBytes] = 1;
                """);

            migrationBuilder.CreateTable(
                name: "ClaimReconciliationRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProtectionClaimId = table.Column<long>(type: "bigint", nullable: false),
                    ReconciliationType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EvidenceUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EvidenceStoragePublicId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EvidenceOriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    EvidenceContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EvidenceFileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimReconciliationRecords", x => x.Id);
                    table.CheckConstraint("CK_ClaimReconciliationRecords_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_ClaimReconciliationRecords_Audit", "[Reason] <> '' AND [EvidenceUrl] <> '' AND [EvidenceStoragePublicId] <> '' AND [EvidenceOriginalFileName] <> '' AND [EvidenceContentType] <> '' AND [EvidenceFileSizeBytes] > 0 AND [RecordedByUserId] <> '00000000-0000-0000-0000-000000000000' AND [IdempotencyKey] <> ''");
                    table.ForeignKey(
                        name: "FK_ClaimReconciliationRecords_ProtectionClaims_ProtectionClaimId",
                        column: x => x.ProtectionClaimId,
                        principalTable: "ProtectionClaims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProtectionClaims_Amounts",
                table: "ProtectionClaims",
                sql: "[TotalDamageAmount] >= 0 AND [EligibleDamageAmount] >= 0 AND [EligibleDamageAmount] <= [TotalDamageAmount] AND [InsuranceRequestedAmount] >= 0 AND [InsuranceApprovedAmount] >= 0 AND [InsuranceApprovedAmount] <= [InsuranceRequestedAmount] AND [InsurancePaidDirectToClaimant] >= 0 AND [InsuranceReimbursedToRiskFund] >= 0 AND [InsurancePaidDirectToClaimant] + [InsuranceReimbursedToRiskFund] <= [InsuranceApprovedAmount] AND ([InsurancePaidDirectToClaimant] = 0 OR [InsuranceReimbursedToRiskFund] = 0) AND (([InsurancePaymentDestination] = 'DIRECT_TO_CLAIMANT' AND [InsuranceReimbursedToRiskFund] = 0) OR ([InsurancePaymentDestination] = 'REIMBURSE_RISK_FUND' AND [InsurancePaidDirectToClaimant] = 0)) AND [RiskFundAdvanceAmount] >= 0 AND [RiskFundPermanentLossAmount] >= 0 AND [DriverLiabilityAmount] >= 0 AND [CustomerLiabilityAmount] >= 0 AND [ThirdPartyLiabilityAmount] >= 0 AND [TotalPaidToClaimant] >= 0 AND [TotalPaidToClaimant] <= [EligibleDamageAmount] AND [RecoveredAmount] >= 0 AND [OutstandingRecoveryAmount] >= 0 AND [WrittenOffAdvanceAmount] >= 0 AND [RecoveredAmount] + [OutstandingRecoveryAmount] + [WrittenOffAdvanceAmount] <= [RiskFundAdvanceAmount]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ClaimRecoveries_Audit",
                table: "ClaimRecoveries",
                sql: "[PayerReference] <> '' AND [PaymentReference] <> '' AND [EvidenceUrl] <> '' AND [EvidenceStoragePublicId] <> '' AND [EvidenceOriginalFileName] <> '' AND [EvidenceContentType] <> '' AND [EvidenceFileSizeBytes] > 0 AND [RecordedByUserId] <> '00000000-0000-0000-0000-000000000000' AND [IdempotencyKey] <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimReconciliationRecords_IdempotencyKey",
                table: "ClaimReconciliationRecords",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClaimReconciliationRecords_ProtectionClaimId_RecordedAtUtc",
                table: "ClaimReconciliationRecords",
                columns: new[] { "ProtectionClaimId", "RecordedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaimReconciliationRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProtectionClaims_Amounts",
                table: "ProtectionClaims");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ClaimRecoveries_Audit",
                table: "ClaimRecoveries");

            migrationBuilder.DropColumn(
                name: "ClosedAtUtc",
                table: "ProtectionClaims");

            migrationBuilder.DropColumn(
                name: "ClosedByUserId",
                table: "ProtectionClaims");

            migrationBuilder.DropColumn(
                name: "WrittenOffAdvanceAmount",
                table: "ProtectionClaims");

            migrationBuilder.DropColumn(
                name: "EvidenceContentType",
                table: "ClaimRecoveries");

            migrationBuilder.DropColumn(
                name: "EvidenceFileSizeBytes",
                table: "ClaimRecoveries");

            migrationBuilder.DropColumn(
                name: "EvidenceOriginalFileName",
                table: "ClaimRecoveries");

            migrationBuilder.DropColumn(
                name: "EvidenceStoragePublicId",
                table: "ClaimRecoveries");

            migrationBuilder.DropColumn(
                name: "PayerReference",
                table: "ClaimRecoveries");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProtectionClaims_Amounts",
                table: "ProtectionClaims",
                sql: "[TotalDamageAmount] >= 0 AND [EligibleDamageAmount] >= 0 AND [EligibleDamageAmount] <= [TotalDamageAmount] AND [InsuranceRequestedAmount] >= 0 AND [InsuranceApprovedAmount] >= 0 AND [InsuranceApprovedAmount] <= [InsuranceRequestedAmount] AND [InsurancePaidDirectToClaimant] >= 0 AND [InsuranceReimbursedToRiskFund] >= 0 AND [InsurancePaidDirectToClaimant] + [InsuranceReimbursedToRiskFund] <= [InsuranceApprovedAmount] AND ([InsurancePaidDirectToClaimant] = 0 OR [InsuranceReimbursedToRiskFund] = 0) AND (([InsurancePaymentDestination] = 'DIRECT_TO_CLAIMANT' AND [InsuranceReimbursedToRiskFund] = 0) OR ([InsurancePaymentDestination] = 'REIMBURSE_RISK_FUND' AND [InsurancePaidDirectToClaimant] = 0)) AND [RiskFundAdvanceAmount] >= 0 AND [RiskFundPermanentLossAmount] >= 0 AND [DriverLiabilityAmount] >= 0 AND [CustomerLiabilityAmount] >= 0 AND [ThirdPartyLiabilityAmount] >= 0 AND [TotalPaidToClaimant] >= 0 AND [TotalPaidToClaimant] <= [EligibleDamageAmount] AND [RecoveredAmount] >= 0 AND [OutstandingRecoveryAmount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ClaimRecoveries_Audit",
                table: "ClaimRecoveries",
                sql: "[PaymentReference] <> '' AND [EvidenceUrl] <> '' AND [RecordedByUserId] <> '00000000-0000-0000-0000-000000000000' AND [IdempotencyKey] <> ''");
        }
    }
}
