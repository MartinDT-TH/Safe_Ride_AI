using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverMatchingCompensationEligibilityPhase3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AcceptLongDistanceTrips",
                table: "DriverProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AcceptLongPickupTrips",
                table: "DriverProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LongPickupCompensation",
                table: "BookingDriverOffers",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PickupDistanceKm",
                table: "BookingDriverOffers",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_BookingDriverOffers_LongPickupCompensation",
                table: "BookingDriverOffers",
                sql: "[LongPickupCompensation] IS NULL OR ([LongPickupCompensation] >= 0 AND [LongPickupCompensation] = ROUND([LongPickupCompensation], 0))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BookingDriverOffers_PickupDistanceKm",
                table: "BookingDriverOffers",
                sql: "[PickupDistanceKm] IS NULL OR [PickupDistanceKm] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BookingDriverOffers_LongPickupCompensation",
                table: "BookingDriverOffers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BookingDriverOffers_PickupDistanceKm",
                table: "BookingDriverOffers");

            migrationBuilder.DropColumn(
                name: "AcceptLongDistanceTrips",
                table: "DriverProfiles");

            migrationBuilder.DropColumn(
                name: "AcceptLongPickupTrips",
                table: "DriverProfiles");

            migrationBuilder.DropColumn(
                name: "LongPickupCompensation",
                table: "BookingDriverOffers");

            migrationBuilder.DropColumn(
                name: "PickupDistanceKm",
                table: "BookingDriverOffers");
        }
    }
}
