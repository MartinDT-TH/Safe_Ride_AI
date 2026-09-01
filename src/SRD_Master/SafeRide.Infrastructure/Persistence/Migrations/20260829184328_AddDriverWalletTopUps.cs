using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverWalletTopUps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_WalletTransactions_TransactionType",
                table: "WalletTransactions");

            migrationBuilder.CreateTable(
                name: "DriverWalletTopUps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WalletId = table.Column<long>(type: "bigint", nullable: false),
                    OrderCode = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaymentLinkId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProviderReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverWalletTopUps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DriverWalletTopUps_DriverWallets_WalletId",
                        column: x => x.WalletId,
                        principalTable: "DriverWallets",
                        principalColumn: "Id");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_WalletTransactions_TransactionType",
                table: "WalletTransactions",
                sql: "[TransactionType] IN ('Income', 'Withdrawal', 'Penalty', 'Bonus', 'TopUp')");

            migrationBuilder.CreateIndex(
                name: "IX_DriverWalletTopUps_OrderCode",
                table: "DriverWalletTopUps",
                column: "OrderCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DriverWalletTopUps_WalletId",
                table: "DriverWalletTopUps",
                column: "WalletId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DriverWalletTopUps");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WalletTransactions_TransactionType",
                table: "WalletTransactions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WalletTransactions_TransactionType",
                table: "WalletTransactions",
                sql: "[TransactionType] IN ('Income', 'Withdrawal', 'Penalty', 'Bonus')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DriverKyc_DrivingLicense",
                table: "DriverKyc",
                sql: "[DocumentType] <> 'DRIVING_LICENSE' OR ([DocumentNumber] IS NOT NULL AND [LicenseClass] IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DriverKyc_LicenseClass",
                table: "DriverKyc",
                sql: "[LicenseClass] IS NULL OR [LicenseClass] IN ('Old_A1', 'Old_A2', 'Old_B1', 'Old_B2', 'A1', 'A', 'B')");
        }
    }
}
