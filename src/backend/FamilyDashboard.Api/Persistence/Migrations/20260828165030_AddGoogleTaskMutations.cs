using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleTaskMutations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsWriteTarget",
                table: "HouseholdTaskListSources",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "WriteTargetConfiguredAt",
                table: "HouseholdTaskListSources",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WriteTargetConfiguredByUserAccountId",
                table: "HouseholdTaskListSources",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GoogleTaskMutationReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdTaskListSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    GoogleTasksConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    RequestFingerprint = table.Column<byte[]>(type: "bytea", nullable: false),
                    ProviderTaskId = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ResultProviderETag = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    RequestedByUserAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributedHouseholdMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedFromSharedDisplay = table.Column<bool>(type: "boolean", nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleTaskMutationReceipts", x => new { x.HouseholdId, x.Id });
                    table.CheckConstraint("CK_GoogleTaskMutationReceipts_Completion", "(\"Status\" = 'Pending' AND \"CompletedAt\" IS NULL) OR (\"Status\" <> 'Pending' AND \"CompletedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_GoogleTaskMutationReceipts_Fingerprint", "octet_length(\"RequestFingerprint\") = 32");
                    table.ForeignKey(
                        name: "FK_GoogleTaskMutationReceipts_GoogleTasksConnections_GoogleTas~",
                        column: x => x.GoogleTasksConnectionId,
                        principalTable: "GoogleTasksConnections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoogleTaskMutationReceipts_HouseholdMembers_HouseholdId_Att~",
                        columns: x => new { x.HouseholdId, x.AttributedHouseholdMemberId },
                        principalTable: "HouseholdMembers",
                        principalColumns: new[] { "HouseholdId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoogleTaskMutationReceipts_HouseholdTaskListSources_Househo~",
                        columns: x => new { x.HouseholdId, x.HouseholdTaskListSourceId },
                        principalTable: "HouseholdTaskListSources",
                        principalColumns: new[] { "HouseholdId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoogleTaskMutationReceipts_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoogleTaskMutationReceipts_UserAccounts_RequestedByUserAcco~",
                        column: x => x.RequestedByUserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdTaskListSources_GoogleTasksConnectionId_ExternalTa~",
                table: "HouseholdTaskListSources",
                columns: new[] { "GoogleTasksConnectionId", "ExternalTaskListId" },
                unique: true,
                filter: "\"IsWriteTarget\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdTaskListSources_HouseholdId",
                table: "HouseholdTaskListSources",
                column: "HouseholdId",
                unique: true,
                filter: "\"IsWriteTarget\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdTaskListSources_WriteTargetConfiguredByUserAccount~",
                table: "HouseholdTaskListSources",
                column: "WriteTargetConfiguredByUserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_GoogleTaskMutationReceipts_GoogleTasksConnectionId",
                table: "GoogleTaskMutationReceipts",
                column: "GoogleTasksConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_GoogleTaskMutationReceipts_HouseholdId_AttributedHouseholdM~",
                table: "GoogleTaskMutationReceipts",
                columns: new[] { "HouseholdId", "AttributedHouseholdMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_GoogleTaskMutationReceipts_HouseholdId_HouseholdTaskListSou~",
                table: "GoogleTaskMutationReceipts",
                columns: new[] { "HouseholdId", "HouseholdTaskListSourceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GoogleTaskMutationReceipts_RequestedByUserAccountId",
                table: "GoogleTaskMutationReceipts",
                column: "RequestedByUserAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_HouseholdTaskListSources_UserAccounts_WriteTargetConfigured~",
                table: "HouseholdTaskListSources",
                column: "WriteTargetConfiguredByUserAccountId",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HouseholdTaskListSources_UserAccounts_WriteTargetConfigured~",
                table: "HouseholdTaskListSources");

            migrationBuilder.DropTable(
                name: "GoogleTaskMutationReceipts");

            migrationBuilder.DropIndex(
                name: "IX_HouseholdTaskListSources_GoogleTasksConnectionId_ExternalTa~",
                table: "HouseholdTaskListSources");

            migrationBuilder.DropIndex(
                name: "IX_HouseholdTaskListSources_HouseholdId",
                table: "HouseholdTaskListSources");

            migrationBuilder.DropIndex(
                name: "IX_HouseholdTaskListSources_WriteTargetConfiguredByUserAccount~",
                table: "HouseholdTaskListSources");

            migrationBuilder.DropColumn(
                name: "IsWriteTarget",
                table: "HouseholdTaskListSources");

            migrationBuilder.DropColumn(
                name: "WriteTargetConfiguredAt",
                table: "HouseholdTaskListSources");

            migrationBuilder.DropColumn(
                name: "WriteTargetConfiguredByUserAccountId",
                table: "HouseholdTaskListSources");
        }
    }
}
