using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTripFareFinalizationPhase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DestinationReached",
                table: "Trips",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FareFinalizedAtUtc",
                table: "Trips",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalizationLatitude",
                table: "Trips",
                type: "decimal(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalizationLongitude",
                table: "Trips",
                type: "decimal(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PlannedRouteProgress",
                table: "Trips",
                type: "decimal(7,6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EndReason",
                table: "Trips",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Trips_PlannedRouteProgress",
                table: "Trips",
                sql: "[PlannedRouteProgress] IS NULL OR ([PlannedRouteProgress] >= 0 AND [PlannedRouteProgress] <= 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Trips_PlannedRouteProgress",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "DestinationReached",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "FareFinalizedAtUtc",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "FinalizationLatitude",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "FinalizationLongitude",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "PlannedRouteProgress",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "EndReason",
                table: "Trips");
        }
    }
}
