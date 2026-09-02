using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOpenChoreAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "HouseholdMemberId",
                table: "ChoreSchedules",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "AssignmentMode",
                table: "ChoreSchedules",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Assigned");

            migrationBuilder.AlterColumn<Guid>(
                name: "HouseholdMemberId",
                table: "ChoreAssignments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "AssignmentMode",
                table: "ChoreAssignments",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Assigned");

            migrationBuilder.AddColumn<Guid>(
                name: "ClaimClientRequestId",
                table: "ChoreAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClaimedAt",
                table: "ChoreAssignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClaimedByMemberId",
                table: "ChoreAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ClaimedFromSharedDisplay",
                table: "ChoreAssignments",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChoreSchedules_AssignmentModeMember",
                table: "ChoreSchedules",
                sql: "(\"AssignmentMode\" = 'Assigned' AND \"HouseholdMemberId\" IS NOT NULL) OR (\"AssignmentMode\" = 'Open' AND \"HouseholdMemberId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreAssignments_HouseholdId_ClaimClientRequestId",
                table: "ChoreAssignments",
                columns: new[] { "HouseholdId", "ClaimClientRequestId" },
                unique: true,
                filter: "\"ClaimClientRequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreAssignments_HouseholdId_ClaimedByMemberId",
                table: "ChoreAssignments",
                columns: new[] { "HouseholdId", "ClaimedByMemberId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChoreAssignments_AssignmentModeMember",
                table: "ChoreAssignments",
                sql: "(\"AssignmentMode\" = 'Assigned' AND \"HouseholdMemberId\" IS NOT NULL) OR \"AssignmentMode\" = 'Open'");

            migrationBuilder.AddForeignKey(
                name: "FK_ChoreAssignments_HouseholdMembers_HouseholdId_ClaimedByMemb~",
                table: "ChoreAssignments",
                columns: new[] { "HouseholdId", "ClaimedByMemberId" },
                principalTable: "HouseholdMembers",
                principalColumns: new[] { "HouseholdId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChoreAssignments_HouseholdMembers_HouseholdId_ClaimedByMemb~",
                table: "ChoreAssignments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChoreSchedules_AssignmentModeMember",
                table: "ChoreSchedules");

            migrationBuilder.DropIndex(
                name: "IX_ChoreAssignments_HouseholdId_ClaimClientRequestId",
                table: "ChoreAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ChoreAssignments_HouseholdId_ClaimedByMemberId",
                table: "ChoreAssignments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChoreAssignments_AssignmentModeMember",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "AssignmentMode",
                table: "ChoreSchedules");

            migrationBuilder.DropColumn(
                name: "AssignmentMode",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "ClaimClientRequestId",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "ClaimedAt",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "ClaimedByMemberId",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "ClaimedFromSharedDisplay",
                table: "ChoreAssignments");

            migrationBuilder.AlterColumn<Guid>(
                name: "HouseholdMemberId",
                table: "ChoreSchedules",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "HouseholdMemberId",
                table: "ChoreAssignments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
