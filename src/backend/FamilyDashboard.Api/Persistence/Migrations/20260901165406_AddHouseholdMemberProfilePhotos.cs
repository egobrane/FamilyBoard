using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdMemberProfilePhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentPhotoAssetId",
                table: "HouseholdMembers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PhotoFocalX",
                table: "HouseholdMembers",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0.5m);

            migrationBuilder.AddColumn<decimal>(
                name: "PhotoFocalY",
                table: "HouseholdMembers",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: false,
                defaultValue: 0.5m);

            migrationBuilder.AddColumn<long>(
                name: "PhotoVersion",
                table: "HouseholdMembers",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.CreateTable(
                name: "HouseholdMemberPhotoAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoragePrefix = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    PixelWidth = table.Column<int>(type: "integer", nullable: false),
                    PixelHeight = table.Column<int>(type: "integer", nullable: false),
                    TotalByteLength = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedByHouseholdMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdMemberPhotoAssets", x => x.Id);
                    table.UniqueConstraint("AK_HouseholdMemberPhotoAssets_HouseholdId_HouseholdMemberId_Id", x => new { x.HouseholdId, x.HouseholdMemberId, x.Id });
                    table.CheckConstraint("CK_HouseholdMemberPhotoAssets_Dimensions", "\"PixelWidth\" > 0 AND \"PixelHeight\" > 0");
                    table.CheckConstraint("CK_HouseholdMemberPhotoAssets_Length", "\"TotalByteLength\" > 0");
                    table.CheckConstraint("CK_HouseholdMemberPhotoAssets_Lifecycle", "(\"State\" = 'Pending' AND \"ActivatedAt\" IS NULL AND \"RetiredAt\" IS NULL) OR (\"State\" = 'Active' AND \"ActivatedAt\" IS NOT NULL AND \"RetiredAt\" IS NULL) OR (\"State\" = 'Retired' AND \"RetiredAt\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_HouseholdMemberPhotoAssets_HouseholdMembers_HouseholdId_Cre~",
                        columns: x => new { x.HouseholdId, x.CreatedByHouseholdMemberId },
                        principalTable: "HouseholdMembers",
                        principalColumns: new[] { "HouseholdId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HouseholdMemberPhotoAssets_HouseholdMembers_HouseholdId_Hou~",
                        columns: x => new { x.HouseholdId, x.HouseholdMemberId },
                        principalTable: "HouseholdMembers",
                        principalColumns: new[] { "HouseholdId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HouseholdMemberPhotoAssets_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdMembers_HouseholdId_Id_CurrentPhotoAssetId",
                table: "HouseholdMembers",
                columns: new[] { "HouseholdId", "Id", "CurrentPhotoAssetId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_HouseholdMembers_PhotoFocalX",
                table: "HouseholdMembers",
                sql: "\"PhotoFocalX\" BETWEEN 0 AND 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HouseholdMembers_PhotoFocalY",
                table: "HouseholdMembers",
                sql: "\"PhotoFocalY\" BETWEEN 0 AND 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HouseholdMembers_PhotoVersion",
                table: "HouseholdMembers",
                sql: "\"PhotoVersion\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdMemberPhotoAssets_HouseholdId_CreatedByHouseholdMe~",
                table: "HouseholdMemberPhotoAssets",
                columns: new[] { "HouseholdId", "CreatedByHouseholdMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdMemberPhotoAssets_HouseholdId_HouseholdMemberId",
                table: "HouseholdMemberPhotoAssets",
                columns: new[] { "HouseholdId", "HouseholdMemberId" },
                unique: true,
                filter: "\"State\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdMemberPhotoAssets_State_CreatedAt",
                table: "HouseholdMemberPhotoAssets",
                columns: new[] { "State", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdMemberPhotoAssets_StoragePrefix",
                table: "HouseholdMemberPhotoAssets",
                column: "StoragePrefix",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_HouseholdMembers_HouseholdMemberPhotoAssets_HouseholdId_Id_~",
                table: "HouseholdMembers",
                columns: new[] { "HouseholdId", "Id", "CurrentPhotoAssetId" },
                principalTable: "HouseholdMemberPhotoAssets",
                principalColumns: new[] { "HouseholdId", "HouseholdMemberId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HouseholdMembers_HouseholdMemberPhotoAssets_HouseholdId_Id_~",
                table: "HouseholdMembers");

            migrationBuilder.DropTable(
                name: "HouseholdMemberPhotoAssets");

            migrationBuilder.DropIndex(
                name: "IX_HouseholdMembers_HouseholdId_Id_CurrentPhotoAssetId",
                table: "HouseholdMembers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HouseholdMembers_PhotoFocalX",
                table: "HouseholdMembers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HouseholdMembers_PhotoFocalY",
                table: "HouseholdMembers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HouseholdMembers_PhotoVersion",
                table: "HouseholdMembers");

            migrationBuilder.DropColumn(
                name: "CurrentPhotoAssetId",
                table: "HouseholdMembers");

            migrationBuilder.DropColumn(
                name: "PhotoFocalX",
                table: "HouseholdMembers");

            migrationBuilder.DropColumn(
                name: "PhotoFocalY",
                table: "HouseholdMembers");

            migrationBuilder.DropColumn(
                name: "PhotoVersion",
                table: "HouseholdMembers");
        }
    }
}
