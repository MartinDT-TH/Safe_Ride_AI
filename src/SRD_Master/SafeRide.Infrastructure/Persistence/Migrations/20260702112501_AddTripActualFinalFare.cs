using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTripActualFinalFare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Trips_TripStatus",
                table: "Trips");

            migrationBuilder.AddColumn<decimal>(
                name: "ActualFare",
                table: "Trips",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalFare",
                table: "Trips",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Trips_ActualFare",
                table: "Trips",
                sql: "[ActualFare] IS NULL OR [ActualFare] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Trips_FinalFare",
                table: "Trips",
                sql: "[FinalFare] IS NULL OR [FinalFare] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Trips_TripStatus",
                table: "Trips",
                sql: "[TripStatus] IN ('ACCEPTED', 'DRIVER_ARRIVING', 'ARRIVED', 'IN_PROGRESS', 'WAITING_RETURN_CONFIRM', 'RETURN_CONFIRMED', 'COMPLETED', 'CANCELLED')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Trips_ActualFare",
                table: "Trips");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Trips_FinalFare",
                table: "Trips");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Trips_TripStatus",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "ActualFare",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "FinalFare",
                table: "Trips");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Trips_TripStatus",
                table: "Trips",
                sql: "[TripStatus] IN ('ACCEPTED', 'DRIVER_ARRIVING', 'ARRIVED', 'IN_PROGRESS', 'COMPLETED', 'CANCELLED')");
        }
    }
}
