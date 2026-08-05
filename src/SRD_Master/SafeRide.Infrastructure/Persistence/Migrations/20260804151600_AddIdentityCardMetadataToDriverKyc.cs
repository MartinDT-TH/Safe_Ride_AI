using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityCardMetadataToDriverKyc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "DriverKyc",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "DriverKyc",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "DriverKyc",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "DriverKyc",
                type: "varchar(10)",
                unicode: false,
                maxLength: 10,
                nullable: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "DriverKyc");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "DriverKyc");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "DriverKyc");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "DriverKyc");
        }
    }
}
