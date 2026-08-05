using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCoreSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Households",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Households", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChoreDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DefaultPointValue = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChoreDefinitions", x => x.Id);
                    table.CheckConstraint("CK_ChoreDefinitions_DefaultPointValue", "\"DefaultPointValue\" >= 0");
                    table.ForeignKey(
                        name: "FK_ChoreDefinitions_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HouseholdConfigurations",
                columns: table => new
                {
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WeekStartsOn = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Theme = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdConfigurations", x => x.HouseholdId);
                    table.ForeignKey(
                        name: "FK_HouseholdConfigurations_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HouseholdMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AvatarColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdMembers_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Rewards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PointCost = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rewards", x => x.Id);
                    table.CheckConstraint("CK_Rewards_PointCost", "\"PointCost\" > 0");
                    table.ForeignKey(
                        name: "FK_Rewards_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ValueJson = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationPreferences_HouseholdMembers_HouseholdMemberId",
                        column: x => x.HouseholdMemberId,
                        principalTable: "HouseholdMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApplicationPreferences_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChoreAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChoreDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChoreAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChoreAssignments_ChoreDefinitions_ChoreDefinitionId",
                        column: x => x.ChoreDefinitionId,
                        principalTable: "ChoreDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChoreAssignments_HouseholdMembers_HouseholdMemberId",
                        column: x => x.HouseholdMemberId,
                        principalTable: "HouseholdMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RewardRedemptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RewardId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    PointCostSnapshot = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedByMemberId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardRedemptions", x => x.Id);
                    table.CheckConstraint("CK_RewardRedemptions_PointCostSnapshot", "\"PointCostSnapshot\" > 0");
                    table.ForeignKey(
                        name: "FK_RewardRedemptions_HouseholdMembers_HouseholdMemberId",
                        column: x => x.HouseholdMemberId,
                        principalTable: "HouseholdMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RewardRedemptions_HouseholdMembers_ReviewedByMemberId",
                        column: x => x.ReviewedByMemberId,
                        principalTable: "HouseholdMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RewardRedemptions_Rewards_RewardId",
                        column: x => x.RewardId,
                        principalTable: "Rewards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChoreCompletions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChoreAssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedByMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChoreCompletions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChoreCompletions_ChoreAssignments_ChoreAssignmentId",
                        column: x => x.ChoreAssignmentId,
                        principalTable: "ChoreAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChoreCompletions_HouseholdMembers_CompletedByMemberId",
                        column: x => x.CompletedByMemberId,
                        principalTable: "HouseholdMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChoreCompletions_HouseholdMembers_ReviewedByMemberId",
                        column: x => x.ReviewedByMemberId,
                        principalTable: "HouseholdMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PointTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ChoreCompletionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RewardRedemptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointTransactions", x => x.Id);
                    table.CheckConstraint("CK_PointTransactions_Amount", "\"Amount\" <> 0");
                    table.ForeignKey(
                        name: "FK_PointTransactions_ChoreCompletions_ChoreCompletionId",
                        column: x => x.ChoreCompletionId,
                        principalTable: "ChoreCompletions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PointTransactions_HouseholdMembers_HouseholdMemberId",
                        column: x => x.HouseholdMemberId,
                        principalTable: "HouseholdMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PointTransactions_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PointTransactions_RewardRedemptions_RewardRedemptionId",
                        column: x => x.RewardRedemptionId,
                        principalTable: "RewardRedemptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationPreferences_HouseholdId_Key",
                table: "ApplicationPreferences",
                columns: new[] { "HouseholdId", "Key" },
                unique: true,
                filter: "\"HouseholdMemberId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationPreferences_HouseholdMemberId_Key",
                table: "ApplicationPreferences",
                columns: new[] { "HouseholdMemberId", "Key" },
                unique: true,
                filter: "\"HouseholdMemberId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreAssignments_ChoreDefinitionId",
                table: "ChoreAssignments",
                column: "ChoreDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreAssignments_HouseholdMemberId_DueAt",
                table: "ChoreAssignments",
                columns: new[] { "HouseholdMemberId", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChoreCompletions_ChoreAssignmentId",
                table: "ChoreCompletions",
                column: "ChoreAssignmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChoreCompletions_CompletedByMemberId",
                table: "ChoreCompletions",
                column: "CompletedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreCompletions_ReviewedByMemberId",
                table: "ChoreCompletions",
                column: "ReviewedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ChoreDefinitions_HouseholdId_IsActive",
                table: "ChoreDefinitions",
                columns: new[] { "HouseholdId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdMembers_HouseholdId_DisplayName",
                table: "HouseholdMembers",
                columns: new[] { "HouseholdId", "DisplayName" });

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_ChoreCompletionId",
                table: "PointTransactions",
                column: "ChoreCompletionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_HouseholdId",
                table: "PointTransactions",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_HouseholdMemberId_CreatedAt",
                table: "PointTransactions",
                columns: new[] { "HouseholdMemberId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_IdempotencyKey",
                table: "PointTransactions",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_RewardRedemptionId",
                table: "PointTransactions",
                column: "RewardRedemptionId",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_Rewards_HouseholdId_IsActive",
                table: "Rewards",
                columns: new[] { "HouseholdId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationPreferences");

            migrationBuilder.DropTable(
                name: "HouseholdConfigurations");

            migrationBuilder.DropTable(
                name: "PointTransactions");

            migrationBuilder.DropTable(
                name: "ChoreCompletions");

            migrationBuilder.DropTable(
                name: "RewardRedemptions");

            migrationBuilder.DropTable(
                name: "ChoreAssignments");

            migrationBuilder.DropTable(
                name: "Rewards");

            migrationBuilder.DropTable(
                name: "ChoreDefinitions");

            migrationBuilder.DropTable(
                name: "HouseholdMembers");

            migrationBuilder.DropTable(
                name: "Households");
        }
    }
}
