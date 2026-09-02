using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowSharedGoogleTaskStatusActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "AttributedHouseholdMemberId",
                table: "GoogleTaskMutationReceipts",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "GoogleTaskMutationReceipts" AS receipt
                SET "AttributedHouseholdMemberId" = membership."HouseholdMemberId"
                FROM "HouseholdMemberships" AS membership
                WHERE receipt."AttributedHouseholdMemberId" IS NULL
                  AND membership."HouseholdId" = receipt."HouseholdId"
                  AND membership."UserAccountId" = receipt."RequestedByUserAccountId";
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "AttributedHouseholdMemberId",
                table: "GoogleTaskMutationReceipts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
