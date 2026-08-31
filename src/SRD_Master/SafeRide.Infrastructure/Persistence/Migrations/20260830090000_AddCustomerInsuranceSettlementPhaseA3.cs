using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SafeRide.Infrastructure.Persistence;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260830090000_AddCustomerInsuranceSettlementPhaseA3")]
public partial class AddCustomerInsuranceSettlementPhaseA3 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "CustomerInsuranceAppliedAmount",
            table: "ProtectionClaims",
            type: "decimal(18,2)",
            nullable: false,
            defaultValue: 0m);
        migrationBuilder.AddColumn<string>(
            name: "CustomerInsuranceReference",
            table: "ProtectionClaims",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);
        migrationBuilder.AddColumn<DateTime>(
            name: "CustomerInsuranceConfirmedAtUtc",
            table: "ProtectionClaims",
            type: "datetime2",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "CustomerInsuranceNote",
            table: "ProtectionClaims",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.DropCheckConstraint(
            name: "CK_ProtectionClaims_Amounts",
            table: "ProtectionClaims");
        migrationBuilder.AddCheckConstraint(
            name: "CK_ProtectionClaims_Amounts",
            table: "ProtectionClaims",
            sql: AmountConstraintSql);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_ProtectionClaims_Amounts",
            table: "ProtectionClaims");
        migrationBuilder.DropColumn(
            name: "CustomerInsuranceAppliedAmount",
            table: "ProtectionClaims");
        migrationBuilder.DropColumn(
            name: "CustomerInsuranceReference",
            table: "ProtectionClaims");
        migrationBuilder.DropColumn(
            name: "CustomerInsuranceConfirmedAtUtc",
            table: "ProtectionClaims");
        migrationBuilder.DropColumn(
            name: "CustomerInsuranceNote",
            table: "ProtectionClaims");
        migrationBuilder.AddCheckConstraint(
            name: "CK_ProtectionClaims_Amounts",
            table: "ProtectionClaims",
            sql: LegacyAmountConstraintSql);
    }

    private const string AmountConstraintSql =
        "[TotalDamageAmount] >= 0 AND [EligibleDamageAmount] >= 0 AND [EligibleDamageAmount] <= [TotalDamageAmount] " +
        "AND [CustomerInsuranceAppliedAmount] >= 0 " +
        "AND [InsuranceRequestedAmount] >= 0 AND [InsuranceApprovedAmount] >= 0 AND [InsuranceApprovedAmount] <= [InsuranceRequestedAmount] " +
        "AND [InsurancePaidDirectToClaimant] >= 0 AND [InsuranceReimbursedToRiskFund] >= 0 " +
        "AND [InsurancePaidDirectToClaimant] + [InsuranceReimbursedToRiskFund] <= [InsuranceApprovedAmount] " +
        "AND ([InsurancePaidDirectToClaimant] = 0 OR [InsuranceReimbursedToRiskFund] = 0) " +
        "AND (([InsurancePaymentDestination] = 'DIRECT_TO_CLAIMANT' AND [InsuranceReimbursedToRiskFund] = 0) OR ([InsurancePaymentDestination] = 'REIMBURSE_RISK_FUND' AND [InsurancePaidDirectToClaimant] = 0)) " +
        "AND [RiskFundAdvanceAmount] >= 0 AND [RiskFundPermanentLossAmount] >= 0 " +
        "AND [DriverLiabilityAmount] >= 0 AND [CustomerLiabilityAmount] >= 0 AND [ThirdPartyLiabilityAmount] >= 0 " +
        "AND [TotalPaidToClaimant] >= 0 AND [TotalPaidToClaimant] <= [EligibleDamageAmount] " +
        "AND [RecoveredAmount] >= 0 AND [OutstandingRecoveryAmount] >= 0 AND [WrittenOffAdvanceAmount] >= 0 " +
        "AND [RecoveredAmount] + [OutstandingRecoveryAmount] + [WrittenOffAdvanceAmount] <= [RiskFundAdvanceAmount]";

    private const string LegacyAmountConstraintSql =
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
        "AND [RecoveredAmount] + [OutstandingRecoveryAmount] + [WrittenOffAdvanceAmount] <= [RiskFundAdvanceAmount]";
}
