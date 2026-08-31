using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardPersonalizationAndWeather : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HouseholdPhotoAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoragePrefix = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    PixelWidth = table.Column<int>(type: "integer", nullable: false),
                    PixelHeight = table.Column<int>(type: "integer", nullable: false),
                    TotalByteLength = table.Column<long>(type: "bigint", nullable: false),
                    CreatedByHouseholdMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    RetiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdPhotoAssets", x => x.Id);
                    table.UniqueConstraint("AK_HouseholdPhotoAssets_HouseholdId_Id", x => new { x.HouseholdId, x.Id });
                    table.CheckConstraint("CK_HouseholdPhotoAssets_Dimensions", "\"PixelWidth\" > 0 AND \"PixelHeight\" > 0");
                    table.CheckConstraint("CK_HouseholdPhotoAssets_Length", "\"TotalByteLength\" > 0");
                    table.CheckConstraint("CK_HouseholdPhotoAssets_Retirement", "\"RetiredAt\" IS NULL OR \"RetiredAt\" >= \"CreatedAt\"");
                    table.ForeignKey(
                        name: "FK_HouseholdPhotoAssets_HouseholdMembers_HouseholdId_CreatedBy~",
                        columns: x => new { x.HouseholdId, x.CreatedByHouseholdMemberId },
                        principalTable: "HouseholdMembers",
                        principalColumns: new[] { "HouseholdId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HouseholdPhotoAssets_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HouseholdWeatherConfigurations",
                columns: table => new
                {
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(8,5)", precision: 8, scale: 5, nullable: false),
                    Longitude = table.Column<decimal>(type: "numeric(8,5)", precision: 8, scale: 5, nullable: false),
                    LocationLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TemperatureUnit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdWeatherConfigurations", x => x.HouseholdId);
                    table.CheckConstraint("CK_HouseholdWeatherConfigurations_Latitude", "\"Latitude\" BETWEEN -90 AND 90");
                    table.CheckConstraint("CK_HouseholdWeatherConfigurations_Longitude", "\"Longitude\" BETWEEN -180 AND 180");
                    table.CheckConstraint("CK_HouseholdWeatherConfigurations_TemperatureUnit", "\"TemperatureUnit\" IN ('auto', 'fahrenheit', 'celsius')");
                    table.CheckConstraint("CK_HouseholdWeatherConfigurations_Version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_HouseholdWeatherConfigurations_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HouseholdDashboardAppearances",
                columns: table => new
                {
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    GreetingTitle = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    GreetingMessage = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    CurrentPhotoAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    PhotoFocalX = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    PhotoFocalY = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdDashboardAppearances", x => x.HouseholdId);
                    table.CheckConstraint("CK_HouseholdDashboardAppearances_FocalX", "\"PhotoFocalX\" BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_HouseholdDashboardAppearances_FocalY", "\"PhotoFocalY\" BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_HouseholdDashboardAppearances_Version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_HouseholdDashboardAppearances_HouseholdPhotoAssets_Househol~",
                        columns: x => new { x.HouseholdId, x.CurrentPhotoAssetId },
                        principalTable: "HouseholdPhotoAssets",
                        principalColumns: new[] { "HouseholdId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HouseholdDashboardAppearances_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdDashboardAppearances_HouseholdId_CurrentPhotoAsset~",
                table: "HouseholdDashboardAppearances",
                columns: new[] { "HouseholdId", "CurrentPhotoAssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdPhotoAssets_HouseholdId_CreatedByHouseholdMemberId",
                table: "HouseholdPhotoAssets",
                columns: new[] { "HouseholdId", "CreatedByHouseholdMemberId" });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdPhotoAssets_HouseholdId_RetiredAt",
                table: "HouseholdPhotoAssets",
                columns: new[] { "HouseholdId", "RetiredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HouseholdDashboardAppearances");

            migrationBuilder.DropTable(
                name: "HouseholdWeatherConfigurations");

            migrationBuilder.DropTable(
                name: "HouseholdPhotoAssets");
        }
    }
}
