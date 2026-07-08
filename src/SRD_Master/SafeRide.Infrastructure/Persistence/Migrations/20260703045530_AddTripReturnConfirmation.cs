using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTripReturnConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TripReturnConfirmations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TripId = table.Column<long>(type: "bigint", nullable: false),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConfirmedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HandoverStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())"),
                    DriverLatitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    DriverLongitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripReturnConfirmations", x => x.Id);
                    table.CheckConstraint("CK_TripReturnConfirmations_DriverLatitude", "[DriverLatitude] IS NULL OR ([DriverLatitude] >= -90 AND [DriverLatitude] <= 90)");
                    table.CheckConstraint("CK_TripReturnConfirmations_DriverLongitude", "[DriverLongitude] IS NULL OR ([DriverLongitude] >= -180 AND [DriverLongitude] <= 180)");
                    table.CheckConstraint("CK_TripReturnConfirmations_HandoverStatus", "[HandoverStatus] IN ('Pending', 'CustomerConfirmed', 'DriverConfirmed', 'Disputed', 'Resolved')");
                    table.ForeignKey(
                        name: "FK_TripReturnConfirmations_AspNetUsers",
                        column: x => x.ConfirmedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TripReturnConfirmations_DriverProfiles",
                        column: x => x.DriverId,
                        principalTable: "DriverProfiles",
                        principalColumn: "DriverId");
                    table.ForeignKey(
                        name: "FK_TripReturnConfirmations_Trips",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TripReturnEvidence",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TripReturnConfirmationId = table.Column<long>(type: "bigint", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ImagePublicId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ContentType = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripReturnEvidence", x => x.Id);
                    table.CheckConstraint("CK_TripReturnEvidence_DisplayOrder", "[DisplayOrder] BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_TripReturnEvidence_FileSizeBytes", "[FileSizeBytes] IS NULL OR [FileSizeBytes] > 0");
                    table.ForeignKey(
                        name: "FK_TripReturnEvidence_TripReturnConfirmations",
                        column: x => x.TripReturnConfirmationId,
                        principalTable: "TripReturnConfirmations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TripReturnConfirmations_ConfirmedByUserId",
                table: "TripReturnConfirmations",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TripReturnConfirmations_DriverId",
                table: "TripReturnConfirmations",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_TripReturnConfirmations_TripId",
                table: "TripReturnConfirmations",
                column: "TripId");

            migrationBuilder.CreateIndex(
                name: "IX_TripReturnConfirmations_TripId_HandoverStatus",
                table: "TripReturnConfirmations",
                columns: new[] { "TripId", "HandoverStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_TripReturnEvidence_TripReturnConfirmationId",
                table: "TripReturnEvidence",
                column: "TripReturnConfirmationId");

            migrationBuilder.CreateIndex(
                name: "IX_TripReturnEvidence_TripReturnConfirmationId_DisplayOrder",
                table: "TripReturnEvidence",
                columns: new[] { "TripReturnConfirmationId", "DisplayOrder" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TripReturnEvidence");

            migrationBuilder.DropTable(
                name: "TripReturnConfirmations");
        }
    }
}
