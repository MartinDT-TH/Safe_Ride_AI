using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimFundingRecoveryPhase7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_ClaimRecoveries_Amount",
                table: "ClaimRecoveries",
                sql: "[Amount] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ClaimRecoveries_Audit",
                table: "ClaimRecoveries",
                sql: "[PaymentReference] <> '' AND [EvidenceUrl] <> '' AND [RecordedByUserId] <> '00000000-0000-0000-0000-000000000000' AND [IdempotencyKey] <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ClaimRecoveries_Amount",
                table: "ClaimRecoveries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ClaimRecoveries_Audit",
                table: "ClaimRecoveries");
        }
    }
}
