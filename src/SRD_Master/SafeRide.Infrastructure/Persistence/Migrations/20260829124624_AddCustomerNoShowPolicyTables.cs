using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerNoShowPolicyTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerBehaviorEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookingId = table.Column<long>(type: "bigint", nullable: false),
                    TripId = table.Column<long>(type: "bigint", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverReportedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArrivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArrivalLatitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    ArrivalLongitude = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    ArrivalDistanceMeters = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    ReminderSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WaitSatisfiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExemptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerBehaviorEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerBehaviorEvents_AspNetUsers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerBehaviorEvents_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerBehaviorEvents_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerBehaviorEvents_DriverProfiles_DriverId",
                        column: x => x.DriverId,
                        principalTable: "DriverProfiles",
                        principalColumn: "DriverId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomerBehaviorEvents_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomerBookingPrivileges",
                columns: table => new
                {
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduledBookingAllowed = table.Column<bool>(type: "bit", nullable: false),
                    ScheduledRestrictedUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InstantBookingAllowed = table.Column<bool>(type: "bit", nullable: false),
                    BookingCooldownUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedNoShowCount = table.Column<int>(type: "int", nullable: false),
                    EligibleBookingCount = table.Column<int>(type: "int", nullable: false),
                    NoShowRate = table.Column<decimal>(type: "decimal(9,6)", nullable: false),
                    ConsecutiveNoShowStreak = table.Column<int>(type: "int", nullable: false),
                    LastNoShowAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RestrictionLevel = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UnderStaffReview = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerBookingPrivileges", x => x.CustomerId);
                    table.ForeignKey(
                        name: "FK_CustomerBookingPrivileges_AspNetUsers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DriverNoShowSupports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TripId = table.Column<long>(type: "bigint", nullable: false),
                    BookingId = table.Column<long>(type: "bigint", nullable: false),
                    DriverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerBehaviorEventId = table.Column<long>(type: "bigint", nullable: false),
                    AcceptedPickupDistanceKm = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    SupportAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WalletTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())"),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverNoShowSupports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DriverNoShowSupports_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DriverNoShowSupports_CustomerBehaviorEvents_CustomerBehaviorEventId",
                        column: x => x.CustomerBehaviorEventId,
                        principalTable: "CustomerBehaviorEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DriverNoShowSupports_DriverProfiles_DriverId",
                        column: x => x.DriverId,
                        principalTable: "DriverProfiles",
                        principalColumn: "DriverId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DriverNoShowSupports_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DriverNoShowSupports_WalletTransactions_WalletTransactionId",
                        column: x => x.WalletTransactionId,
                        principalTable: "WalletTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBehaviorEvents_BookingId",
                table: "CustomerBehaviorEvents",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBehaviorEvents_CustomerId",
                table: "CustomerBehaviorEvents",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBehaviorEvents_DriverId",
                table: "CustomerBehaviorEvents",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBehaviorEvents_ReviewedByUserId",
                table: "CustomerBehaviorEvents",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBehaviorEvents_TripId_EventType",
                table: "CustomerBehaviorEvents",
                columns: new[] { "TripId", "EventType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DriverNoShowSupports_BookingId",
                table: "DriverNoShowSupports",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverNoShowSupports_CustomerBehaviorEventId",
                table: "DriverNoShowSupports",
                column: "CustomerBehaviorEventId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverNoShowSupports_DriverId",
                table: "DriverNoShowSupports",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_DriverNoShowSupports_TripId",
                table: "DriverNoShowSupports",
                column: "TripId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DriverNoShowSupports_WalletTransactionId",
                table: "DriverNoShowSupports",
                column: "WalletTransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerBookingPrivileges");

            migrationBuilder.DropTable(
                name: "DriverNoShowSupports");

            migrationBuilder.DropTable(
                name: "CustomerBehaviorEvents");
        }
    }
}
