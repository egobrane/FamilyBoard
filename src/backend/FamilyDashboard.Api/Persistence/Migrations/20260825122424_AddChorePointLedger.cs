using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChorePointLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PointTransactions_ChoreCompletions_ChoreCompletionId",
                table: "PointTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_PointTransactions_HouseholdMembers_HouseholdMemberId",
                table: "PointTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_HouseholdId",
                table: "PointTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_HouseholdMemberId_CreatedAt",
                table: "PointTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_IdempotencyKey",
                table: "PointTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChoreDefinitions_DefaultPointValue",
                table: "ChoreDefinitions");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByMemberId",
                table: "PointTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversesPointTransactionId",
                table: "PointTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PointValueSnapshot",
                table: "ChoreCompletions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PointValueSnapshot",
                table: "ChoreAssignments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE "ChoreAssignments" AS assignment
                SET "PointValueSnapshot" = definition."DefaultPointValue"
                FROM "ChoreDefinitions" AS definition
                WHERE assignment."HouseholdId" = definition."HouseholdId"
                  AND assignment."ChoreDefinitionId" = definition."Id";

                UPDATE "ChoreCompletions" AS completion
                SET "PointValueSnapshot" = assignment."PointValueSnapshot"
                FROM "ChoreAssignments" AS assignment
                WHERE completion."HouseholdId" = assignment."HouseholdId"
                  AND completion."ChoreAssignmentId" = assignment."Id";
                """);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PointTransactions_HouseholdId_Id",
                table: "PointTransactions",
                columns: new[] { "HouseholdId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_ChoreCompletions_HouseholdId_Id",
                table: "ChoreCompletions",
                columns: new[] { "HouseholdId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_HouseholdId_ChoreCompletionId",
                table: "PointTransactions",
                columns: new[] { "HouseholdId", "ChoreCompletionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_HouseholdId_CreatedByMemberId",
                table: "PointTransactions",
                columns: new[] { "HouseholdId", "CreatedByMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_HouseholdId_HouseholdMemberId_CreatedAt_Id",
                table: "PointTransactions",
                columns: new[] { "HouseholdId", "HouseholdMemberId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_HouseholdId_IdempotencyKey",
                table: "PointTransactions",
                columns: new[] { "HouseholdId", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_HouseholdId_ReversesPointTransactionId",
                table: "PointTransactions",
                columns: new[] { "HouseholdId", "ReversesPointTransactionId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PointTransactions_ChoreCompletionLink",
                table: "PointTransactions",
                sql: "\"Type\" <> 'ChoreCompletion' OR (\"ChoreCompletionId\" IS NOT NULL AND \"Amount\" > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PointTransactions_ReversalLink",
                table: "PointTransactions",
                sql: "(\"Type\" = 'Reversal' AND \"ReversesPointTransactionId\" IS NOT NULL) OR (\"Type\" <> 'Reversal' AND \"ReversesPointTransactionId\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChoreDefinitions_DefaultPointValue",
                table: "ChoreDefinitions",
                sql: "\"DefaultPointValue\" BETWEEN 0 AND 10000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChoreCompletions_PointValueSnapshot",
                table: "ChoreCompletions",
                sql: "\"PointValueSnapshot\" BETWEEN 0 AND 10000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChoreAssignments_PointValueSnapshot",
                table: "ChoreAssignments",
                sql: "\"PointValueSnapshot\" BETWEEN 0 AND 10000");

            migrationBuilder.AddForeignKey(
                name: "FK_PointTransactions_ChoreCompletions_HouseholdId_ChoreComplet~",
                table: "PointTransactions",
                columns: new[] { "HouseholdId", "ChoreCompletionId" },
                principalTable: "ChoreCompletions",
                principalColumns: new[] { "HouseholdId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PointTransactions_HouseholdMembers_HouseholdId_CreatedByMem~",
                table: "PointTransactions",
                columns: new[] { "HouseholdId", "CreatedByMemberId" },
                principalTable: "HouseholdMembers",
                principalColumns: new[] { "HouseholdId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PointTransactions_HouseholdMembers_HouseholdId_HouseholdMem~",
                table: "PointTransactions",
                columns: new[] { "HouseholdId", "HouseholdMemberId" },
                principalTable: "HouseholdMembers",
                principalColumns: new[] { "HouseholdId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PointTransactions_PointTransactions_HouseholdId_ReversesPoi~",
                table: "PointTransactions",
                columns: new[] { "HouseholdId", "ReversesPointTransactionId" },
                principalTable: "PointTransactions",
                principalColumns: new[] { "HouseholdId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PointTransactions_ChoreCompletions_HouseholdId_ChoreComplet~",
                table: "PointTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_PointTransactions_HouseholdMembers_HouseholdId_CreatedByMem~",
                table: "PointTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_PointTransactions_HouseholdMembers_HouseholdId_HouseholdMem~",
                table: "PointTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_PointTransactions_PointTransactions_HouseholdId_ReversesPoi~",
                table: "PointTransactions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PointTransactions_HouseholdId_Id",
                table: "PointTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_HouseholdId_ChoreCompletionId",
                table: "PointTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_HouseholdId_CreatedByMemberId",
                table: "PointTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_HouseholdId_HouseholdMemberId_CreatedAt_Id",
                table: "PointTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_HouseholdId_IdempotencyKey",
                table: "PointTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_HouseholdId_ReversesPointTransactionId",
                table: "PointTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PointTransactions_ChoreCompletionLink",
                table: "PointTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PointTransactions_ReversalLink",
                table: "PointTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChoreDefinitions_DefaultPointValue",
                table: "ChoreDefinitions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ChoreCompletions_HouseholdId_Id",
                table: "ChoreCompletions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChoreCompletions_PointValueSnapshot",
                table: "ChoreCompletions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ChoreAssignments_PointValueSnapshot",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "CreatedByMemberId",
                table: "PointTransactions");

            migrationBuilder.DropColumn(
                name: "ReversesPointTransactionId",
                table: "PointTransactions");

            migrationBuilder.DropColumn(
                name: "PointValueSnapshot",
                table: "ChoreCompletions");

            migrationBuilder.DropColumn(
                name: "PointValueSnapshot",
                table: "ChoreAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_HouseholdId",
                table: "PointTransactions",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_HouseholdMemberId_CreatedAt",
                table: "PointTransactions",
                columns: new[] { "HouseholdMemberId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_IdempotencyKey",
                table: "PointTransactions",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ChoreDefinitions_DefaultPointValue",
                table: "ChoreDefinitions",
                sql: "\"DefaultPointValue\" >= 0");

            migrationBuilder.AddForeignKey(
                name: "FK_PointTransactions_ChoreCompletions_ChoreCompletionId",
                table: "PointTransactions",
                column: "ChoreCompletionId",
                principalTable: "ChoreCompletions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PointTransactions_HouseholdMembers_HouseholdMemberId",
                table: "PointTransactions",
                column: "HouseholdMemberId",
                principalTable: "HouseholdMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
