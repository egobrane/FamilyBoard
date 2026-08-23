using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringChoreSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ChoreScheduleId",
                table: "ChoreAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DueTimeResolution",
                table: "ChoreAssignments",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "Exact");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GeneratedAt",
                table: "ChoreAssignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ScheduleOccurrenceLocalDate",
                table: "ChoreAssignments",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChoreSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChoreDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecurrenceKind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Interval = table.Column<int>(type: "integer", nullable: false),
                    DaysOfWeekMask = table.Column<int>(type: "integer", nullable: true),
                    StartLocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndLocalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DueLocalTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    BlockedReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    NextOccurrenceLocalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LastGeneratedOccurrenceLocalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LastEvaluatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PausedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChoreSchedules", x => x.Id);
                    table.UniqueConstraint("AK_ChoreSchedules_HouseholdId_Id", x => new { x.HouseholdId, x.Id });
                    table.CheckConstraint("CK_ChoreSchedules_DateRange", "\"EndLocalDate\" IS NULL OR \"EndLocalDate\" >= \"StartLocalDate\"");
                    table.CheckConstraint("CK_ChoreSchedules_Interval", "\"Interval\" >= 1");
                    table.CheckConstraint("CK_ChoreSchedules_WeekdayMask", "(\"RecurrenceKind\" = 'Daily' AND \"DaysOfWeekMask\" IS NULL) OR (\"RecurrenceKind\" = 'Weekly' AND \"DaysOfWeekMask\" BETWEEN 1 AND 127)");
                    table.ForeignKey(
                        name: "FK_ChoreSchedules_ChoreDefinitions_HouseholdId_ChoreDefinition~",
                        columns: x => new { x.HouseholdId, x.ChoreDefinitionId },
                        principalTable: "ChoreDefinitions",
                        principalColumns: new[] { "HouseholdId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChoreSchedules_HouseholdMembers_HouseholdId_CreatedByMember~",
                        columns: x => new { x.HouseholdId, x.CreatedByMemberId },
                        principalTable: "HouseholdMembers",
                        principalColumns: new[] { "HouseholdId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChoreSchedules_HouseholdMembers_HouseholdId_HouseholdMember~",
                        columns: x => new { x.HouseholdId, x.HouseholdMemberId },
                        principalTable: "HouseholdMembers",
                        principalColumns: new[] { "HouseholdId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChoreSchedules_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChoreAssignments_HouseholdId_ChoreScheduleId_ScheduleOccurr~",
                table: "ChoreAssignments",
                columns: new[] { "HouseholdId", "ChoreScheduleId", "ScheduleOccurrenceLocalDate" },
                unique: true,
                filter: "\"ChoreScheduleId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreSchedules_HouseholdId_ChoreDefinitionId",
                table: "ChoreSchedules",
                columns: new[] { "HouseholdId", "ChoreDefinitionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChoreSchedules_HouseholdId_ClientRequestId",
                table: "ChoreSchedules",
                columns: new[] { "HouseholdId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChoreSchedules_HouseholdId_CreatedByMemberId",
                table: "ChoreSchedules",
                columns: new[] { "HouseholdId", "CreatedByMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChoreSchedules_HouseholdId_HouseholdMemberId",
                table: "ChoreSchedules",
                columns: new[] { "HouseholdId", "HouseholdMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_ChoreSchedules_Status_NextOccurrenceLocalDate",
                table: "ChoreSchedules",
                columns: new[] { "Status", "NextOccurrenceLocalDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_ChoreAssignments_ChoreSchedules_HouseholdId_ChoreScheduleId",
                table: "ChoreAssignments",
                columns: new[] { "HouseholdId", "ChoreScheduleId" },
                principalTable: "ChoreSchedules",
                principalColumns: new[] { "HouseholdId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChoreAssignments_ChoreSchedules_HouseholdId_ChoreScheduleId",
                table: "ChoreAssignments");

            migrationBuilder.DropTable(
                name: "ChoreSchedules");

            migrationBuilder.DropIndex(
                name: "IX_ChoreAssignments_HouseholdId_ChoreScheduleId_ScheduleOccurr~",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "ChoreScheduleId",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "DueTimeResolution",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "GeneratedAt",
                table: "ChoreAssignments");

            migrationBuilder.DropColumn(
                name: "ScheduleOccurrenceLocalDate",
                table: "ChoreAssignments");
        }
    }
}
