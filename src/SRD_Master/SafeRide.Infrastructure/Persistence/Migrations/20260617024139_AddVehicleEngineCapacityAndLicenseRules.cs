using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleEngineCapacityAndLicenseRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Vehicles_RequiredLicenseClass",
                table: "Vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PricingRules_VehicleClass",
                table: "PricingRules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DriverKyc_LicenseClass",
                table: "DriverKyc");

            migrationBuilder.AddColumn<int>(
                name: "EngineCapacityCc",
                table: "Vehicles",
                type: "int",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicles_EngineCapacityCc",
                table: "Vehicles",
                sql: "[EngineCapacityCc] IS NULL OR [EngineCapacityCc] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicles_RequiredLicenseClass",
                table: "Vehicles",
                sql: "[RequiredLicenseClass] IN ('A1', 'A', 'B')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PricingRules_VehicleClass",
                table: "PricingRules",
                sql: "[VehicleClass] IN ('A1', 'A', 'B')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DriverKyc_LicenseClass",
                table: "DriverKyc",
                sql: "[LicenseClass] IS NULL OR [LicenseClass] IN ('Old_A1', 'Old_A2', 'Old_B1', 'Old_B2', 'A1', 'A', 'B')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Vehicles_EngineCapacityCc",
                table: "Vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Vehicles_RequiredLicenseClass",
                table: "Vehicles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PricingRules_VehicleClass",
                table: "PricingRules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DriverKyc_LicenseClass",
                table: "DriverKyc");

            migrationBuilder.DropColumn(
                name: "EngineCapacityCc",
                table: "Vehicles");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Vehicles_RequiredLicenseClass",
                table: "Vehicles",
                sql: "[RequiredLicenseClass] IN ('A1', 'A', 'B', 'C1', 'C', 'D1', 'D2', 'D')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PricingRules_VehicleClass",
                table: "PricingRules",
                sql: "[VehicleClass] IN ('A1', 'A', 'B', 'C1', 'C', 'D1', 'D2', 'D')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DriverKyc_LicenseClass",
                table: "DriverKyc",
                sql: "[LicenseClass] IS NULL OR [LicenseClass] IN ('A1', 'A', 'B1', 'B', 'C1', 'C', 'D1', 'D2', 'D', 'Old_B1', 'Old_B2', 'Old_A1', 'Old_A2')");
        }
    }
}
