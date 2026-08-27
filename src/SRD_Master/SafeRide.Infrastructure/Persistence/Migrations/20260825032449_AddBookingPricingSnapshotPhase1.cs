using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingPricingSnapshotPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "EstimatedDistanceKm",
                table: "Bookings",
                type: "decimal(18,6)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AcceptedBaseFare",
                table: "Bookings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AcceptedLongDistanceOptInThresholdKm",
                table: "Bookings",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AcceptedLongDistanceRatePerKm",
                table: "Bookings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AcceptedLongDistanceThresholdKm",
                table: "Bookings",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AcceptedMaximumTripDistanceKm",
                table: "Bookings",
                type: "decimal(18,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AcceptedMinimumServiceFare",
                table: "Bookings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AcceptedPricePerHour",
                table: "Bookings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AcceptedPricePerKm",
                table: "Bookings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AcceptedSurgeMultiplier",
                table: "Bookings",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LongDistanceComponent",
                table: "Bookings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NormalFare",
                table: "Bookings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PricingSnapshotVersion",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SurgeAmount",
                table: "Bookings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SurgeEvaluationTime",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SurgedFare",
                table: "Bookings",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bookings_PricingSnapshotAmounts",
                table: "Bookings",
                sql: "[PricingSnapshotVersion] IS NULL OR [PricingSnapshotVersion] = 0 OR ([EstimatedDistanceKm] IS NOT NULL AND [EstimatedDurationMinutes] IS NOT NULL AND [SurgeEvaluationTime] IS NOT NULL AND [AcceptedBaseFare] IS NOT NULL AND [AcceptedBaseFare] >= 0 AND [AcceptedMinimumServiceFare] IS NOT NULL AND [AcceptedMinimumServiceFare] >= 0 AND [AcceptedSurgeMultiplier] IS NOT NULL AND [AcceptedSurgeMultiplier] >= 1 AND [NormalFare] IS NOT NULL AND [NormalFare] >= 0 AND [SurgedFare] IS NOT NULL AND [SurgedFare] >= [NormalFare] AND [SurgeAmount] IS NOT NULL AND [SurgeAmount] = [SurgedFare] - [NormalFare] AND [AcceptedLongDistanceThresholdKm] IS NOT NULL AND [AcceptedLongDistanceThresholdKm] > 0 AND [AcceptedLongDistanceOptInThresholdKm] IS NOT NULL AND [AcceptedLongDistanceOptInThresholdKm] >= [AcceptedLongDistanceThresholdKm] AND [AcceptedMaximumTripDistanceKm] IS NOT NULL AND [AcceptedMaximumTripDistanceKm] >= [AcceptedLongDistanceOptInThresholdKm] AND [AcceptedLongDistanceRatePerKm] IS NOT NULL AND [AcceptedLongDistanceRatePerKm] >= 0 AND [LongDistanceComponent] IS NOT NULL AND [LongDistanceComponent] >= 0 AND [NormalFare] = ROUND([NormalFare], 0) AND [SurgedFare] = ROUND([SurgedFare], 0) AND [SurgeAmount] = ROUND([SurgeAmount], 0) AND [LongDistanceComponent] = ROUND([LongDistanceComponent], 0) AND [EstimatedFare] = ROUND([EstimatedFare], 0) AND [EstimatedFare] = [SurgedFare] + [LongDistanceComponent] AND (([AcceptedPricePerKm] IS NOT NULL AND [AcceptedPricePerKm] > 0 AND [AcceptedPricePerHour] IS NULL AND NULLIF(LTRIM(RTRIM([RoutePolyline])), '') IS NOT NULL) OR ([AcceptedPricePerHour] IS NOT NULL AND [AcceptedPricePerHour] > 0 AND [AcceptedPricePerKm] IS NULL AND [LongDistanceComponent] = 0)))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bookings_PricingSnapshotVersion",
                table: "Bookings",
                sql: "[PricingSnapshotVersion] IS NULL OR [PricingSnapshotVersion] IN (0, 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Bookings_PricingSnapshotAmounts",
                table: "Bookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Bookings_PricingSnapshotVersion",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "AcceptedBaseFare",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "AcceptedLongDistanceOptInThresholdKm",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "AcceptedLongDistanceRatePerKm",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "AcceptedLongDistanceThresholdKm",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "AcceptedMaximumTripDistanceKm",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "AcceptedMinimumServiceFare",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "AcceptedPricePerHour",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "AcceptedPricePerKm",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "AcceptedSurgeMultiplier",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "LongDistanceComponent",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "NormalFare",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PricingSnapshotVersion",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "SurgeAmount",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "SurgeEvaluationTime",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "SurgedFare",
                table: "Bookings");

            migrationBuilder.AlterColumn<decimal>(
                name: "EstimatedDistanceKm",
                table: "Bookings",
                type: "decimal(10,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,6)",
                oldNullable: true);
        }
    }
}
