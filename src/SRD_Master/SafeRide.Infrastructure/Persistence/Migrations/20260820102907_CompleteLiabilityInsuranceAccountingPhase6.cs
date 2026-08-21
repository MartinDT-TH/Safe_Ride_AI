using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteLiabilityInsuranceAccountingPhase6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProtectionClaims_Amounts",
                table: "ProtectionClaims");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InsuranceClaimProviderAudits_Amounts",
                table: "InsuranceClaimProviderAudits");

            migrationBuilder.RenameColumn(
                name: "InsuranceCoveredAmount",
                table: "ProtectionClaims",
                newName: "InsuranceApprovedAmount");

            migrationBuilder.RenameColumn(
                name: "CoveredAmount",
                table: "InsuranceClaimProviderAudits",
                newName: "ApprovedAmount");

            migrationBuilder.AddColumn<decimal>(
                name: "InsuranceReimbursedToRiskFund",
                table: "ProtectionClaims",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "InsurancePaidDirectToClaimant",
                table: "ProtectionClaims",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "InsurancePaymentDestination",
                table: "ProtectionClaims",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "DIRECT_TO_CLAIMANT");

            migrationBuilder.Sql(
                "UPDATE [ProtectionClaims] SET [InsurancePaidDirectToClaimant] = [InsuranceApprovedAmount]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProtectionClaims_Amounts",
                table: "ProtectionClaims",
                sql: "[TotalDamageAmount] >= 0 AND [EligibleDamageAmount] >= 0 AND [EligibleDamageAmount] <= [TotalDamageAmount] AND [InsuranceRequestedAmount] >= 0 AND [InsuranceApprovedAmount] >= 0 AND [InsuranceApprovedAmount] <= [InsuranceRequestedAmount] AND [InsurancePaidDirectToClaimant] >= 0 AND [InsuranceReimbursedToRiskFund] >= 0 AND [InsurancePaidDirectToClaimant] + [InsuranceReimbursedToRiskFund] <= [InsuranceApprovedAmount] AND ([InsurancePaidDirectToClaimant] = 0 OR [InsuranceReimbursedToRiskFund] = 0) AND (([InsurancePaymentDestination] = 'DIRECT_TO_CLAIMANT' AND [InsuranceReimbursedToRiskFund] = 0) OR ([InsurancePaymentDestination] = 'REIMBURSE_RISK_FUND' AND [InsurancePaidDirectToClaimant] = 0)) AND [RiskFundAdvanceAmount] >= 0 AND [RiskFundPermanentLossAmount] >= 0 AND [DriverLiabilityAmount] >= 0 AND [CustomerLiabilityAmount] >= 0 AND [ThirdPartyLiabilityAmount] >= 0 AND [TotalPaidToClaimant] >= 0 AND [TotalPaidToClaimant] <= [EligibleDamageAmount] AND [RecoveredAmount] >= 0 AND [OutstandingRecoveryAmount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InsuranceClaimProviderAudits_Amounts",
                table: "InsuranceClaimProviderAudits",
                sql: "[RequestedAmount] >= 0 AND [ApprovedAmount] >= 0 AND [ApprovedAmount] <= [RequestedAmount]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProtectionClaims_Amounts",
                table: "ProtectionClaims");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InsuranceClaimProviderAudits_Amounts",
                table: "InsuranceClaimProviderAudits");

            migrationBuilder.DropColumn(
                name: "InsuranceReimbursedToRiskFund",
                table: "ProtectionClaims");

            migrationBuilder.DropColumn(
                name: "InsurancePaidDirectToClaimant",
                table: "ProtectionClaims");

            migrationBuilder.DropColumn(
                name: "InsurancePaymentDestination",
                table: "ProtectionClaims");

            migrationBuilder.RenameColumn(
                name: "InsuranceApprovedAmount",
                table: "ProtectionClaims",
                newName: "InsuranceCoveredAmount");

            migrationBuilder.RenameColumn(
                name: "ApprovedAmount",
                table: "InsuranceClaimProviderAudits",
                newName: "CoveredAmount");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProtectionClaims_Amounts",
                table: "ProtectionClaims",
                sql: "[TotalDamageAmount] >= 0 AND [EligibleDamageAmount] >= 0 AND [EligibleDamageAmount] <= [TotalDamageAmount] AND [InsuranceRequestedAmount] >= 0 AND [InsuranceCoveredAmount] >= 0 AND [InsuranceCoveredAmount] <= [InsuranceRequestedAmount] AND [RiskFundAdvanceAmount] >= 0 AND [RiskFundPermanentLossAmount] >= 0 AND [DriverLiabilityAmount] >= 0 AND [CustomerLiabilityAmount] >= 0 AND [ThirdPartyLiabilityAmount] >= 0 AND [TotalPaidToClaimant] >= 0 AND [TotalPaidToClaimant] <= [EligibleDamageAmount] AND [RecoveredAmount] >= 0 AND [OutstandingRecoveryAmount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InsuranceClaimProviderAudits_Amounts",
                table: "InsuranceClaimProviderAudits",
                sql: "[RequestedAmount] >= 0 AND [CoveredAmount] >= 0 AND [CoveredAmount] <= [RequestedAmount]");
        }
    }
}
