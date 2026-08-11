using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SecureTripSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "TripShares",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "(sysutcdatetime())",
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<DateTime>(
                name: "OpenedAt",
                table: "TripShares",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedAt",
                table: "TripShares",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TokenHash",
                table: "TripShares",
                type: "char(64)",
                unicode: false,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [TripShares]
                SET [TokenHash] = CONVERT(char(64), HASHBYTES('SHA2_256', CONVERT(varbinary(max), [ShareToken])), 2);

                ;WITH DuplicateTokens AS
                (
                    SELECT [Id], ROW_NUMBER() OVER (PARTITION BY [TokenHash] ORDER BY [Id]) AS [RowNumber]
                    FROM [TripShares]
                )
                UPDATE shares
                SET [TokenHash] = CONVERT(char(64), HASHBYTES('SHA2_256', CONVERT(varbinary(max), CONCAT(shares.[ShareToken], ':', shares.[Id]))), 2)
                FROM [TripShares] shares
                INNER JOIN DuplicateTokens duplicates ON duplicates.[Id] = shares.[Id]
                WHERE duplicates.[RowNumber] > 1;

                ;WITH DuplicateRecipients AS
                (
                    SELECT [Id], ROW_NUMBER() OVER (
                        PARTITION BY [TripId], [RecipientUserId]
                        ORDER BY [CreatedAt] DESC, [Id] DESC) AS [RowNumber]
                    FROM [TripShares]
                )
                UPDATE shares
                SET [RevokedAt] = SYSUTCDATETIME()
                FROM [TripShares] shares
                INNER JOIN DuplicateRecipients duplicates ON duplicates.[Id] = shares.[Id]
                WHERE duplicates.[RowNumber] > 1;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "TokenHash",
                table: "TripShares",
                type: "char(64)",
                unicode: false,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "char(64)",
                oldUnicode: false,
                oldNullable: true);

            // Some deployed databases have legacy indexes/constraints that were
            // created outside the EF migration history and still include ShareToken.
            // SQL Server requires every dependent object to be removed before the
            // plaintext token column can be dropped.
            migrationBuilder.Sql(
                """
                DECLARE @dropShareTokenDependencies nvarchar(max) = N'';

                SELECT @dropShareTokenDependencies += CASE
                    WHEN [indexes].[is_unique_constraint] = 1
                        THEN N'ALTER TABLE [TripShares] DROP CONSTRAINT ' + QUOTENAME([indexes].[name]) + N';'
                    ELSE N'DROP INDEX ' + QUOTENAME([indexes].[name]) + N' ON [TripShares];'
                END
                FROM [sys].[indexes] AS [indexes]
                WHERE [indexes].[object_id] = OBJECT_ID(N'[TripShares]')
                    AND [indexes].[is_primary_key] = 0
                    AND EXISTS
                    (
                        SELECT 1
                        FROM [sys].[index_columns] AS [indexColumns]
                        INNER JOIN [sys].[columns] AS [columns]
                            ON [columns].[object_id] = [indexColumns].[object_id]
                            AND [columns].[column_id] = [indexColumns].[column_id]
                        WHERE [indexColumns].[object_id] = [indexes].[object_id]
                            AND [indexColumns].[index_id] = [indexes].[index_id]
                            AND [columns].[name] = N'ShareToken'
                    );

                IF LEN(@dropShareTokenDependencies) > 0
                    EXEC sp_executesql @dropShareTokenDependencies;
                """);

            migrationBuilder.DropColumn(
                name: "ShareToken",
                table: "TripShares");

            migrationBuilder.CreateIndex(
                name: "IX_TripShares_ActiveLookup",
                table: "TripShares",
                columns: new[] { "TripId", "ExpiresAt", "RevokedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_TripShares_ActiveRecipient",
                table: "TripShares",
                columns: new[] { "TripId", "RecipientUserId" },
                unique: true,
                filter: "[RevokedAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_TripShares_TokenHash",
                table: "TripShares",
                column: "TokenHash",
                unique: true);

            migrationBuilder.Sql(
                "ALTER TABLE [TripShares] WITH NOCHECK ADD CONSTRAINT [CK_TripShares_DifferentUsers] CHECK ([SharedByUserId] <> [RecipientUserId]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TripShares_ActiveLookup",
                table: "TripShares");

            migrationBuilder.DropIndex(
                name: "UX_TripShares_ActiveRecipient",
                table: "TripShares");

            migrationBuilder.DropIndex(
                name: "UX_TripShares_TokenHash",
                table: "TripShares");

            migrationBuilder.Sql(
                "ALTER TABLE [TripShares] DROP CONSTRAINT [CK_TripShares_DifferentUsers];");

            migrationBuilder.DropColumn(
                name: "OpenedAt",
                table: "TripShares");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "TripShares");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "TripShares",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldDefaultValueSql: "(sysutcdatetime())");

            migrationBuilder.AddColumn<string>(
                name: "ShareToken",
                table: "TripShares",
                type: "varchar(100)",
                unicode: false,
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                "UPDATE [TripShares] SET [ShareToken] = [TokenHash];");

            migrationBuilder.DropColumn(
                name: "TokenHash",
                table: "TripShares");
        }
    }
}
