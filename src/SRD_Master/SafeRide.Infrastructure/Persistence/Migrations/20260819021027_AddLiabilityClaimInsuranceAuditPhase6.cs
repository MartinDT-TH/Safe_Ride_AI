using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLiabilityClaimInsuranceAuditPhase6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ProtectionClaims_Amounts",
                table: "ProtectionClaims");

            migrationBuilder.AddColumn<decimal>(
                name: "InsuranceRequestedAmount",
                table: "ProtectionClaims",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "InsuranceClaimProviderAudits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProtectionClaimId = table.Column<long>(type: "bigint", nullable: false),
                    Operation = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ResultStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CoveredAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ProviderReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RequestPayload = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    ResponsePayload = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    PerformedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InsuranceClaimProviderAudits", x => x.Id);
                    table.CheckConstraint("CK_InsuranceClaimProviderAudits_Amounts", "[RequestedAmount] >= 0 AND [CoveredAmount] >= 0 AND [CoveredAmount] <= [RequestedAmount]");
                    table.ForeignKey(
                        name: "FK_InsuranceClaimProviderAudits_ProtectionClaims_ProtectionClaimId",
                        column: x => x.ProtectionClaimId,
                        principalTable: "ProtectionClaims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProtectionClaims_Amounts",
                table: "ProtectionClaims",
                sql: "[TotalDamageAmount] >= 0 AND [EligibleDamageAmount] >= 0 AND [EligibleDamageAmount] <= [TotalDamageAmount] AND [InsuranceRequestedAmount] >= 0 AND [InsuranceCoveredAmount] >= 0 AND [InsuranceCoveredAmount] <= [InsuranceRequestedAmount] AND [RiskFundAdvanceAmount] >= 0 AND [RiskFundPermanentLossAmount] >= 0 AND [DriverLiabilityAmount] >= 0 AND [CustomerLiabilityAmount] >= 0 AND [ThirdPartyLiabilityAmount] >= 0 AND [TotalPaidToClaimant] >= 0 AND [TotalPaidToClaimant] <= [EligibleDamageAmount] AND [RecoveredAmount] >= 0 AND [OutstandingRecoveryAmount] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_InsuranceClaimProviderAudits_ProtectionClaimId_CreatedAtUtc",
                table: "InsuranceClaimProviderAudits",
                columns: new[] { "ProtectionClaimId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InsuranceClaimProviderAudits");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProtectionClaims_Amounts",
                table: "ProtectionClaims");

            migrationBuilder.DropColumn(
                name: "InsuranceRequestedAmount",
                table: "ProtectionClaims");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProtectionClaims_Amounts",
                table: "ProtectionClaims",
                sql: "[TotalDamageAmount] >= 0 AND [EligibleDamageAmount] >= 0 AND [TotalPaidToClaimant] >= 0 AND [RecoveredAmount] >= 0 AND [OutstandingRecoveryAmount] >= 0");
        }
    }
}
