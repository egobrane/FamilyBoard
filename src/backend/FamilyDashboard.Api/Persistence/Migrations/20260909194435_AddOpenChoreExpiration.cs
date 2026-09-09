using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOpenChoreExpiration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClaimExpiresAt",
                table: "ChoreAssignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiredAt",
                table: "ChoreAssignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "ChoreAssignments"
                SET "ClaimExpiresAt" =
                    CASE
                        WHEN "DueLocalDate" IS NOT NULL AND "DueTimeZone" IS NOT NULL
                            THEN (("DueLocalDate" + 1)::timestamp without time zone AT TIME ZONE "DueTimeZone")
                        ELSE COALESCE("DueAt", "CreatedAt") + INTERVAL '1 day'
                    END
                WHERE "AssignmentMode" = 'Open';

                UPDATE "ChoreAssignments"
                SET "Status" = 'Skipped',
                    "ExpiredAt" = CURRENT_TIMESTAMP,
                    "SkipReason" = 'Expired unclaimed at the end of its due day.',
                    "UpdatedAt" = CURRENT_TIMESTAMP,
                    "Version" = "Version" + 1
                WHERE "AssignmentMode" = 'Open'
                  AND "Status" = 'Pending'
                  AND "ClaimedAt" IS NULL
                  AND "ClaimExpiresAt" <= CURRENT_TIMESTAMP;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ChoreAssignments_HouseholdId_Status_AssignmentMode_ClaimExp~",
                table: "ChoreAssignments",
                columns: new[] { "HouseholdId", "Status", "AssignmentMode", "ClaimExpiresAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChoreAssignments_ExpiredAt",
                table: "ChoreAssignments",
                sql: "\"ExpiredAt\" IS NULL OR (\"Status\" = 'Skipped' AND \"AssignmentMode\" = 'Open' AND \"ClaimedAt\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChoreAssignments_OpenClaimExpiry",
                table: "ChoreAssignments",
                sql: "(\"AssignmentMode\" = 'Assigned' AND \"ClaimExpiresAt\" IS NULL) OR (\"AssignmentMode\" = 'Open' AND \"ClaimExpiresAt\" IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChoreAssignments_HouseholdId_Status_AssignmentMode_ClaimExp~",
                table: "ChoreAssignments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChoreAssignments_ExpiredAt",
                table: "ChoreAssignments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChoreAssignments_OpenClaimExpiry",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "ClaimExpiresAt",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "ExpiredAt",
                table: "ChoreAssignments");
        }
    }
}
