using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTripWaitingPaymentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Trips_TripStatus",
                table: "Trips");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Trips_TripStatus",
                table: "Trips",
                sql: "[TripStatus] IN ('ACCEPTED', 'DRIVER_ARRIVING', 'ARRIVED', 'IN_PROGRESS', 'WAITING_RETURN_CONFIRM', 'RETURN_CONFIRMED', 'WAITING_PAYMENT', 'COMPLETED', 'CANCELLED')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Trips_TripStatus",
                table: "Trips");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Trips_TripStatus",
                table: "Trips",
                sql: "[TripStatus] IN ('ACCEPTED', 'DRIVER_ARRIVING', 'ARRIVED', 'IN_PROGRESS', 'WAITING_RETURN_CONFIRM', 'RETURN_CONFIRMED', 'COMPLETED', 'CANCELLED')");
        }
    }
}
