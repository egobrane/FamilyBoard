using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSelectedHouseholdToUserSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SelectedHouseholdId",
                table: "UserSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_UserAccountId_SelectedHouseholdId",
                table: "UserSessions",
                columns: new[] { "UserAccountId", "SelectedHouseholdId" });

            migrationBuilder.AddForeignKey(
                name: "FK_UserSessions_HouseholdMemberships_UserAccountId_SelectedHou~",
                table: "UserSessions",
                columns: new[] { "UserAccountId", "SelectedHouseholdId" },
                principalTable: "HouseholdMemberships",
                principalColumns: new[] { "UserAccountId", "HouseholdId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserSessions_HouseholdMemberships_UserAccountId_SelectedHou~",
                table: "UserSessions");

            migrationBuilder.DropIndex(
                name: "IX_UserSessions_UserAccountId_SelectedHouseholdId",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "SelectedHouseholdId",
                table: "UserSessions");
        }
    }
}
