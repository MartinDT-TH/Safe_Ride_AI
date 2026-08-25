using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenFinancialConcurrencyPhase61 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT [DriverId]
                    FROM [Trips]
                    WHERE [TripStatus] <> 'COMPLETED' AND [TripStatus] <> 'CANCELLED'
                    GROUP BY [DriverId]
                    HAVING COUNT_BIG(*) > 1)
                    THROW 51000, 'Phase 6.1 preflight failed: duplicate active trips exist for a driver.', 1;

                IF EXISTS (
                    SELECT [BookingId]
                    FROM [BookingPromotions]
                    GROUP BY [BookingId]
                    HAVING COUNT_BIG(*) > 1)
                    THROW 51000, 'Phase 6.1 preflight failed: a booking has multiple promotions.', 1;

                IF EXISTS (
                    SELECT [TripId]
                    FROM [Payments]
                    WHERE [PaymentMethod] = 'QR' AND [PaymentStatus] = 'Pending'
                    GROUP BY [TripId]
                    HAVING COUNT_BIG(*) > 1)
                    THROW 51000, 'Phase 6.1 preflight failed: duplicate pending QR intents exist.', 1;

                IF EXISTS (
                    SELECT [TransactionReference]
                    FROM [Payments]
                    WHERE [TransactionReference] IS NOT NULL
                    GROUP BY [TransactionReference]
                    HAVING COUNT_BIG(*) > 1)
                    THROW 51000, 'Phase 6.1 preflight failed: duplicate payment references exist.', 1;

                IF EXISTS (
                    SELECT 1
                    FROM [SafetyPaymentReconciliations]
                    WHERE [SuccessfulPaymentAmount] + [RemainingPayableAmount]
                        <> [CustomerPayableAmount] + [RefundObligationAmount])
                    THROW 51000, 'Phase 6.1 preflight failed: payment reconciliation identity is invalid.', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Trips_DriverId",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_Payments_TripId",
                table: "Payments");

            migrationBuilder.CreateTable(
                name: "TripEndReconciliationRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TripId = table.Column<long>(type: "bigint", nullable: false),
                    RequestedReason = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RequestedByDriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ResolvedByStaffId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TripEndReconciliationRequests", x => x.Id);
                    table.CheckConstraint("CK_TripEndReconciliations_Reason", "[RequestedReason] IN ('DRIVER_UNABLE_TO_CONTINUE','STARTED_BY_MISTAKE')");
                    table.CheckConstraint("CK_TripEndReconciliations_Status", "[Status] IN ('PENDING','APPROVED','REJECTED')");
                    table.ForeignKey(
                        name: "FK_TripEndReconciliationRequests_AspNetUsers_ResolvedByStaffId",
                        column: x => x.ResolvedByStaffId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TripEndReconciliationRequests_DriverProfiles_RequestedByDriverId",
                        column: x => x.RequestedByDriverId,
                        principalTable: "DriverProfiles",
                        principalColumn: "DriverId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TripEndReconciliationRequests_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_Trips_Driver_Active",
                table: "Trips",
                column: "DriverId",
                unique: true,
                filter: "[TripStatus] <> 'COMPLETED' AND [TripStatus] <> 'CANCELLED'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SafetyPaymentReconciliations_Identity",
                table: "SafetyPaymentReconciliations",
                sql: "[SuccessfulPaymentAmount] + [RemainingPayableAmount] = [CustomerPayableAmount] + [RefundObligationAmount]");

            migrationBuilder.CreateIndex(
                name: "UX_Payments_TransactionReference",
                table: "Payments",
                column: "TransactionReference",
                unique: true,
                filter: "[TransactionReference] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_Payments_Trip_PendingQr",
                table: "Payments",
                column: "TripId",
                unique: true,
                filter: "[PaymentMethod] = 'QR' AND [PaymentStatus] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "UX_BookingPromotions_BookingId",
                table: "BookingPromotions",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TripEndReconciliationRequests_RequestedByDriverId",
                table: "TripEndReconciliationRequests",
                column: "RequestedByDriverId");

            migrationBuilder.CreateIndex(
                name: "IX_TripEndReconciliationRequests_ResolvedByStaffId",
                table: "TripEndReconciliationRequests",
                column: "ResolvedByStaffId");

            migrationBuilder.CreateIndex(
                name: "UX_TripEndReconciliations_Trip_Pending",
                table: "TripEndReconciliationRequests",
                column: "TripId",
                unique: true,
                filter: "[Status] = 'PENDING'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TripEndReconciliationRequests");

            migrationBuilder.DropIndex(
                name: "UX_Trips_Driver_Active",
                table: "Trips");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SafetyPaymentReconciliations_Identity",
                table: "SafetyPaymentReconciliations");

            migrationBuilder.DropIndex(
                name: "UX_Payments_TransactionReference",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "UX_Payments_Trip_PendingQr",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "UX_BookingPromotions_BookingId",
                table: "BookingPromotions");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_DriverId",
                table: "Trips",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TripId",
                table: "Payments",
                column: "TripId");
        }
    }
}
