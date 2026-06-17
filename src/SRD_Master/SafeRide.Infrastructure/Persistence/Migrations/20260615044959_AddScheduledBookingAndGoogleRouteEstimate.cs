using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledBookingAndGoogleRouteEstimate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Bookings_BookingStatus",
                table: "Bookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Bookings_EstimatedFare",
                table: "Bookings");

            migrationBuilder.Sql(
                """
                UPDATE [Bookings]
                SET
                    [BookingStatus] = CASE [BookingStatus]
                        WHEN 'SEARCHING_DRIVER' THEN 'Searching'
                        WHEN 'DRIVER_ASSIGNED' THEN 'DriverAssigned'
                        WHEN 'CUSTOMER_CANCELLED' THEN 'Cancelled'
                        WHEN 'DRIVER_CANCELLED' THEN 'Cancelled'
                        WHEN 'EXPIRED' THEN 'Expired'
                        WHEN 'CONVERTED_TO_TRIP' THEN 'Completed'
                        WHEN 'PENDING_PAYMENT' THEN 'Completed'
                        ELSE [BookingStatus]
                    END,
                    [UpdatedAt] = COALESCE([UpdatedAt], [CreatedAt], SYSUTCDATETIME()),
                    [EstimatedFare] = COALESCE([EstimatedFare], 0);
                """);

            DropLegacyBookingIndexes(migrationBuilder);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Bookings",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "EstimatedFare",
                table: "Bookings",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BookingType",
                table: "Bookings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValueSql: "('Now')");

            migrationBuilder.AlterColumn<string>(
                name: "BookingStatus",
                table: "Bookings",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldDefaultValueSql: "('SEARCHING_DRIVER')");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_BookingStatus",
                table: "Bookings",
                column: "BookingStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_BookingType",
                table: "Bookings",
                column: "BookingType");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ScheduledAt",
                table: "Bookings",
                column: "ScheduledAt");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bookings_BookingStatus",
                table: "Bookings",
                sql: "[BookingStatus] IN ('PendingSchedule', 'Searching', 'DriverAssigned', 'Cancelled', 'Expired', 'Completed')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bookings_EstimatedFare",
                table: "Bookings",
                sql: "[EstimatedFare] >= 0");

            CreateLegacyBookingIndexes(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_BookingStatus",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_BookingType",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_ScheduledAt",
                table: "Bookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Bookings_BookingStatus",
                table: "Bookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Bookings_EstimatedFare",
                table: "Bookings");

            migrationBuilder.Sql(
                """
                UPDATE [Bookings]
                SET [BookingStatus] = CASE [BookingStatus]
                    WHEN 'PendingSchedule' THEN 'SEARCHING_DRIVER'
                    WHEN 'Searching' THEN 'SEARCHING_DRIVER'
                    WHEN 'DriverAssigned' THEN 'DRIVER_ASSIGNED'
                    WHEN 'Cancelled' THEN 'CUSTOMER_CANCELLED'
                    WHEN 'Expired' THEN 'EXPIRED'
                    WHEN 'Completed' THEN 'CONVERTED_TO_TRIP'
                    ELSE [BookingStatus]
                END;
                """);

            DropLegacyBookingIndexes(migrationBuilder);

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAt",
                table: "Bookings",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<decimal>(
                name: "EstimatedFare",
                table: "Bookings",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "BookingType",
                table: "Bookings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValueSql: "('Now')",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "BookingStatus",
                table: "Bookings",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValueSql: "('SEARCHING_DRIVER')",
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bookings_BookingStatus",
                table: "Bookings",
                sql: "[BookingStatus] IN ('SEARCHING_DRIVER', 'DRIVER_ASSIGNED', 'CUSTOMER_CANCELLED', 'DRIVER_CANCELLED', 'EXPIRED', 'CONVERTED_TO_TRIP')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bookings_EstimatedFare",
                table: "Bookings",
                sql: "[EstimatedFare] IS NULL OR [EstimatedFare] >= 0");

            CreateLegacyBookingIndexes(migrationBuilder);
        }

        private static void DropLegacyBookingIndexes(
            MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS [IX_Bookings_BookingStatus_CreatedAt]
                    ON [Bookings];
                DROP INDEX IF EXISTS [IX_Bookings_CustomerId_BookingStatus_CreatedAt]
                    ON [Bookings];
                """);
        }

        private static void CreateLegacyBookingIndexes(
            MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE INDEX [IX_Bookings_BookingStatus_CreatedAt]
                    ON [Bookings] ([BookingStatus], [CreatedAt])
                    INCLUDE ([Id], [CustomerId], [VehicleId], [ServiceTypeId],
                        [PickupAddress], [EstimatedFare]);

                CREATE INDEX [IX_Bookings_CustomerId_BookingStatus_CreatedAt]
                    ON [Bookings] ([CustomerId], [BookingStatus], [CreatedAt])
                    INCLUDE ([Id], [VehicleId], [ServiceTypeId], [PickupAddress],
                        [DestinationAddress], [EstimatedFare]);
                """);
        }
    }
}
