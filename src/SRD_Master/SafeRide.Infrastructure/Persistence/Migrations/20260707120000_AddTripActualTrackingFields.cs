using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SafeRide.Infrastructure.Persistence;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260707120000_AddTripActualTrackingFields")]
    public partial class AddTripActualTrackingFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ActualDistanceKm",
                table: "Trips",
                type: "decimal(18, 2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActualDurationMinutes",
                table: "Trips",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndedAt",
                table: "Trips",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Trips_ActualDistanceKm",
                table: "Trips",
                sql: "[ActualDistanceKm] IS NULL OR [ActualDistanceKm] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Trips_ActualDurationMinutes",
                table: "Trips",
                sql: "[ActualDurationMinutes] IS NULL OR [ActualDurationMinutes] >= 0");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Trips_ActualDistanceKm",
                table: "Trips");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Trips_ActualDurationMinutes",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "ActualDistanceKm",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "ActualDurationMinutes",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "EndedAt",
                table: "Trips");
        }
    }
}
