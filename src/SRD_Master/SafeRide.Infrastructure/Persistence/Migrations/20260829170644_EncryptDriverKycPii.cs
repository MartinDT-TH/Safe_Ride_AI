using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EncryptDriverKycPii : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // These constraints compare the plaintext enum value and therefore
            // cannot remain once LicenseClass is encrypted.
            // Deployed databases may not contain these legacy checks. Catalog-guard
            // each drop so the migration is safe across schema versions.
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DriverKyc_DrivingLicense' AND parent_object_id = OBJECT_ID(N'[DriverKyc]')) EXEC(N'ALTER TABLE [DriverKyc] DROP CONSTRAINT [CK_DriverKyc_DrivingLicense]')");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DriverKyc_LicenseClass' AND parent_object_id = OBJECT_ID(N'[DriverKyc]')) EXEC(N'ALTER TABLE [DriverKyc] DROP CONSTRAINT [CK_DriverKyc_LicenseClass]')");
            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_DriverKyc_DocumentFile' AND parent_object_id = OBJECT_ID(N'[DriverKyc]')) EXEC(N'ALTER TABLE [DriverKyc] DROP CONSTRAINT [CK_DriverKyc_DocumentFile]')");
            migrationBuilder.AlterColumn<string>(
                name: "LicenseClass",
                table: "DriverKyc",
                type: "nvarchar(max)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IssueDate",
                table: "DriverKyc",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Gender",
                table: "DriverKyc",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(10)",
                oldUnicode: false,
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "DriverKyc",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FrontImageUrl",
                table: "DriverKyc",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FileUrl",
                table: "DriverKyc",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ExpiryDate",
                table: "DriverKyc",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DocumentNumber",
                table: "DriverKyc",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldUnicode: false,
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DateOfBirth",
                table: "DriverKyc",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BackImageUrl",
                table: "DriverKyc",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "DriverKyc",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "LicenseClass",
                table: "DriverKyc",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "IssueDate",
                table: "DriverKyc",
                type: "date",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Gender",
                table: "DriverKyc",
                type: "varchar(10)",
                unicode: false,
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "DriverKyc",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FrontImageUrl",
                table: "DriverKyc",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FileUrl",
                table: "DriverKyc",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "ExpiryDate",
                table: "DriverKyc",
                type: "date",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DocumentNumber",
                table: "DriverKyc",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "DateOfBirth",
                table: "DriverKyc",
                type: "date",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BackImageUrl",
                table: "DriverKyc",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "DriverKyc",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
