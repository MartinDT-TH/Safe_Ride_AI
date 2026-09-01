using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyCustomerVehicleInsurancePhaseA4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InsurancePolicyDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_TripProtectionCoverages_VehicleInsurancePolicies_VehicleInsurancePolicyId",
                table: "TripProtectionCoverages");

            migrationBuilder.DropIndex(
                name: "IX_TripProtectionCoverages_VehicleInsurancePolicyId",
                table: "TripProtectionCoverages");

            migrationBuilder.DropColumn(
                name: "InsuranceCoverageSnapshot",
                table: "TripProtectionCoverages");

            migrationBuilder.DropColumn(
                name: "InsuranceDeductibleSnapshot",
                table: "TripProtectionCoverages");

            migrationBuilder.DropColumn(
                name: "InsuranceProviderSnapshot",
                table: "TripProtectionCoverages");

            migrationBuilder.DropColumn(
                name: "PolicyNumberSnapshot",
                table: "TripProtectionCoverages");

            migrationBuilder.DropColumn(
                name: "VehicleInsurancePolicyId",
                table: "TripProtectionCoverages");

            migrationBuilder.DropTable(
                name: "VehicleInsurancePolicies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "InsuranceCoverageSnapshot",
                table: "TripProtectionCoverages",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InsuranceDeductibleSnapshot",
                table: "TripProtectionCoverages",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InsuranceProviderSnapshot",
                table: "TripProtectionCoverages",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyNumberSnapshot",
                table: "TripProtectionCoverages",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "VehicleInsurancePolicyId",
                table: "TripProtectionCoverages",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VehicleInsurancePolicies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VehicleId = table.Column<long>(type: "bigint", nullable: false),
                    InsuranceType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PolicyNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CoverageAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Deductible = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DocumentUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    VerificationStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleInsurancePolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleInsurancePolicies_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InsurancePolicyDocuments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VehicleInsurancePolicyId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    StorageObjectKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InsurancePolicyDocuments", x => x.Id);
                    table.CheckConstraint(
                        "CK_InsurancePolicyDocuments_FileSize",
                        "[FileSizeBytes] > 0 AND [FileSizeBytes] <= 10000000");
                    table.ForeignKey(
                        name: "FK_InsurancePolicyDocuments_VehicleInsurancePolicies_VehicleInsurancePolicyId",
                        column: x => x.VehicleInsurancePolicyId,
                        principalTable: "VehicleInsurancePolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TripProtectionCoverages_VehicleInsurancePolicyId",
                table: "TripProtectionCoverages",
                column: "VehicleInsurancePolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleInsurancePolicies_Provider_PolicyNumber",
                table: "VehicleInsurancePolicies",
                columns: new[] { "Provider", "PolicyNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleInsurancePolicies_VehicleId",
                table: "VehicleInsurancePolicies",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_InsurancePolicyDocuments_VehicleInsurancePolicyId_UploadedAtUtc",
                table: "InsurancePolicyDocuments",
                columns: new[] { "VehicleInsurancePolicyId", "UploadedAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_TripProtectionCoverages_VehicleInsurancePolicies_VehicleInsurancePolicyId",
                table: "TripProtectionCoverages",
                column: "VehicleInsurancePolicyId",
                principalTable: "VehicleInsurancePolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
