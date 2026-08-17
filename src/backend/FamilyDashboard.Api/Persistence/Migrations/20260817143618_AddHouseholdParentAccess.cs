using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdParentAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AdministrativeElevationHouseholdId",
                table: "UserSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentAccessFailedAttemptCount",
                table: "UserSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ParentAccessFailureWindowStartedAt",
                table: "UserSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ParentAccessLockedUntil",
                table: "UserSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HouseholdAccessPins",
                columns: table => new
                {
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    PinHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    Salt = table.Column<byte[]>(type: "bytea", nullable: false),
                    HashVersion = table.Column<short>(type: "smallint", nullable: false),
                    WorkFactor = table.Column<int>(type: "integer", nullable: false),
                    PepperVersion = table.Column<short>(type: "smallint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChangedByUserAccountId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdAccessPins", x => x.HouseholdId);
                    table.CheckConstraint("CK_HouseholdAccessPins_ChangedAfterCreated", "\"ChangedAt\" >= \"CreatedAt\"");
                    table.CheckConstraint("CK_HouseholdAccessPins_HashLength", "octet_length(\"PinHash\") = 32");
                    table.CheckConstraint("CK_HouseholdAccessPins_SaltLength", "octet_length(\"Salt\") = 16");
                    table.CheckConstraint("CK_HouseholdAccessPins_Versions", "\"HashVersion\" > 0 AND \"PepperVersion\" > 0 AND \"WorkFactor\" > 0");
                    table.ForeignKey(
                        name: "FK_HouseholdAccessPins_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HouseholdAccessPins_UserAccounts_ChangedByUserAccountId",
                        column: x => x.ChangedByUserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParentAccessAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    TraceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CooldownUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentAccessAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentAccessAuditEvents_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParentAccessAuditEvents_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParentAccessAuditEvents_UserSessions_UserSessionId",
                        column: x => x.UserSessionId,
                        principalTable: "UserSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_UserAccountId_AdministrativeElevationHousehold~",
                table: "UserSessions",
                columns: new[] { "UserAccountId", "AdministrativeElevationHouseholdId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserSessions_AdministrativeElevation",
                table: "UserSessions",
                sql: "(\"AdministrativeElevationHouseholdId\" IS NULL AND \"AdministrativeElevationExpiresAt\" IS NULL) OR (\"AdministrativeElevationHouseholdId\" IS NOT NULL AND \"AdministrativeElevationExpiresAt\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserSessions_ParentAccessFailures",
                table: "UserSessions",
                sql: "\"ParentAccessFailedAttemptCount\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdAccessPins_ChangedByUserAccountId",
                table: "HouseholdAccessPins",
                column: "ChangedByUserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentAccessAuditEvents_HouseholdId_OccurredAt",
                table: "ParentAccessAuditEvents",
                columns: new[] { "HouseholdId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ParentAccessAuditEvents_UserAccountId",
                table: "ParentAccessAuditEvents",
                column: "UserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ParentAccessAuditEvents_UserSessionId_OccurredAt",
                table: "ParentAccessAuditEvents",
                columns: new[] { "UserSessionId", "OccurredAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_UserSessions_HouseholdMemberships_UserAccountId_Administrat~",
                table: "UserSessions",
                columns: new[] { "UserAccountId", "AdministrativeElevationHouseholdId" },
                principalTable: "HouseholdMemberships",
                principalColumns: new[] { "UserAccountId", "HouseholdId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserSessions_HouseholdMemberships_UserAccountId_Administrat~",
                table: "UserSessions");

            migrationBuilder.DropTable(
                name: "HouseholdAccessPins");

            migrationBuilder.DropTable(
                name: "ParentAccessAuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_UserSessions_UserAccountId_AdministrativeElevationHousehold~",
                table: "UserSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserSessions_AdministrativeElevation",
                table: "UserSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserSessions_ParentAccessFailures",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "AdministrativeElevationHouseholdId",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "ParentAccessFailedAttemptCount",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "ParentAccessFailureWindowStartedAt",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "ParentAccessLockedUntil",
                table: "UserSessions");
        }
    }
}
