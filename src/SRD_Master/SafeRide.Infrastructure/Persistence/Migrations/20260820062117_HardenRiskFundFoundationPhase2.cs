using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HardenRiskFundFoundationPhase2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            DropAdministrativeAuditConstraintIfExists(migrationBuilder);

            migrationBuilder.AddCheckConstraint(
                name: "CK_RiskFundTransactions_AdministrativeAudit",
                table: "RiskFundTransactions",
                sql: "[TransactionType] NOT IN ('OPENING_BALANCE','ADJUSTMENT') OR ([PerformedByUserId] IS NOT NULL AND [PerformedByUserId] <> '00000000-0000-0000-0000-000000000000' AND [ExternalReference] IS NOT NULL AND LTRIM(RTRIM([ExternalReference])) <> '' AND [EvidenceUrl] IS NOT NULL AND LTRIM(RTRIM([EvidenceUrl])) <> '' AND LTRIM(RTRIM([Reason])) <> '' AND LTRIM(RTRIM([IdempotencyKey])) <> '')");

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_RiskFundTransactions_AppendOnly]
                ON [dbo].[RiskFundTransactions]
                INSTEAD OF UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51000, 'Risk Fund ledger transactions are append-only.', 1;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [dbo].[TR_RiskProtectionPolicyVersions_ImmutableWhenReferenced]
                ON [dbo].[RiskProtectionPolicyVersions]
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    IF EXISTS (
                        SELECT 1
                        FROM deleted AS d
                        WHERE EXISTS (
                            SELECT 1 FROM [dbo].[TripProtectionCoverages] AS c
                            WHERE c.[PolicyVersionId] = d.[Id])
                           OR EXISTS (
                            SELECT 1 FROM [dbo].[TripFinancialSettlements] AS s
                            WHERE s.[PolicyVersionId] = d.[Id])
                    )
                    BEGIN
                        THROW 51001, 'Referenced Risk Protection policy versions are immutable.', 1;
                    END;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS [dbo].[TR_RiskProtectionPolicyVersions_ImmutableWhenReferenced];");
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS [dbo].[TR_RiskFundTransactions_AppendOnly];");

            DropAdministrativeAuditConstraintIfExists(migrationBuilder);

            migrationBuilder.AddCheckConstraint(
                name: "CK_RiskFundTransactions_AdministrativeAudit",
                table: "RiskFundTransactions",
                sql: "[TransactionType] NOT IN ('OPENING_BALANCE','ADJUSTMENT') OR ([PerformedByUserId] IS NOT NULL AND [ExternalReference] IS NOT NULL AND [ExternalReference] <> '' AND [EvidenceUrl] IS NOT NULL AND [EvidenceUrl] <> '')");
        }

        private static void DropAdministrativeAuditConstraintIfExists(
            MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [sys].[check_constraints]
                    WHERE [name] = N'CK_RiskFundTransactions_AdministrativeAudit'
                      AND [parent_object_id] = OBJECT_ID(N'[dbo].[RiskFundTransactions]'))
                BEGIN
                    ALTER TABLE [dbo].[RiskFundTransactions]
                    DROP CONSTRAINT [CK_RiskFundTransactions_AdministrativeAudit];
                END;
                """);
        }
    }
}
