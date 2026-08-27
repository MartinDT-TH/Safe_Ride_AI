using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSafetyTerminationPaymentReconciliationPhase4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SafetyPaymentReconciliations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TripId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerPayableAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SuccessfulPaymentAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RemainingPayableAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RefundObligationAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DriverCreditedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyPaymentReconciliations", x => x.Id);
                    table.CheckConstraint("CK_SafetyPaymentReconciliations_Amounts", "[CustomerPayableAmount] >= 0 AND [SuccessfulPaymentAmount] >= 0 AND [RemainingPayableAmount] >= 0 AND [RefundObligationAmount] >= 0 AND [DriverCreditedAmount] >= 0 AND NOT ([RemainingPayableAmount] > 0 AND [RefundObligationAmount] > 0)");
                    table.CheckConstraint("CK_SafetyPaymentReconciliations_Status", "[Status] IN ('NOT_REQUIRED','PAYMENT_PENDING','PAID','REFUND_PENDING','REFUNDED')");
                    table.ForeignKey(
                        name: "FK_SafetyPaymentReconciliations_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SafetyTerminationEvidence",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TripId = table.Column<long>(type: "bigint", nullable: false),
                    EvidenceUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    StoragePublicId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SafetyTerminationEvidence", x => x.Id);
                    table.CheckConstraint("CK_SafetyTerminationEvidence_TrustedMetadata", "[EvidenceUrl] <> '' AND [StoragePublicId] <> '' AND [OriginalFileName] <> '' AND [ContentType] <> '' AND [FileSizeBytes] > 0 AND [UploadedByUserId] <> '00000000-0000-0000-0000-000000000000'");
                    table.ForeignKey(
                        name: "FK_SafetyTerminationEvidence_AspNetUsers_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SafetyTerminationEvidence_Trips_TripId",
                        column: x => x.TripId,
                        principalTable: "Trips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ManualPaymentRefunds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SafetyPaymentReconciliationId = table.Column<long>(type: "bigint", nullable: false),
                    PaymentId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaymentReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EvidenceUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RefundedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConfirmationIdempotencyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RefundedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualPaymentRefunds", x => x.Id);
                    table.CheckConstraint("CK_ManualPaymentRefunds_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_ManualPaymentRefunds_EvidenceOnRefund", "[Status] = 'REFUND_PENDING' OR ([PaymentReference] IS NOT NULL AND LTRIM(RTRIM([PaymentReference])) <> '' AND [EvidenceUrl] IS NOT NULL AND LTRIM(RTRIM([EvidenceUrl])) <> '' AND [RefundedByUserId] IS NOT NULL AND [RefundedByUserId] <> '00000000-0000-0000-0000-000000000000' AND [RefundedAtUtc] IS NOT NULL)");
                    table.CheckConstraint("CK_ManualPaymentRefunds_Status", "[Status] IN ('REFUND_PENDING','REFUNDED')");
                    table.ForeignKey(
                        name: "FK_ManualPaymentRefunds_AspNetUsers_RefundedByUserId",
                        column: x => x.RefundedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ManualPaymentRefunds_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ManualPaymentRefunds_SafetyPaymentReconciliations_SafetyPaymentReconciliationId",
                        column: x => x.SafetyPaymentReconciliationId,
                        principalTable: "SafetyPaymentReconciliations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ManualPaymentRefunds_ConfirmationIdempotencyKey",
                table: "ManualPaymentRefunds",
                column: "ConfirmationIdempotencyKey",
                unique: true,
                filter: "[ConfirmationIdempotencyKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ManualPaymentRefunds_PaymentId",
                table: "ManualPaymentRefunds",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_ManualPaymentRefunds_RefundedByUserId",
                table: "ManualPaymentRefunds",
                column: "RefundedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ManualPaymentRefunds_SafetyPaymentReconciliationId",
                table: "ManualPaymentRefunds",
                column: "SafetyPaymentReconciliationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SafetyPaymentReconciliations_TripId",
                table: "SafetyPaymentReconciliations",
                column: "TripId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SafetyTerminationEvidence_TripId_CreatedAtUtc",
                table: "SafetyTerminationEvidence",
                columns: new[] { "TripId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SafetyTerminationEvidence_UploadedByUserId",
                table: "SafetyTerminationEvidence",
                column: "UploadedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManualPaymentRefunds");

            migrationBuilder.DropTable(
                name: "SafetyTerminationEvidence");

            migrationBuilder.DropTable(
                name: "SafetyPaymentReconciliations");
        }
    }
}
