using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteAccidentDisputeEvidenceSecurityPhase5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccidentEvidence_AccidentReportId",
                table: "AccidentEvidence");

            migrationBuilder.AddColumn<int>(
                name: "SequenceNumber",
                table: "AccidentEvidence",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                IF EXISTS (
                    SELECT 1
                    FROM [AccidentEvidence]
                    GROUP BY [AccidentReportId]
                    HAVING COUNT_BIG(*) > 20)
                    THROW 51000, 'Cannot enforce accident evidence cap: an accident already has more than 20 evidence records.', 1;

                WITH [NumberedEvidence] AS (
                    SELECT [Id], ROW_NUMBER() OVER (
                        PARTITION BY [AccidentReportId]
                        ORDER BY [CreatedAtUtc], [Id]) AS [SequenceNumber]
                    FROM [AccidentEvidence])
                UPDATE [e]
                SET [e].[SequenceNumber] = [n].[SequenceNumber]
                FROM [AccidentEvidence] AS [e]
                INNER JOIN [NumberedEvidence] AS [n] ON [n].[Id] = [e].[Id];
                """);

            migrationBuilder.AlterColumn<int>(
                name: "SequenceNumber",
                table: "AccidentEvidence",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "LiabilityDisputeAudits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssessmentId = table.Column<long>(type: "bigint", nullable: false),
                    DisputedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisputedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiabilityDisputeAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiabilityDisputeAudits_AccidentLiabilityAssessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "AccidentLiabilityAssessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LiabilityDisputeEvidence",
                columns: table => new
                {
                    LiabilityDisputeAuditId = table.Column<long>(type: "bigint", nullable: false),
                    AccidentEvidenceId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiabilityDisputeEvidence", x => new { x.LiabilityDisputeAuditId, x.AccidentEvidenceId });
                    table.ForeignKey(
                        name: "FK_LiabilityDisputeEvidence_AccidentEvidence_AccidentEvidenceId",
                        column: x => x.AccidentEvidenceId,
                        principalTable: "AccidentEvidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LiabilityDisputeEvidence_LiabilityDisputeAudits_LiabilityDisputeAuditId",
                        column: x => x.LiabilityDisputeAuditId,
                        principalTable: "LiabilityDisputeAudits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccidentEvidence_AccidentReportId_SequenceNumber",
                table: "AccidentEvidence",
                columns: new[] { "AccidentReportId", "SequenceNumber" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccidentEvidence_SequenceNumber",
                table: "AccidentEvidence",
                sql: "[SequenceNumber] BETWEEN 1 AND 20");

            migrationBuilder.CreateIndex(
                name: "IX_LiabilityDisputeAudits_AssessmentId_DisputedAtUtc",
                table: "LiabilityDisputeAudits",
                columns: new[] { "AssessmentId", "DisputedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LiabilityDisputeEvidence_AccidentEvidenceId",
                table: "LiabilityDisputeEvidence",
                column: "AccidentEvidenceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LiabilityDisputeEvidence");

            migrationBuilder.DropTable(
                name: "LiabilityDisputeAudits");

            migrationBuilder.DropIndex(
                name: "IX_AccidentEvidence_AccidentReportId_SequenceNumber",
                table: "AccidentEvidence");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccidentEvidence_SequenceNumber",
                table: "AccidentEvidence");

            migrationBuilder.DropColumn(
                name: "SequenceNumber",
                table: "AccidentEvidence");

            migrationBuilder.CreateIndex(
                name: "IX_AccidentEvidence_AccidentReportId",
                table: "AccidentEvidence",
                column: "AccidentReportId");
        }
    }
}
