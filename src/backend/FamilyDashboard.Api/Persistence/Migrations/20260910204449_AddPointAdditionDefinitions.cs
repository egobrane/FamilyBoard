using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPointAdditionDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PointAdditionDefinitionId",
                table: "PointTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PointAdditionDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AdditionAmount = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointAdditionDefinitions", x => x.Id);
                    table.UniqueConstraint("AK_PointAdditionDefinitions_HouseholdId_Id", x => new { x.HouseholdId, x.Id });
                    table.CheckConstraint("CK_PointAdditionDefinitions_Amount", "\"AdditionAmount\" BETWEEN 1 AND 10000");
                    table.CheckConstraint("CK_PointAdditionDefinitions_ClientRequestId", "\"ClientRequestId\" <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_PointAdditionDefinitions_Version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_PointAdditionDefinitions_HouseholdMembers_HouseholdId_Creat~",
                        columns: x => new { x.HouseholdId, x.CreatedByMemberId },
                        principalTable: "HouseholdMembers",
                        principalColumns: new[] { "HouseholdId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PointAdditionDefinitions_HouseholdMembers_HouseholdId_Updat~",
                        columns: x => new { x.HouseholdId, x.UpdatedByMemberId },
                        principalTable: "HouseholdMembers",
                        principalColumns: new[] { "HouseholdId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PointAdditionDefinitions_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_HouseholdId_PointAdditionDefinitionId",
                table: "PointTransactions",
                columns: new[] { "HouseholdId", "PointAdditionDefinitionId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PointTransactions_AdditionDefinitionLink",
                table: "PointTransactions",
                sql: "\"PointAdditionDefinitionId\" IS NULL OR (\"Type\" = 'Adjustment' AND \"Amount\" > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_PointAdditionDefinitions_HouseholdId_ClientRequestId",
                table: "PointAdditionDefinitions",
                columns: new[] { "HouseholdId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointAdditionDefinitions_HouseholdId_CreatedByMemberId",
                table: "PointAdditionDefinitions",
                columns: new[] { "HouseholdId", "CreatedByMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_PointAdditionDefinitions_HouseholdId_IsActive_Title",
                table: "PointAdditionDefinitions",
                columns: new[] { "HouseholdId", "IsActive", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_PointAdditionDefinitions_HouseholdId_UpdatedByMemberId",
                table: "PointAdditionDefinitions",
                columns: new[] { "HouseholdId", "UpdatedByMemberId" });

            migrationBuilder.AddForeignKey(
                name: "FK_PointTransactions_PointAdditionDefinitions_HouseholdId_Poin~",
                table: "PointTransactions",
                columns: new[] { "HouseholdId", "PointAdditionDefinitionId" },
                principalTable: "PointAdditionDefinitions",
                principalColumns: new[] { "HouseholdId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PointTransactions_PointAdditionDefinitions_HouseholdId_Poin~",
                table: "PointTransactions");

            migrationBuilder.DropTable(
                name: "PointAdditionDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_HouseholdId_PointAdditionDefinitionId",
                table: "PointTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PointTransactions_AdditionDefinitionLink",
                table: "PointTransactions");

            migrationBuilder.DropColumn(
                name: "PointAdditionDefinitionId",
                table: "PointTransactions");
        }
    }
}
