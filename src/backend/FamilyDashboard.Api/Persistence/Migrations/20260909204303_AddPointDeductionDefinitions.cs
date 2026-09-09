using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPointDeductionDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PointDeductionDefinitionId",
                table: "PointTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PointDeductionDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DeductionAmount = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointDeductionDefinitions", x => x.Id);
                    table.UniqueConstraint("AK_PointDeductionDefinitions_HouseholdId_Id", x => new { x.HouseholdId, x.Id });
                    table.CheckConstraint("CK_PointDeductionDefinitions_Amount", "\"DeductionAmount\" BETWEEN 1 AND 10000");
                    table.CheckConstraint("CK_PointDeductionDefinitions_ClientRequestId", "\"ClientRequestId\" <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_PointDeductionDefinitions_Version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_PointDeductionDefinitions_HouseholdMembers_HouseholdId_Crea~",
                        columns: x => new { x.HouseholdId, x.CreatedByMemberId },
                        principalTable: "HouseholdMembers",
                        principalColumns: new[] { "HouseholdId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PointDeductionDefinitions_HouseholdMembers_HouseholdId_Upda~",
                        columns: x => new { x.HouseholdId, x.UpdatedByMemberId },
                        principalTable: "HouseholdMembers",
                        principalColumns: new[] { "HouseholdId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PointDeductionDefinitions_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_HouseholdId_PointDeductionDefinitionId",
                table: "PointTransactions",
                columns: new[] { "HouseholdId", "PointDeductionDefinitionId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_PointTransactions_DeductionDefinitionLink",
                table: "PointTransactions",
                sql: "\"PointDeductionDefinitionId\" IS NULL OR (\"Type\" = 'Adjustment' AND \"Amount\" < 0)");

            migrationBuilder.CreateIndex(
                name: "IX_PointDeductionDefinitions_HouseholdId_ClientRequestId",
                table: "PointDeductionDefinitions",
                columns: new[] { "HouseholdId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PointDeductionDefinitions_HouseholdId_CreatedByMemberId",
                table: "PointDeductionDefinitions",
                columns: new[] { "HouseholdId", "CreatedByMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_PointDeductionDefinitions_HouseholdId_IsActive_Title",
                table: "PointDeductionDefinitions",
                columns: new[] { "HouseholdId", "IsActive", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_PointDeductionDefinitions_HouseholdId_UpdatedByMemberId",
                table: "PointDeductionDefinitions",
                columns: new[] { "HouseholdId", "UpdatedByMemberId" });

            migrationBuilder.AddForeignKey(
                name: "FK_PointTransactions_PointDeductionDefinitions_HouseholdId_Poi~",
                table: "PointTransactions",
                columns: new[] { "HouseholdId", "PointDeductionDefinitionId" },
                principalTable: "PointDeductionDefinitions",
                principalColumns: new[] { "HouseholdId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PointTransactions_PointDeductionDefinitions_HouseholdId_Poi~",
                table: "PointTransactions");

            migrationBuilder.DropTable(
                name: "PointDeductionDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_HouseholdId_PointDeductionDefinitionId",
                table: "PointTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PointTransactions_DeductionDefinitionLink",
                table: "PointTransactions");

            migrationBuilder.DropColumn(
                name: "PointDeductionDefinitionId",
                table: "PointTransactions");
        }
    }
}
