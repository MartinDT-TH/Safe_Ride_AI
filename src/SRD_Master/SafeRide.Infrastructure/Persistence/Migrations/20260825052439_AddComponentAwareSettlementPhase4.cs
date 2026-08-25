using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddComponentAwareSettlementPhase4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TripFinancialSettlements_NonNegative",
                table: "TripFinancialSettlements");

            migrationBuilder.AddColumn<decimal>(
                name: "AppliedPromotionDiscount",
                table: "TripFinancialSettlements",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ComponentBreakdownVersion",
                table: "TripFinancialSettlements",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DriverFareEarning",
                table: "TripFinancialSettlements",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DriverPayout",
                table: "TripFinancialSettlements",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FareComponent",
                table: "TripFinancialSettlements",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GrossFare",
                table: "TripFinancialSettlements",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LongDistanceComponent",
                table: "TripFinancialSettlements",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LongDistanceEarning",
                table: "TripFinancialSettlements",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LongPickupCompensation",
                table: "TripFinancialSettlements",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SnapshotPromotionDiscount",
                table: "TripFinancialSettlements",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TripFinancialSettlements_ComponentIdentity",
                table: "TripFinancialSettlements",
                sql: "[ComponentBreakdownVersion] IS NULL OR ([ComponentBreakdownVersion] = 1 AND [GrossFare] = [FareComponent] + [LongDistanceComponent] AND [CommissionBase] = [FareComponent] AND [DriverFareEarning] = [FareComponent] - [GrossPlatformCommission] AND [LongDistanceEarning] = [LongDistanceComponent] AND [DriverPayout] = [DriverFareEarning] + [LongDistanceEarning] + [LongPickupCompensation] AND [DriverEarning] = [DriverPayout] AND [PromotionExpense] = [AppliedPromotionDiscount] AND [AppliedPromotionDiscount] <= [GrossFare] AND [AppliedPromotionDiscount] <= [SnapshotPromotionDiscount] AND [CustomerPayableAmount] = [GrossFare] - [AppliedPromotionDiscount] AND [NetPlatformCommission] = [GrossPlatformCommission] - [PromotionExpense] AND [NetOperatingRevenue] = [NetPlatformCommission] - [RiskContribution] - [LongPickupCompensation])");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TripFinancialSettlements_ComponentNullability",
                table: "TripFinancialSettlements",
                sql: "([ComponentBreakdownVersion] IS NULL AND [GrossFare] IS NULL AND [FareComponent] IS NULL AND [LongDistanceComponent] IS NULL AND [SnapshotPromotionDiscount] IS NULL AND [AppliedPromotionDiscount] IS NULL AND [DriverFareEarning] IS NULL AND [LongDistanceEarning] IS NULL AND [LongPickupCompensation] IS NULL AND [DriverPayout] IS NULL) OR ([ComponentBreakdownVersion] IS NOT NULL AND [GrossFare] IS NOT NULL AND [FareComponent] IS NOT NULL AND [LongDistanceComponent] IS NOT NULL AND [SnapshotPromotionDiscount] IS NOT NULL AND [AppliedPromotionDiscount] IS NOT NULL AND [DriverFareEarning] IS NOT NULL AND [LongDistanceEarning] IS NOT NULL AND [LongPickupCompensation] IS NOT NULL AND [DriverPayout] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TripFinancialSettlements_NonNegative",
                table: "TripFinancialSettlements",
                sql: "[CommissionBase] >= 0 AND [PromotionExpense] >= 0 AND [CustomerPayableAmount] >= 0 AND [GrossPlatformCommission] >= 0 AND [DriverEarning] >= 0 AND [RiskContribution] >= 0 AND ([ComponentBreakdownVersion] IS NULL OR ([GrossFare] >= 0 AND [FareComponent] >= 0 AND [LongDistanceComponent] >= 0 AND [SnapshotPromotionDiscount] >= 0 AND [AppliedPromotionDiscount] >= 0 AND [DriverFareEarning] >= 0 AND [LongDistanceEarning] >= 0 AND [LongPickupCompensation] >= 0 AND [DriverPayout] >= 0))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TripFinancialSettlements_ComponentIdentity",
                table: "TripFinancialSettlements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TripFinancialSettlements_ComponentNullability",
                table: "TripFinancialSettlements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TripFinancialSettlements_NonNegative",
                table: "TripFinancialSettlements");

            migrationBuilder.DropColumn(
                name: "AppliedPromotionDiscount",
                table: "TripFinancialSettlements");

            migrationBuilder.DropColumn(
                name: "ComponentBreakdownVersion",
                table: "TripFinancialSettlements");

            migrationBuilder.DropColumn(
                name: "DriverFareEarning",
                table: "TripFinancialSettlements");

            migrationBuilder.DropColumn(
                name: "DriverPayout",
                table: "TripFinancialSettlements");

            migrationBuilder.DropColumn(
                name: "FareComponent",
                table: "TripFinancialSettlements");

            migrationBuilder.DropColumn(
                name: "GrossFare",
                table: "TripFinancialSettlements");

            migrationBuilder.DropColumn(
                name: "LongDistanceComponent",
                table: "TripFinancialSettlements");

            migrationBuilder.DropColumn(
                name: "LongDistanceEarning",
                table: "TripFinancialSettlements");

            migrationBuilder.DropColumn(
                name: "LongPickupCompensation",
                table: "TripFinancialSettlements");

            migrationBuilder.DropColumn(
                name: "SnapshotPromotionDiscount",
                table: "TripFinancialSettlements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TripFinancialSettlements_NonNegative",
                table: "TripFinancialSettlements",
                sql: "[CommissionBase] >= 0 AND [PromotionExpense] >= 0 AND [CustomerPayableAmount] >= 0 AND [GrossPlatformCommission] >= 0 AND [DriverEarning] >= 0 AND [RiskContribution] >= 0");
        }
    }
}
