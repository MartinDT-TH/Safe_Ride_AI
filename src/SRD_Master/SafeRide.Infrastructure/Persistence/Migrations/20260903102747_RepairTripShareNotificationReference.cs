using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RepairTripShareNotificationReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 20260715140710 may already be present in __EFMigrationsHistory on
            // databases where schema drift left Notifications.ReferenceId or
            // its replacement index missing. A new idempotent migration is
            // required because EF never executes an already-applied migration
            // again after its source is changed.
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[Notifications]', N'U') IS NULL
                BEGIN
                    THROW 51000, 'Cannot repair trip-share notifications because dbo.Notifications does not exist.', 1;
                END;

                IF EXISTS
                (
                    SELECT 1
                    FROM [sys].[indexes]
                    WHERE [object_id] = OBJECT_ID(N'[dbo].[Notifications]')
                        AND [name] = N'IX_Notifications_UserId'
                )
                BEGIN
                    DROP INDEX [IX_Notifications_UserId] ON [dbo].[Notifications];
                END;

                IF COL_LENGTH(N'[dbo].[Notifications]', N'ReferenceId') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Notifications] ADD [ReferenceId] bigint NULL;
                END;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM [sys].[indexes]
                    WHERE [object_id] = OBJECT_ID(N'[dbo].[Notifications]')
                        AND [name] = N'IX_Notifications_UserId_Type_Reference'
                )
                BEGIN
                    CREATE INDEX [IX_Notifications_UserId_Type_Reference]
                        ON [dbo].[Notifications] ([UserId], [NotificationType], [ReferenceId]);
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The column and index belong to the earlier
            // AddTripShareNotificationReference migration. Rolling back this
            // repair must not remove schema that EF still considers applied.
        }
    }
}
