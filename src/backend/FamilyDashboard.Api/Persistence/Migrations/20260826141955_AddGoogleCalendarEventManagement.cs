using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleCalendarEventManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CalendarEventMutationReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    CalendarEventCreationReceiptId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdCalendarSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RequestedByUserAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActingHouseholdMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedFromSharedDisplay = table.Column<bool>(type: "boolean", nullable: false),
                    RequestFingerprint = table.Column<byte[]>(type: "bytea", nullable: false),
                    ExpectedProviderVersion = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ResultProviderVersion = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarEventMutationReceipts", x => new { x.HouseholdId, x.Id });
                    table.CheckConstraint("CK_CalendarEventMutationReceipts_Completion", "(\"Status\" = 'Pending' AND \"CompletedAt\" IS NULL) OR (\"Status\" = 'Succeeded' AND \"CompletedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_CalendarEventMutationReceipts_Fingerprint", "octet_length(\"RequestFingerprint\") = 32");
                    table.ForeignKey(
                        name: "FK_CalendarEventMutationReceipts_CalendarEventCreationReceipts~",
                        columns: x => new { x.HouseholdId, x.CalendarEventCreationReceiptId },
                        principalTable: "CalendarEventCreationReceipts",
                        principalColumns: new[] { "HouseholdId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalendarEventMutationReceipts_HouseholdCalendarSources_Hous~",
                        columns: x => new { x.HouseholdId, x.HouseholdCalendarSourceId },
                        principalTable: "HouseholdCalendarSources",
                        principalColumns: new[] { "HouseholdId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalendarEventMutationReceipts_HouseholdMembers_HouseholdId_~",
                        columns: x => new { x.HouseholdId, x.ActingHouseholdMemberId },
                        principalTable: "HouseholdMembers",
                        principalColumns: new[] { "HouseholdId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalendarEventMutationReceipts_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CalendarEventMutationReceipts_UserAccounts_RequestedByUserA~",
                        column: x => x.RequestedByUserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventMutationReceipts_HouseholdId_ActingHouseholdMe~",
                table: "CalendarEventMutationReceipts",
                columns: new[] { "HouseholdId", "ActingHouseholdMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventMutationReceipts_HouseholdId_CalendarEventCrea~",
                table: "CalendarEventMutationReceipts",
                columns: new[] { "HouseholdId", "CalendarEventCreationReceiptId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventMutationReceipts_HouseholdId_HouseholdCalendar~",
                table: "CalendarEventMutationReceipts",
                columns: new[] { "HouseholdId", "HouseholdCalendarSourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventMutationReceipts_RequestedByUserAccountId",
                table: "CalendarEventMutationReceipts",
                column: "RequestedByUserAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CalendarEventMutationReceipts");
        }
    }
}
