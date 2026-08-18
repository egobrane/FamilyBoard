using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleCalendarReadOnlyIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoogleCalendarConnections",
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
                    table.PrimaryKey("PK_GoogleCalendarConnections", x => x.Id);
                    table.UniqueConstraint("AK_GoogleCalendarConnections_Id_UserAccountId", x => new { x.Id, x.UserAccountId });
                    table.CheckConstraint("CK_GoogleCalendarConnections_Tokens", "(\"Status\" = 'Active' AND \"ProtectedRefreshToken\" IS NOT NULL) OR \"Status\" <> 'Active'");
                    table.ForeignKey(
                        name: "FK_GoogleCalendarConnections_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HouseholdCalendarSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    GoogleCalendarConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalCalendarId = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    DisplayNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Color = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AddedByUserAccountId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdCalendarSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdCalendarSources_GoogleCalendarConnections_GoogleCa~",
                        columns: x => new { x.GoogleCalendarConnectionId, x.OwnerUserAccountId },
                        principalTable: "GoogleCalendarConnections",
                        principalColumns: new[] { "Id", "UserAccountId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HouseholdCalendarSources_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HouseholdCalendarSources_UserAccounts_AddedByUserAccountId",
                        column: x => x.AddedByUserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoogleCalendarConnections_ProviderSubject_Status",
                table: "GoogleCalendarConnections",
                columns: new[] { "ProviderSubject", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GoogleCalendarConnections_UserAccountId",
                table: "GoogleCalendarConnections",
                column: "UserAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdCalendarSources_AddedByUserAccountId",
                table: "HouseholdCalendarSources",
                column: "AddedByUserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdCalendarSources_GoogleCalendarConnectionId_OwnerUs~",
                table: "HouseholdCalendarSources",
                columns: new[] { "GoogleCalendarConnectionId", "OwnerUserAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdCalendarSources_HouseholdId_GoogleCalendarConnecti~",
                table: "HouseholdCalendarSources",
                columns: new[] { "HouseholdId", "GoogleCalendarConnectionId", "ExternalCalendarId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdCalendarSources_HouseholdId_IsActive",
                table: "HouseholdCalendarSources",
                columns: new[] { "HouseholdId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HouseholdCalendarSources");

            migrationBuilder.DropTable(
                name: "GoogleCalendarConnections");
        }
    }
}
