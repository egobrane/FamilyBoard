using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChoreManagementWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChoreAssignments_ChoreDefinitions_ChoreDefinitionId",
                table: "ChoreAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ChoreAssignments_HouseholdMembers_HouseholdMemberId",
                table: "ChoreAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ChoreCompletions_ChoreAssignments_ChoreAssignmentId",
                table: "ChoreCompletions");

            migrationBuilder.DropForeignKey(
                name: "FK_ChoreCompletions_HouseholdMembers_CompletedByMemberId",
                table: "ChoreCompletions");

            migrationBuilder.DropForeignKey(
                name: "FK_ChoreCompletions_HouseholdMembers_ReviewedByMemberId",
                table: "ChoreCompletions");

            migrationBuilder.DropIndex(
                name: "IX_ChoreCompletions_ChoreAssignmentId",
                table: "ChoreCompletions");

            migrationBuilder.DropIndex(
                name: "IX_ChoreCompletions_CompletedByMemberId",
                table: "ChoreCompletions");

            migrationBuilder.DropIndex(
                name: "IX_ChoreCompletions_ReviewedByMemberId",
                table: "ChoreCompletions");

            migrationBuilder.DropIndex(
                name: "IX_ChoreAssignments_ChoreDefinitionId",
                table: "ChoreAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ChoreAssignments_HouseholdMemberId_DueAt",
                table: "ChoreAssignments");

            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                table: "ChoreDefinitions",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "ChoreDefinitions",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                table: "ChoreCompletions",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "HouseholdId",
                table: "ChoreCompletions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "ChoreCompletions",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedByUserAccountId",
                table: "ChoreCompletions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "ChoreCompletions",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<bool>(
                name: "WasSharedDisplay",
                table: "ChoreCompletions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                table: "ChoreAssignments",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByMemberId",
                table: "ChoreAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionSnapshot",
                table: "ChoreAssignments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DueHasExplicitTime",
                table: "ChoreAssignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DueLocalDate",
                table: "ChoreAssignments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "DueLocalTime",
                table: "ChoreAssignments",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DueTimeZone",
                table: "ChoreAssignments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HouseholdId",
                table: "ChoreAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SkipReason",
                table: "ChoreAssignments",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SkippedAt",
                table: "ChoreAssignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SkippedByMemberId",
                table: "ChoreAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleSnapshot",
                table: "ChoreAssignments",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "ChoreAssignments",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.Sql("""
                UPDATE "ChoreAssignments" AS assignment
                SET "HouseholdId" = definition."HouseholdId",
                    "TitleSnapshot" = definition."Title",
                    "DescriptionSnapshot" = definition."Description",
                    "DueTimeZone" = COALESCE(configuration."TimeZone", 'UTC'),
                    "DueLocalDate" = CASE WHEN assignment."DueAt" IS NULL THEN NULL
                        ELSE (assignment."DueAt" AT TIME ZONE COALESCE(configuration."TimeZone", 'UTC'))::date END,
                    "DueLocalTime" = CASE WHEN assignment."DueAt" IS NULL THEN NULL
                        ELSE (assignment."DueAt" AT TIME ZONE COALESCE(configuration."TimeZone", 'UTC'))::time END,
                    "DueHasExplicitTime" = assignment."DueAt" IS NOT NULL
                FROM "ChoreDefinitions" AS definition
                LEFT JOIN "HouseholdConfigurations" AS configuration
                    ON configuration."HouseholdId" = definition."HouseholdId"
                WHERE assignment."ChoreDefinitionId" = definition."Id";

                UPDATE "ChoreCompletions" AS completion
                SET "HouseholdId" = assignment."HouseholdId"
                FROM "ChoreAssignments" AS assignment
                WHERE completion."ChoreAssignmentId" = assignment."Id";
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "HouseholdId",
                table: "ChoreAssignments",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TitleSnapshot",
                table: "ChoreAssignments",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "HouseholdId",
                table: "ChoreCompletions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ChoreDefinitions_HouseholdId_Id",
                table: "ChoreDefinitions",
                columns: new[] { "HouseholdId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ChoreAssignments_HouseholdId_Id",
                table: "ChoreAssignments",
                columns: new[] { "HouseholdId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ChoreDefinitions_HouseholdId_ClientRequestId",
                table: "ChoreDefinitions",
                columns: new[] { "HouseholdId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChoreCompletions_HouseholdId_ChoreAssignmentId",
                table: "ChoreCompletions",
                columns: new[] { "HouseholdId", "ChoreAssignmentId" },
                unique: true,
                filter: "\"Status\" = 'PendingReview'");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreCompletions_HouseholdId_ClientRequestId",
                table: "ChoreCompletions",
                columns: new[] { "HouseholdId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChoreCompletions_HouseholdId_CompletedByMemberId",
                table: "ChoreCompletions",
                columns: new[] { "HouseholdId", "CompletedByMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChoreCompletions_HouseholdId_ReviewedByMemberId",
                table: "ChoreCompletions",
                columns: new[] { "HouseholdId", "ReviewedByMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChoreCompletions_SubmittedByUserAccountId",
                table: "ChoreCompletions",
                column: "SubmittedByUserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreAssignments_HouseholdId_ChoreDefinitionId",
                table: "ChoreAssignments",
                columns: new[] { "HouseholdId", "ChoreDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChoreAssignments_HouseholdId_ClientRequestId",
                table: "ChoreAssignments",
                columns: new[] { "HouseholdId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChoreAssignments_HouseholdId_CreatedByMemberId",
                table: "ChoreAssignments",
                columns: new[] { "HouseholdId", "CreatedByMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChoreAssignments_HouseholdId_HouseholdMemberId_Status_DueAt",
                table: "ChoreAssignments",
                columns: new[] { "HouseholdId", "HouseholdMemberId", "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChoreAssignments_HouseholdId_SkippedByMemberId",
                table: "ChoreAssignments",
                columns: new[] { "HouseholdId", "SkippedByMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChoreAssignments_HouseholdId_Status_DueAt",
                table: "ChoreAssignments",
                columns: new[] { "HouseholdId", "Status", "DueAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ChoreAssignments_ChoreDefinitions_HouseholdId_ChoreDefiniti~",
                table: "ChoreAssignments",
                columns: new[] { "HouseholdId", "ChoreDefinitionId" },
                principalTable: "ChoreDefinitions",
                principalColumns: new[] { "HouseholdId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChoreAssignments_HouseholdMembers_HouseholdId_CreatedByMemb~",
                table: "ChoreAssignments",
                columns: new[] { "HouseholdId", "CreatedByMemberId" },
                principalTable: "HouseholdMembers",
                principalColumns: new[] { "HouseholdId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChoreAssignments_HouseholdMembers_HouseholdId_HouseholdMemb~",
                table: "ChoreAssignments",
                columns: new[] { "HouseholdId", "HouseholdMemberId" },
                principalTable: "HouseholdMembers",
                principalColumns: new[] { "HouseholdId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChoreAssignments_HouseholdMembers_HouseholdId_SkippedByMemb~",
                table: "ChoreAssignments",
                columns: new[] { "HouseholdId", "SkippedByMemberId" },
                principalTable: "HouseholdMembers",
                principalColumns: new[] { "HouseholdId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChoreCompletions_ChoreAssignments_HouseholdId_ChoreAssignme~",
                table: "ChoreCompletions",
                columns: new[] { "HouseholdId", "ChoreAssignmentId" },
                principalTable: "ChoreAssignments",
                principalColumns: new[] { "HouseholdId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChoreCompletions_HouseholdMembers_HouseholdId_CompletedByMe~",
                table: "ChoreCompletions",
                columns: new[] { "HouseholdId", "CompletedByMemberId" },
                principalTable: "HouseholdMembers",
                principalColumns: new[] { "HouseholdId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChoreCompletions_HouseholdMembers_HouseholdId_ReviewedByMem~",
                table: "ChoreCompletions",
                columns: new[] { "HouseholdId", "ReviewedByMemberId" },
                principalTable: "HouseholdMembers",
                principalColumns: new[] { "HouseholdId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChoreCompletions_UserAccounts_SubmittedByUserAccountId",
                table: "ChoreCompletions",
                column: "SubmittedByUserAccountId",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChoreAssignments_ChoreDefinitions_HouseholdId_ChoreDefiniti~",
                table: "ChoreAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ChoreAssignments_HouseholdMembers_HouseholdId_CreatedByMemb~",
                table: "ChoreAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ChoreAssignments_HouseholdMembers_HouseholdId_HouseholdMemb~",
                table: "ChoreAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ChoreAssignments_HouseholdMembers_HouseholdId_SkippedByMemb~",
                table: "ChoreAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ChoreCompletions_ChoreAssignments_HouseholdId_ChoreAssignme~",
                table: "ChoreCompletions");

            migrationBuilder.DropForeignKey(
                name: "FK_ChoreCompletions_HouseholdMembers_HouseholdId_CompletedByMe~",
                table: "ChoreCompletions");

            migrationBuilder.DropForeignKey(
                name: "FK_ChoreCompletions_HouseholdMembers_HouseholdId_ReviewedByMem~",
                table: "ChoreCompletions");

            migrationBuilder.DropForeignKey(
                name: "FK_ChoreCompletions_UserAccounts_SubmittedByUserAccountId",
                table: "ChoreCompletions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ChoreDefinitions_HouseholdId_Id",
                table: "ChoreDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_ChoreDefinitions_HouseholdId_ClientRequestId",
                table: "ChoreDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_ChoreCompletions_HouseholdId_ChoreAssignmentId",
                table: "ChoreCompletions");

            migrationBuilder.DropIndex(
                name: "IX_ChoreCompletions_HouseholdId_ClientRequestId",
                table: "ChoreCompletions");

            migrationBuilder.DropIndex(
                name: "IX_ChoreCompletions_HouseholdId_CompletedByMemberId",
                table: "ChoreCompletions");

            migrationBuilder.DropIndex(
                name: "IX_ChoreCompletions_HouseholdId_ReviewedByMemberId",
                table: "ChoreCompletions");

            migrationBuilder.DropIndex(
                name: "IX_ChoreCompletions_SubmittedByUserAccountId",
                table: "ChoreCompletions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ChoreAssignments_HouseholdId_Id",
                table: "ChoreAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ChoreAssignments_HouseholdId_ChoreDefinitionId",
                table: "ChoreAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ChoreAssignments_HouseholdId_ClientRequestId",
                table: "ChoreAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ChoreAssignments_HouseholdId_CreatedByMemberId",
                table: "ChoreAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ChoreAssignments_HouseholdId_HouseholdMemberId_Status_DueAt",
                table: "ChoreAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ChoreAssignments_HouseholdId_SkippedByMemberId",
                table: "ChoreAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ChoreAssignments_HouseholdId_Status_DueAt",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "ChoreDefinitions");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "ChoreDefinitions");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "ChoreCompletions");

            migrationBuilder.DropColumn(
                name: "HouseholdId",
                table: "ChoreCompletions");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "ChoreCompletions");

            migrationBuilder.DropColumn(
                name: "SubmittedByUserAccountId",
                table: "ChoreCompletions");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "ChoreCompletions");

            migrationBuilder.DropColumn(
                name: "WasSharedDisplay",
                table: "ChoreCompletions");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "CreatedByMemberId",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "DescriptionSnapshot",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "DueHasExplicitTime",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "DueLocalDate",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "DueLocalTime",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "DueTimeZone",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "HouseholdId",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "SkipReason",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "SkippedAt",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "SkippedByMemberId",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "TitleSnapshot",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "ChoreAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreCompletions_ChoreAssignmentId",
                table: "ChoreCompletions",
                column: "ChoreAssignmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChoreCompletions_CompletedByMemberId",
                table: "ChoreCompletions",
                column: "CompletedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreCompletions_ReviewedByMemberId",
                table: "ChoreCompletions",
                column: "ReviewedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreAssignments_ChoreDefinitionId",
                table: "ChoreAssignments",
                column: "ChoreDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreAssignments_HouseholdMemberId_DueAt",
                table: "ChoreAssignments",
                columns: new[] { "HouseholdMemberId", "DueAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ChoreAssignments_ChoreDefinitions_ChoreDefinitionId",
                table: "ChoreAssignments",
                column: "ChoreDefinitionId",
                principalTable: "ChoreDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChoreAssignments_HouseholdMembers_HouseholdMemberId",
                table: "ChoreAssignments",
                column: "HouseholdMemberId",
                principalTable: "HouseholdMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChoreCompletions_ChoreAssignments_ChoreAssignmentId",
                table: "ChoreCompletions",
                column: "ChoreAssignmentId",
                principalTable: "ChoreAssignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChoreCompletions_HouseholdMembers_CompletedByMemberId",
                table: "ChoreCompletions",
                column: "CompletedByMemberId",
                principalTable: "HouseholdMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChoreCompletions_HouseholdMembers_ReviewedByMemberId",
                table: "ChoreCompletions",
                column: "ReviewedByMemberId",
                principalTable: "HouseholdMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
