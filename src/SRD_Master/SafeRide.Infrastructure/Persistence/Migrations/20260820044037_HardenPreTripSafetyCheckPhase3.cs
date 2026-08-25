using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenPreTripSafetyCheckPhase3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EvidenceContentType",
                table: "PreTripVehicleChecks",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "EvidenceFileSizeBytes",
                table: "PreTripVehicleChecks",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceOriginalFileName",
                table: "PreTripVehicleChecks",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceStoragePublicId",
                table: "PreTripVehicleChecks",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PreTripVehicleChecks_EvidenceFileSize",
                table: "PreTripVehicleChecks",
                sql: "[EvidenceFileSizeBytes] IS NULL OR ([EvidenceFileSizeBytes] > 0 AND [EvidenceFileSizeBytes] <= 10000000)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PreTripVehicleChecks_EvidenceFileSize",
                table: "PreTripVehicleChecks");

            migrationBuilder.DropColumn(
                name: "EvidenceContentType",
                table: "PreTripVehicleChecks");

            migrationBuilder.DropColumn(
                name: "EvidenceFileSizeBytes",
                table: "PreTripVehicleChecks");

            migrationBuilder.DropColumn(
                name: "EvidenceOriginalFileName",
                table: "PreTripVehicleChecks");

            migrationBuilder.DropColumn(
                name: "EvidenceStoragePublicId",
                table: "PreTripVehicleChecks");
        }
    }
}
