using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SafeRide.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeCustomerIntoxicationRootCause : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS
                (
                    SELECT 1
                    FROM [AccidentLiabilityCauses] AS [intoxication]
                    WHERE [intoxication].[RootCause] = N'CUSTOMER_INTOXICATION'
                      AND
                      (
                          [intoxication].[ResponsibleParty] <> N'CUSTOMER'
                          OR NOT EXISTS
                          (
                              SELECT 1
                              FROM [AccidentLiabilityCauses] AS [interference]
                              WHERE [interference].[AssessmentId] = [intoxication].[AssessmentId]
                                AND [interference].[RootCause] = N'CUSTOMER_INTERFERENCE'
                                AND [interference].[ResponsibleParty] = N'CUSTOMER'
                          )
                      )
                )
                BEGIN
                    THROW 51000, N'Cannot normalize CUSTOMER_INTOXICATION without a matching CUSTOMER_INTERFERENCE allocation.', 1;
                END;

                UPDATE [interference]
                SET [interference].[Percentage] = [interference].[Percentage] + [intoxication].[Percentage]
                FROM [AccidentLiabilityCauses] AS [interference]
                INNER JOIN [AccidentLiabilityCauses] AS [intoxication]
                    ON [intoxication].[AssessmentId] = [interference].[AssessmentId]
                   AND [intoxication].[RootCause] = N'CUSTOMER_INTOXICATION'
                   AND [intoxication].[ResponsibleParty] = N'CUSTOMER'
                WHERE [interference].[RootCause] = N'CUSTOMER_INTERFERENCE'
                  AND [interference].[ResponsibleParty] = N'CUSTOMER';

                DELETE FROM [AccidentLiabilityCauses]
                WHERE [RootCause] = N'CUSTOMER_INTOXICATION';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "CUSTOMER_INTOXICATION allocations cannot be reconstructed after they are merged into CUSTOMER_INTERFERENCE.");
        }
    }
}
