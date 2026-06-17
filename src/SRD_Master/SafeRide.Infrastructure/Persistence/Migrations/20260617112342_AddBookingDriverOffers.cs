using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingDriverOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookingDriverOffers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookingId = table.Column<long>(type: "bigint", nullable: false),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfferStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Offered"),
                    OfferedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiredAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingDriverOffers", x => x.Id);
                    table.CheckConstraint("CK_BookingDriverOffers_ExpiresAt", "[ExpiresAt] > [OfferedAt]");
                    table.CheckConstraint("CK_BookingDriverOffers_OfferStatus", "[OfferStatus] IN ('Offered', 'Confirmed', 'Expired', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_BookingDriverOffers_Bookings",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_BookingDriverOffers_DriverProfiles",
                        column: x => x.DriverId,
                        principalTable: "DriverProfiles",
                        principalColumn: "DriverId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingDriverOffers_BookingId",
                table: "BookingDriverOffers",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingDriverOffers_BookingId_OfferStatus",
                table: "BookingDriverOffers",
                columns: new[] { "BookingId", "OfferStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingDriverOffers_DriverId",
                table: "BookingDriverOffers",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingDriverOffers_DriverId_OfferStatus",
                table: "BookingDriverOffers",
                columns: new[] { "DriverId", "OfferStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingDriverOffers");
        }
    }
}
