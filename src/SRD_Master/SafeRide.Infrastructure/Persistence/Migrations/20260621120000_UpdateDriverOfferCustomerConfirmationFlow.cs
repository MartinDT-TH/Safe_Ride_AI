using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260621120000_UpdateDriverOfferCustomerConfirmationFlow")]
    public partial class UpdateDriverOfferCustomerConfirmationFlow : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BookingDriverOffers_OfferStatus",
                table: "BookingDriverOffers");

            migrationBuilder.Sql("""
                UPDATE BookingDriverOffers
                SET OfferStatus = CASE OfferStatus
                    WHEN 'Offered' THEN 'Sent'
                    WHEN 'Pending' THEN 'Sent'
                    WHEN 'Confirmed' THEN 'CustomerConfirmed'
                    WHEN 'Accepted' THEN 'CustomerConfirmed'
                    ELSE OfferStatus
                END
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_BookingDriverOffers_OfferStatus",
                table: "BookingDriverOffers",
                sql: "[OfferStatus] IN ('Sent', 'DriverAccepted', 'CustomerConfirmed', 'Rejected', 'Expired', 'Cancelled')");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_BookingDriverOffers_OfferStatus",
                table: "BookingDriverOffers");

            migrationBuilder.Sql("""
                UPDATE BookingDriverOffers
                SET OfferStatus = CASE OfferStatus
                    WHEN 'DriverAccepted' THEN 'Accepted'
                    WHEN 'CustomerConfirmed' THEN 'Accepted'
                    ELSE OfferStatus
                END
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_BookingDriverOffers_OfferStatus",
                table: "BookingDriverOffers",
                sql: "[OfferStatus] IN ('Pending', 'Sent', 'Accepted', 'Rejected', 'Expired', 'Cancelled')");
        }
    }
}
