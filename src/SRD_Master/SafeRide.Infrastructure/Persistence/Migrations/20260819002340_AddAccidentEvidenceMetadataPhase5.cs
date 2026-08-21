using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccidentEvidenceMetadataPhase5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PreTripVehicleChecks_TripId_CheckedAtUtc",
                table: "PreTripVehicleChecks");

            migrationBuilder.AddColumn<DateTime>(
                name: "CapturedAtUtc",
                table: "AccidentEvidence",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                table: "AccidentEvidence",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "AccidentEvidence",
                type: "decimal(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "AccidentEvidence",
                type: "decimal(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "AccidentEvidence",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StoragePublicId",
                table: "AccidentEvidence",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreTripVehicleChecks_TripId_CheckedAtUtc",
                table: "PreTripVehicleChecks",
                columns: new[] { "TripId", "CheckedAtUtc" },
                descending: new[] { false, true });

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccidentEvidence_FileSize",
                table: "AccidentEvidence",
                sql: "[FileSizeBytes] IS NULL OR ([FileSizeBytes] > 0 AND [FileSizeBytes] <= 10000000)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PreTripVehicleChecks_TripId_CheckedAtUtc",
                table: "PreTripVehicleChecks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccidentEvidence_FileSize",
                table: "AccidentEvidence");

            migrationBuilder.DropColumn(
                name: "CapturedAtUtc",
                table: "AccidentEvidence");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                table: "AccidentEvidence");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "AccidentEvidence");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "AccidentEvidence");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "AccidentEvidence");

            migrationBuilder.DropColumn(
                name: "StoragePublicId",
                table: "AccidentEvidence");

            migrationBuilder.CreateIndex(
                name: "IX_PreTripVehicleChecks_TripId_CheckedAtUtc",
                table: "PreTripVehicleChecks",
                columns: new[] { "TripId", "CheckedAtUtc" });
        }
    }
}
