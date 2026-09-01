using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletSettlementEffectUniquenessPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SettlementEffect",
                table: "WalletTransactions",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_WalletTransactions_Trip_Wallet_SettlementEffect",
                table: "WalletTransactions",
                columns: new[] { "TripId", "WalletId" },
                unique: true,
                filter: "[TripId] IS NOT NULL AND [SettlementEffect] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_WalletTransactions_Trip_Wallet_SettlementEffect",
                table: "WalletTransactions");

            migrationBuilder.DropColumn(
                name: "SettlementEffect",
                table: "WalletTransactions");

        }
    }
}
