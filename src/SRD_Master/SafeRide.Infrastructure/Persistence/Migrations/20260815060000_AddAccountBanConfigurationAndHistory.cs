using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SafeRide.Infrastructure.Persistence;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260815060000_AddAccountBanConfigurationAndHistory")]
    public partial class AddAccountBanConfigurationAndHistory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountBanConfigurations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    NegativeFeedbackThreshold = table.Column<int>(type: "int", nullable: false),
                    NegativeRatingMaxScore = table.Column<int>(type: "int", nullable: false),
                    TemporaryBanDurationDays = table.Column<int>(type: "int", nullable: false),
                    MaximumTemporaryBans = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())"),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountBanConfigurations", x => x.Id);
                    table.CheckConstraint("CK_AccountBanConfigurations_MaximumTemporaryBans", "[MaximumTemporaryBans] > 0");
                    table.CheckConstraint("CK_AccountBanConfigurations_NegativeFeedbackThreshold", "[NegativeFeedbackThreshold] > 0");
                    table.CheckConstraint("CK_AccountBanConfigurations_NegativeRatingMaxScore", "[NegativeRatingMaxScore] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_AccountBanConfigurations_Singleton", "[Id] = 1");
                    table.CheckConstraint("CK_AccountBanConfigurations_TemporaryBanDurationDays", "[TemporaryBanDurationDays] > 0");
                    table.ForeignKey(
                        name: "FK_AccountBanConfigurations_UpdatedByUser",
                        column: x => x.UpdatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AccountBanHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BanType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Trigger = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())"),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TriggeringRatingId = table.Column<long>(type: "bigint", nullable: true),
                    NegativeFeedbackCount = table.Column<int>(type: "int", nullable: true),
                    TemporaryBanSequence = table.Column<int>(type: "int", nullable: true),
                    ReleasedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReleasedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReleaseReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountBanHistories", x => x.Id);
                    table.CheckConstraint("CK_AccountBanHistories_EndAfterStart", "[EndsAt] IS NULL OR [EndsAt] > [StartedAt]");
                    table.CheckConstraint("CK_AccountBanHistories_NegativeFeedbackCount", "[NegativeFeedbackCount] IS NULL OR [NegativeFeedbackCount] >= 0");
                    table.CheckConstraint("CK_AccountBanHistories_TemporaryBanSequence", "[TemporaryBanSequence] IS NULL OR [TemporaryBanSequence] > 0");
                    table.ForeignKey(
                        name: "FK_AccountBanHistories_CreatedByUser",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountBanHistories_ReleasedByUser",
                        column: x => x.ReleasedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountBanHistories_TriggeringRating",
                        column: x => x.TriggeringRatingId,
                        principalTable: "Ratings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AccountBanHistories_User",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AccountBanConfigurations",
                columns: new[]
                {
                    "Id",
                    "NegativeFeedbackThreshold",
                    "NegativeRatingMaxScore",
                    "TemporaryBanDurationDays",
                    "MaximumTemporaryBans",
                    "IsEnabled",
                    "CreatedAt",
                    "UpdatedAt",
                    "UpdatedByUserId"
                },
                values: new object[]
                {
                    1L,
                    5,
                    2,
                    15,
                    3,
                    true,
                    new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
                    null
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountBanConfigurations_UpdatedByUserId",
                table: "AccountBanConfigurations",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountBanHistories_CreatedByUserId",
                table: "AccountBanHistories",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountBanHistories_ReleasedByUserId",
                table: "AccountBanHistories",
                column: "ReleasedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountBanHistories_TriggeringRatingId",
                table: "AccountBanHistories",
                column: "TriggeringRatingId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountBanHistories_UserId_Source_BanType_CreatedAt",
                table: "AccountBanHistories",
                columns: new[] { "UserId", "Source", "BanType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountBanHistories_UserId_Status",
                table: "AccountBanHistories",
                columns: new[] { "UserId", "Status" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountBanHistories");

            migrationBuilder.DropTable(
                name: "AccountBanConfigurations");
        }
    }
}
