using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTripArrivalVerificationSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ArrivalDistanceMeters",
                table: "Trips",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ArrivalLatitude",
                table: "Trips",
                type: "decimal(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArrivalLocationVerifiedAt",
                table: "Trips",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ArrivalLongitude",
                table: "Trips",
                type: "decimal(9,6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArrivalDistanceMeters",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "ArrivalLatitude",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "ArrivalLocationVerifiedAt",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "ArrivalLongitude",
                table: "Trips");
        }
    }
}
