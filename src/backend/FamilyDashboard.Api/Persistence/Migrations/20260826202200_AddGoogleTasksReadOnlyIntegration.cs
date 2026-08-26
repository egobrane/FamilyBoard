using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleTasksReadOnlyIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoogleTasksConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderSubject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ProviderEmailNormalized = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    ProtectedAccessToken = table.Column<string>(type: "text", nullable: true),
                    ProtectedRefreshToken = table.Column<string>(type: "text", nullable: true),
                    AccessTokenExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GrantedScopes = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ConnectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSuccessfulRefreshAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoogleTasksConnections", x => x.Id);
                    table.UniqueConstraint("AK_GoogleTasksConnections_Id_UserAccountId", x => new { x.Id, x.UserAccountId });
                    table.CheckConstraint("CK_GoogleTasksConnections_Tokens", "(\"Status\" = 'Active' AND \"ProtectedRefreshToken\" IS NOT NULL) OR \"Status\" <> 'Active'");
                    table.ForeignKey(
                        name: "FK_GoogleTasksConnections_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HouseholdTaskListSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    GoogleTasksConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalTaskListId = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    DisplayNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AddedByUserAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdTaskListSources", x => x.Id);
                    table.UniqueConstraint("AK_HouseholdTaskListSources_HouseholdId_Id", x => new { x.HouseholdId, x.Id });
                    table.ForeignKey(
                        name: "FK_HouseholdTaskListSources_GoogleTasksConnections_GoogleTasks~",
                        columns: x => new { x.GoogleTasksConnectionId, x.OwnerUserAccountId },
                        principalTable: "GoogleTasksConnections",
                        principalColumns: new[] { "Id", "UserAccountId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HouseholdTaskListSources_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HouseholdTaskListSources_UserAccounts_AddedByUserAccountId",
                        column: x => x.AddedByUserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoogleTasksConnections_ProviderSubject_Status",
                table: "GoogleTasksConnections",
                columns: new[] { "ProviderSubject", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GoogleTasksConnections_UserAccountId",
                table: "GoogleTasksConnections",
                column: "UserAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdTaskListSources_AddedByUserAccountId",
                table: "HouseholdTaskListSources",
                column: "AddedByUserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdTaskListSources_GoogleTasksConnectionId_OwnerUserA~",
                table: "HouseholdTaskListSources",
                columns: new[] { "GoogleTasksConnectionId", "OwnerUserAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdTaskListSources_HouseholdId_GoogleTasksConnectionI~",
                table: "HouseholdTaskListSources",
                columns: new[] { "HouseholdId", "GoogleTasksConnectionId", "ExternalTaskListId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdTaskListSources_HouseholdId_IsActive",
                table: "HouseholdTaskListSources",
                columns: new[] { "HouseholdId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HouseholdTaskListSources");

            migrationBuilder.DropTable(
                name: "GoogleTasksConnections");
        }
    }
}
