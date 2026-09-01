using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SafeRide.Infrastructure.Persistence;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260829163000_AddInsuranceDocumentsPhaseA3")]
public partial class AddInsuranceDocumentsPhaseA3 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        CreateDocumentTable(migrationBuilder, "InsurancePolicyDocuments", "VehicleInsurancePolicyId", "VehicleInsurancePolicies", 30, "CK_InsurancePolicyDocuments_FileSize");
        CreateDocumentTable(migrationBuilder, "InsuranceClaimDocuments", "ProtectionClaimId", "ProtectionClaims", 35, "CK_InsuranceClaimDocuments_FileSize");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("InsuranceClaimDocuments");
        migrationBuilder.DropTable("InsurancePolicyDocuments");
    }

    private static void CreateDocumentTable(MigrationBuilder migrationBuilder, string tableName, string foreignKeyColumn, string principalTable, int typeLength, string checkName)
    {
        migrationBuilder.CreateTable(tableName, table => new
        {
            Id = table.Column<long>("bigint", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
            DocumentType = table.Column<string>($"nvarchar({typeLength})", maxLength: typeLength, nullable: false),
            StorageObjectKey = table.Column<string>("nvarchar(500)", maxLength: 500, nullable: false),
            OriginalFileName = table.Column<string>("nvarchar(255)", maxLength: 255, nullable: false),
            ContentType = table.Column<string>("nvarchar(100)", maxLength: 100, nullable: false),
            FileSizeBytes = table.Column<long>("bigint", nullable: false),
            Sha256Hash = table.Column<string>("nvarchar(64)", maxLength: 64, nullable: false),
            UploadedByUserId = table.Column<Guid>("uniqueidentifier", nullable: false),
            UploadedAtUtc = table.Column<DateTime>("datetime2", nullable: false),
            AggregateId = table.Column<long>("bigint", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey($"PK_{tableName}", x => x.Id);
            table.ForeignKey($"FK_{tableName}_{principalTable}_{foreignKeyColumn}", x => x.AggregateId, principalTable, "Id", onDelete: ReferentialAction.Restrict);
            table.CheckConstraint(checkName, "[FileSizeBytes] > 0 AND [FileSizeBytes] <= 10000000");
        });
        migrationBuilder.RenameColumn("AggregateId", tableName, foreignKeyColumn);
        migrationBuilder.CreateIndex($"IX_{tableName}_{foreignKeyColumn}_UploadedAtUtc", tableName, new[] { foreignKeyColumn, "UploadedAtUtc" });
    }
}
