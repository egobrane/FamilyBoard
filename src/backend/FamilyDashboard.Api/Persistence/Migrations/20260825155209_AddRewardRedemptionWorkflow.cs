using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRewardRedemptionWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PointTransactions_RewardRedemptions_RewardRedemptionId",
                table: "PointTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_RewardRedemptions_HouseholdMembers_HouseholdMemberId",
                table: "RewardRedemptions");

            migrationBuilder.DropForeignKey(
                name: "FK_RewardRedemptions_HouseholdMembers_ReviewedByMemberId",
                table: "RewardRedemptions");

            migrationBuilder.DropForeignKey(
                name: "FK_RewardRedemptions_Rewards_RewardId",
                table: "RewardRedemptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rewards_PointCost",
                table: "Rewards");

            migrationBuilder.DropIndex(
                name: "IX_RewardRedemptions_HouseholdMemberId_RequestedAt",
                table: "RewardRedemptions");

            migrationBuilder.DropIndex(
                name: "IX_RewardRedemptions_ReviewedByMemberId",
                table: "RewardRedemptions");

            migrationBuilder.DropIndex(
                name: "IX_RewardRedemptions_RewardId",
                table: "RewardRedemptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RewardRedemptions_PointCostSnapshot",
                table: "RewardRedemptions");

            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                table: "Rewards",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByMemberId",
                table: "Rewards",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedByMemberId",
                table: "Rewards",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "Rewards",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "RewardRedemptions",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                table: "RewardRedemptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancelledByMemberId",
                table: "RewardRedemptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                table: "RewardRedemptions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FulfilledAt",
                table: "RewardRedemptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FulfilledByMemberId",
                table: "RewardRedemptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HouseholdId",
                table: "RewardRedemptions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedByMemberId",
                table: "RewardRedemptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RequestedByUserAccountId",
                table: "RewardRedemptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "RewardRedemptions",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RewardDescriptionSnapshot",
                table: "RewardRedemptions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RewardTitleSnapshot",
                table: "RewardRedemptions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "RewardRedemptions",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<bool>(
                name: "WasSharedDisplay",
                table: "RewardRedemptions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE "Rewards"
                SET "ClientRequestId" = "Id", "Version" = 1
                WHERE "ClientRequestId" = '00000000-0000-0000-0000-000000000000';

                UPDATE "RewardRedemptions" AS redemption
                SET "HouseholdId" = reward."HouseholdId",
                    "ClientRequestId" = redemption."Id",
                    "RewardTitleSnapshot" = reward."Title",
                    "RewardDescriptionSnapshot" = reward."Description",
                    "Version" = 1
                FROM "Rewards" AS reward
                WHERE redemption."RewardId" = reward."Id";
                """);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Rewards_HouseholdId_Id",
                table: "Rewards",
                columns: new[] { "HouseholdId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_RewardRedemptions_HouseholdId_Id",
                table: "RewardRedemptions",
                columns: new[] { "HouseholdId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Rewards_HouseholdId_ClientRequestId",
                table: "Rewards",
                columns: new[] { "HouseholdId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rewards_HouseholdId_CreatedByMemberId",
                table: "Rewards",
                columns: new[] { "HouseholdId", "CreatedByMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_Rewards_HouseholdId_UpdatedByMemberId",
                table: "Rewards",
                columns: new[] { "HouseholdId", "UpdatedByMemberId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rewards_ClientRequestId",
                table: "Rewards",
                sql: "\"ClientRequestId\" <> '00000000-0000-0000-0000-000000000000'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rewards_PointCost",
                table: "Rewards",
                sql: "\"PointCost\" BETWEEN 1 AND 10000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rewards_Version",
                table: "Rewards",
                sql: "\"Version\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_RewardRedemptions_HouseholdId_CancelledByMemberId",
                table: "RewardRedemptions",
                columns: new[] { "HouseholdId", "CancelledByMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_RewardRedemptions_HouseholdId_ClientRequestId",
                table: "RewardRedemptions",
                columns: new[] { "HouseholdId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RewardRedemptions_HouseholdId_FulfilledByMemberId",
                table: "RewardRedemptions",
                columns: new[] { "HouseholdId", "FulfilledByMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_RewardRedemptions_HouseholdId_HouseholdMemberId_RequestedAt",
                table: "RewardRedemptions",
                columns: new[] { "HouseholdId", "HouseholdMemberId", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RewardRedemptions_HouseholdId_RequestedByMemberId",
                table: "RewardRedemptions",
                columns: new[] { "HouseholdId", "RequestedByMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_RewardRedemptions_HouseholdId_ReviewedByMemberId",
                table: "RewardRedemptions",
                columns: new[] { "HouseholdId", "ReviewedByMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_RewardRedemptions_HouseholdId_RewardId",
                table: "RewardRedemptions",
                columns: new[] { "HouseholdId", "RewardId" });

            migrationBuilder.CreateIndex(
                name: "IX_RewardRedemptions_HouseholdId_Status_RequestedAt",
                table: "RewardRedemptions",
                columns: new[] { "HouseholdId", "Status", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RewardRedemptions_RequestedByUserAccountId",
                table: "RewardRedemptions",
                column: "RequestedByUserAccountId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RewardRedemptions_ClientRequestId",
                table: "RewardRedemptions",
                sql: "\"ClientRequestId\" <> '00000000-0000-0000-0000-000000000000'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RewardRedemptions_PointCostSnapshot",
                table: "RewardRedemptions",
                sql: "\"PointCostSnapshot\" BETWEEN 1 AND 10000");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RewardRedemptions_Version",
                table: "RewardRedemptions",
                sql: "\"Version\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_HouseholdId_RewardRedemptionId",
                table: "PointTransactions",
                columns: new[] { "HouseholdId", "RewardRedemptionId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PointTransactions_RewardRedemptionLink",
                table: "PointTransactions",
                sql: "\"Type\" <> 'RewardRedemption' OR (\"RewardRedemptionId\" IS NOT NULL AND \"Amount\" < 0)");

            migrationBuilder.AddForeignKey(
                name: "FK_PointTransactions_RewardRedemptions_HouseholdId_RewardRedem~",
                table: "PointTransactions",
                columns: new[] { "HouseholdId", "RewardRedemptionId" },
                principalTable: "RewardRedemptions",
                principalColumns: new[] { "HouseholdId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RewardRedemptions_HouseholdMembers_HouseholdId_CancelledByM~",
                table: "RewardRedemptions",
                columns: new[] { "HouseholdId", "CancelledByMemberId" },
                principalTable: "HouseholdMembers",
                principalColumns: new[] { "HouseholdId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RewardRedemptions_HouseholdMembers_HouseholdId_FulfilledByM~",
                table: "RewardRedemptions",
                columns: new[] { "HouseholdId", "FulfilledByMemberId" },
                principalTable: "HouseholdMembers",
                principalColumns: new[] { "HouseholdId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RewardRedemptions_HouseholdMembers_HouseholdId_HouseholdMem~",
                table: "RewardRedemptions",
                columns: new[] { "HouseholdId", "HouseholdMemberId" },
                principalTable: "HouseholdMembers",
                principalColumns: new[] { "HouseholdId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RewardRedemptions_HouseholdMembers_HouseholdId_RequestedByM~",
                table: "RewardRedemptions",
                columns: new[] { "HouseholdId", "RequestedByMemberId" },
                principalTable: "HouseholdMembers",
                principalColumns: new[] { "HouseholdId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RewardRedemptions_HouseholdMembers_HouseholdId_ReviewedByMe~",
                table: "RewardRedemptions",
                columns: new[] { "HouseholdId", "ReviewedByMemberId" },
                principalTable: "HouseholdMembers",
                principalColumns: new[] { "HouseholdId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RewardRedemptions_Rewards_HouseholdId_RewardId",
                table: "RewardRedemptions",
                columns: new[] { "HouseholdId", "RewardId" },
                principalTable: "Rewards",
                principalColumns: new[] { "HouseholdId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RewardRedemptions_UserAccounts_RequestedByUserAccountId",
                table: "RewardRedemptions",
                column: "RequestedByUserAccountId",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rewards_HouseholdMembers_HouseholdId_CreatedByMemberId",
                table: "Rewards",
                columns: new[] { "HouseholdId", "CreatedByMemberId" },
                principalTable: "HouseholdMembers",
                principalColumns: new[] { "HouseholdId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rewards_HouseholdMembers_HouseholdId_UpdatedByMemberId",
                table: "Rewards",
                columns: new[] { "HouseholdId", "UpdatedByMemberId" },
                principalTable: "HouseholdMembers",
                principalColumns: new[] { "HouseholdId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PointTransactions_RewardRedemptions_HouseholdId_RewardRedem~",
                table: "PointTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_RewardRedemptions_HouseholdMembers_HouseholdId_CancelledByM~",
                table: "RewardRedemptions");

            migrationBuilder.DropForeignKey(
                name: "FK_RewardRedemptions_HouseholdMembers_HouseholdId_FulfilledByM~",
                table: "RewardRedemptions");

            migrationBuilder.DropForeignKey(
                name: "FK_RewardRedemptions_HouseholdMembers_HouseholdId_HouseholdMem~",
                table: "RewardRedemptions");

            migrationBuilder.DropForeignKey(
                name: "FK_RewardRedemptions_HouseholdMembers_HouseholdId_RequestedByM~",
                table: "RewardRedemptions");

            migrationBuilder.DropForeignKey(
                name: "FK_RewardRedemptions_HouseholdMembers_HouseholdId_ReviewedByMe~",
                table: "RewardRedemptions");

            migrationBuilder.DropForeignKey(
                name: "FK_RewardRedemptions_Rewards_HouseholdId_RewardId",
                table: "RewardRedemptions");

            migrationBuilder.DropForeignKey(
                name: "FK_RewardRedemptions_UserAccounts_RequestedByUserAccountId",
                table: "RewardRedemptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Rewards_HouseholdMembers_HouseholdId_CreatedByMemberId",
                table: "Rewards");

            migrationBuilder.DropForeignKey(
                name: "FK_Rewards_HouseholdMembers_HouseholdId_UpdatedByMemberId",
                table: "Rewards");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Rewards_HouseholdId_Id",
                table: "Rewards");

            migrationBuilder.DropIndex(
                name: "IX_Rewards_HouseholdId_ClientRequestId",
                table: "Rewards");

            migrationBuilder.DropIndex(
                name: "IX_Rewards_HouseholdId_CreatedByMemberId",
                table: "Rewards");

            migrationBuilder.DropIndex(
                name: "IX_Rewards_HouseholdId_UpdatedByMemberId",
                table: "Rewards");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rewards_ClientRequestId",
                table: "Rewards");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rewards_PointCost",
                table: "Rewards");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rewards_Version",
                table: "Rewards");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_RewardRedemptions_HouseholdId_Id",
                table: "RewardRedemptions");

            migrationBuilder.DropIndex(
                name: "IX_RewardRedemptions_HouseholdId_CancelledByMemberId",
                table: "RewardRedemptions");

            migrationBuilder.DropIndex(
                name: "IX_RewardRedemptions_HouseholdId_ClientRequestId",
                table: "RewardRedemptions");

            migrationBuilder.DropIndex(
                name: "IX_RewardRedemptions_HouseholdId_FulfilledByMemberId",
                table: "RewardRedemptions");

            migrationBuilder.DropIndex(
                name: "IX_RewardRedemptions_HouseholdId_HouseholdMemberId_RequestedAt",
                table: "RewardRedemptions");

            migrationBuilder.DropIndex(
                name: "IX_RewardRedemptions_HouseholdId_RequestedByMemberId",
                table: "RewardRedemptions");

            migrationBuilder.DropIndex(
                name: "IX_RewardRedemptions_HouseholdId_ReviewedByMemberId",
                table: "RewardRedemptions");

            migrationBuilder.DropIndex(
                name: "IX_RewardRedemptions_HouseholdId_RewardId",
                table: "RewardRedemptions");

            migrationBuilder.DropIndex(
                name: "IX_RewardRedemptions_HouseholdId_Status_RequestedAt",
                table: "RewardRedemptions");

            migrationBuilder.DropIndex(
                name: "IX_RewardRedemptions_RequestedByUserAccountId",
                table: "RewardRedemptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RewardRedemptions_ClientRequestId",
                table: "RewardRedemptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RewardRedemptions_PointCostSnapshot",
                table: "RewardRedemptions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RewardRedemptions_Version",
                table: "RewardRedemptions");

            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_HouseholdId_RewardRedemptionId",
                table: "PointTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PointTransactions_RewardRedemptionLink",
                table: "PointTransactions");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "Rewards");

            migrationBuilder.DropColumn(
                name: "CreatedByMemberId",
                table: "Rewards");

            migrationBuilder.DropColumn(
                name: "UpdatedByMemberId",
                table: "Rewards");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Rewards");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "RewardRedemptions");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "RewardRedemptions");

            migrationBuilder.DropColumn(
                name: "CancelledByMemberId",
                table: "RewardRedemptions");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "RewardRedemptions");

            migrationBuilder.DropColumn(
                name: "FulfilledAt",
                table: "RewardRedemptions");

            migrationBuilder.DropColumn(
                name: "FulfilledByMemberId",
                table: "RewardRedemptions");

            migrationBuilder.DropColumn(
                name: "HouseholdId",
                table: "RewardRedemptions");

            migrationBuilder.DropColumn(
                name: "RequestedByMemberId",
                table: "RewardRedemptions");

            migrationBuilder.DropColumn(
                name: "RequestedByUserAccountId",
                table: "RewardRedemptions");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "RewardRedemptions");

            migrationBuilder.DropColumn(
                name: "RewardDescriptionSnapshot",
                table: "RewardRedemptions");

            migrationBuilder.DropColumn(
                name: "RewardTitleSnapshot",
                table: "RewardRedemptions");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "RewardRedemptions");

            migrationBuilder.DropColumn(
                name: "WasSharedDisplay",
                table: "RewardRedemptions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rewards_PointCost",
                table: "Rewards",
                sql: "\"PointCost\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_RewardRedemptions_HouseholdMemberId_RequestedAt",
                table: "RewardRedemptions",
                columns: new[] { "HouseholdMemberId", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RewardRedemptions_ReviewedByMemberId",
                table: "RewardRedemptions",
                column: "ReviewedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_RewardRedemptions_RewardId",
                table: "RewardRedemptions",
                column: "RewardId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RewardRedemptions_PointCostSnapshot",
                table: "RewardRedemptions",
                sql: "\"PointCostSnapshot\" > 0");

            migrationBuilder.AddForeignKey(
                name: "FK_PointTransactions_RewardRedemptions_RewardRedemptionId",
                table: "PointTransactions",
                column: "RewardRedemptionId",
                principalTable: "RewardRedemptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RewardRedemptions_HouseholdMembers_HouseholdMemberId",
                table: "RewardRedemptions",
                column: "HouseholdMemberId",
                principalTable: "HouseholdMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RewardRedemptions_HouseholdMembers_ReviewedByMemberId",
                table: "RewardRedemptions",
                column: "ReviewedByMemberId",
                principalTable: "HouseholdMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RewardRedemptions_Rewards_RewardId",
                table: "RewardRedemptions",
                column: "RewardId",
                principalTable: "Rewards",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
