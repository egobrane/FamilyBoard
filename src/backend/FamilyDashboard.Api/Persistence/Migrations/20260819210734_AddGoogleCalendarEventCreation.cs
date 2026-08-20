using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleCalendarEventCreation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EventCreationEnabledAt",
                table: "HouseholdCalendarSources",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EventCreationEnabledByUserAccountId",
                table: "HouseholdCalendarSources",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEventCreationTarget",
                table: "HouseholdCalendarSources",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_HouseholdCalendarSources_HouseholdId_Id",
                table: "HouseholdCalendarSources",
                columns: new[] { "HouseholdId", "Id" });

            migrationBuilder.CreateTable(
                name: "CalendarEventCreationReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdCalendarSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedByUserAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributedHouseholdMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedFromSharedDisplay = table.Column<bool>(type: "boolean", nullable: false),
                    RequestFingerprint = table.Column<byte[]>(type: "bytea", nullable: false),
                    ProviderEventId = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEventCreationReceipts", x => new { x.HouseholdId, x.Id });
                    table.CheckConstraint("CK_CalendarEventCreationReceipts_Completion", "(\"Status\" = 'Pending' AND \"ProviderEventId\" IS NULL AND \"CompletedAt\" IS NULL) OR (\"Status\" = 'Succeeded' AND \"ProviderEventId\" IS NOT NULL AND \"CompletedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_CalendarEventCreationReceipts_Fingerprint", "octet_length(\"RequestFingerprint\") = 32");
                    table.ForeignKey(
                        name: "FK_CalendarEventCreationReceipts_HouseholdCalendarSources_Hous~",
                        columns: x => new { x.HouseholdId, x.HouseholdCalendarSourceId },
                        principalTable: "HouseholdCalendarSources",
                        principalColumns: new[] { "HouseholdId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalendarEventCreationReceipts_HouseholdMembers_HouseholdId_~",
                        columns: x => new { x.HouseholdId, x.AttributedHouseholdMemberId },
                        principalTable: "HouseholdMembers",
                        principalColumns: new[] { "HouseholdId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalendarEventCreationReceipts_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalendarEventCreationReceipts_UserAccounts_RequestedByUserA~",
                        column: x => x.RequestedByUserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdCalendarSources_EventCreationEnabledByUserAccountId",
                table: "HouseholdCalendarSources",
                column: "EventCreationEnabledByUserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdCalendarSources_HouseholdId",
                table: "HouseholdCalendarSources",
                column: "HouseholdId",
                unique: true,
                filter: "\"IsActive\" AND \"IsEventCreationTarget\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HouseholdCalendarSources_EventCreationTarget",
                table: "HouseholdCalendarSources",
                sql: "(\"IsEventCreationTarget\" AND \"IsActive\" AND \"EventCreationEnabledAt\" IS NOT NULL AND \"EventCreationEnabledByUserAccountId\" IS NOT NULL) OR (NOT \"IsEventCreationTarget\" AND \"EventCreationEnabledAt\" IS NULL AND \"EventCreationEnabledByUserAccountId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventCreationReceipts_HouseholdCalendarSourceId_Pro~",
                table: "CalendarEventCreationReceipts",
                columns: new[] { "HouseholdCalendarSourceId", "ProviderEventId" },
                unique: true,
                filter: "\"ProviderEventId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventCreationReceipts_HouseholdId_AttributedHouseho~",
                table: "CalendarEventCreationReceipts",
                columns: new[] { "HouseholdId", "AttributedHouseholdMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventCreationReceipts_HouseholdId_HouseholdCalendar~",
                table: "CalendarEventCreationReceipts",
                columns: new[] { "HouseholdId", "HouseholdCalendarSourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventCreationReceipts_HouseholdId_Status_CreatedAt",
                table: "CalendarEventCreationReceipts",
                columns: new[] { "HouseholdId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventCreationReceipts_RequestedByUserAccountId",
                table: "CalendarEventCreationReceipts",
                column: "RequestedByUserAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_HouseholdCalendarSources_UserAccounts_EventCreationEnabledB~",
                table: "HouseholdCalendarSources",
                column: "EventCreationEnabledByUserAccountId",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HouseholdCalendarSources_UserAccounts_EventCreationEnabledB~",
                table: "HouseholdCalendarSources");

            migrationBuilder.DropTable(
                name: "CalendarEventCreationReceipts");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_HouseholdCalendarSources_HouseholdId_Id",
                table: "HouseholdCalendarSources");

            migrationBuilder.DropIndex(
                name: "IX_HouseholdCalendarSources_EventCreationEnabledByUserAccountId",
                table: "HouseholdCalendarSources");

            migrationBuilder.DropIndex(
                name: "IX_HouseholdCalendarSources_HouseholdId",
                table: "HouseholdCalendarSources");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HouseholdCalendarSources_EventCreationTarget",
                table: "HouseholdCalendarSources");

            migrationBuilder.DropColumn(
                name: "EventCreationEnabledAt",
                table: "HouseholdCalendarSources");

            migrationBuilder.DropColumn(
                name: "EventCreationEnabledByUserAccountId",
                table: "HouseholdCalendarSources");

            migrationBuilder.DropColumn(
                name: "IsEventCreationTarget",
                table: "HouseholdCalendarSources");
        }
    }
}
