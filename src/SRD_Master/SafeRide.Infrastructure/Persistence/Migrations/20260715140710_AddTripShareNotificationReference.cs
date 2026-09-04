using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTripShareNotificationReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Older production databases may not have the legacy UserId index
            // even though their EF history is before this migration.  A regular
            // DropIndex then aborts the migration before ReferenceId is added,
            // causing every TripShared notification insert to fail at runtime.
            migrationBuilder.Sql(
                """
                IF EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'[Notifications]')
                        AND name = N'IX_Notifications_UserId'
                )
                BEGIN
                    DROP INDEX [IX_Notifications_UserId] ON [Notifications];
                END

                IF COL_LENGTH(N'[Notifications]', N'ReferenceId') IS NULL
                BEGIN
                    ALTER TABLE [Notifications] ADD [ReferenceId] bigint NULL;
                END

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'[Notifications]')
                        AND name = N'IX_Notifications_UserId_Type_Reference'
                )
                BEGIN
                    CREATE INDEX [IX_Notifications_UserId_Type_Reference]
                    ON [Notifications] ([UserId], [NotificationType], [ReferenceId]);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'[Notifications]')
                        AND name = N'IX_Notifications_UserId_Type_Reference'
                )
                BEGIN
                    DROP INDEX [IX_Notifications_UserId_Type_Reference] ON [Notifications];
                END

                IF COL_LENGTH(N'[Notifications]', N'ReferenceId') IS NOT NULL
                BEGIN
                    ALTER TABLE [Notifications] DROP COLUMN [ReferenceId];
                END

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'[Notifications]')
                        AND name = N'IX_Notifications_UserId'
                )
                BEGIN
                    CREATE INDEX [IX_Notifications_UserId] ON [Notifications] ([UserId]);
                END
                """);
        }
    }
}
