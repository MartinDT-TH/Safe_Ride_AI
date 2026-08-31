using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDrivingLicenseKycConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_DriverKyc_DrivingLicense",
                table: "DriverKyc",
                sql: "[DocumentType] <> 'DRIVING_LICENSE' OR ([DocumentNumber] IS NOT NULL AND [LicenseClass] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_DriverKyc_DrivingLicense",
                table: "DriverKyc");
        }
    }
}
