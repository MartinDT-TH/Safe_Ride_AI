using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBookingDriverOfferMatchingStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BookingDriverOffers_OfferStatus",
                table: "BookingDriverOffers");

            migrationBuilder.Sql("""
                UPDATE BookingDriverOffers
                SET OfferStatus = CASE OfferStatus
                    WHEN 'Offered' THEN 'Sent'
                    WHEN 'Confirmed' THEN 'Accepted'
                    ELSE OfferStatus
                END
                """);

            migrationBuilder.AlterColumn<string>(
                name: "OfferStatus",
                table: "BookingDriverOffers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Offered");

            migrationBuilder.CreateIndex(
                name: "IX_BookingDriverOffers_BookingId_DriverId",
                table: "BookingDriverOffers",
                columns: new[] { "BookingId", "DriverId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingDriverOffers_OfferStatus_ExpiresAt",
                table: "BookingDriverOffers",
                columns: new[] { "OfferStatus", "ExpiresAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_BookingDriverOffers_OfferStatus",
                table: "BookingDriverOffers",
                sql: "[OfferStatus] IN ('Pending', 'Sent', 'Accepted', 'Rejected', 'Expired', 'Cancelled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BookingDriverOffers_BookingId_DriverId",
                table: "BookingDriverOffers");

            migrationBuilder.DropIndex(
                name: "IX_BookingDriverOffers_OfferStatus_ExpiresAt",
                table: "BookingDriverOffers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BookingDriverOffers_OfferStatus",
                table: "BookingDriverOffers");

            migrationBuilder.Sql("""
                UPDATE BookingDriverOffers
                SET OfferStatus = CASE OfferStatus
                    WHEN 'Sent' THEN 'Offered'
                    WHEN 'Pending' THEN 'Offered'
                    WHEN 'Accepted' THEN 'Confirmed'
                    WHEN 'Rejected' THEN 'Cancelled'
                    ELSE OfferStatus
                END
                """);

            migrationBuilder.AlterColumn<string>(
                name: "OfferStatus",
                table: "BookingDriverOffers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Offered",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddCheckConstraint(
                name: "CK_BookingDriverOffers_OfferStatus",
                table: "BookingDriverOffers",
                sql: "[OfferStatus] IN ('Offered', 'Confirmed', 'Expired', 'Cancelled')");
        }
    }
}
