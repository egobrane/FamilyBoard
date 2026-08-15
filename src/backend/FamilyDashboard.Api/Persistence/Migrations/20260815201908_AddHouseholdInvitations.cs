using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FamilyDashboard.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HouseholdInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntendedEmailNormalized = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AcceptedByUserAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedByUserAccountId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdInvitations", x => x.Id);
                    table.CheckConstraint("CK_HouseholdInvitations_ExpiresAfterCreation", "\"ExpiresAt\" > \"CreatedAt\"");
                    table.CheckConstraint("CK_HouseholdInvitations_NormalizedEmail", "\"IntendedEmailNormalized\" = lower(btrim(\"IntendedEmailNormalized\"))");
                    table.CheckConstraint("CK_HouseholdInvitations_TerminalState", "(\"Status\" = 'Pending' AND \"AcceptedAt\" IS NULL AND \"AcceptedByUserAccountId\" IS NULL AND \"RevokedAt\" IS NULL AND \"RevokedByUserAccountId\" IS NULL) OR (\"Status\" = 'Expired' AND \"AcceptedAt\" IS NULL AND \"AcceptedByUserAccountId\" IS NULL AND \"RevokedAt\" IS NULL AND \"RevokedByUserAccountId\" IS NULL) OR (\"Status\" = 'Accepted' AND \"AcceptedAt\" IS NOT NULL AND \"AcceptedByUserAccountId\" IS NOT NULL AND \"RevokedAt\" IS NULL AND \"RevokedByUserAccountId\" IS NULL) OR (\"Status\" = 'Revoked' AND \"AcceptedAt\" IS NULL AND \"AcceptedByUserAccountId\" IS NULL AND \"RevokedAt\" IS NOT NULL AND \"RevokedByUserAccountId\" IS NOT NULL)");
                    table.CheckConstraint("CK_HouseholdInvitations_TokenHashLength", "octet_length(\"TokenHash\") = 32");
                    table.ForeignKey(
                        name: "FK_HouseholdInvitations_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HouseholdInvitations_UserAccounts_AcceptedByUserAccountId",
                        column: x => x.AcceptedByUserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HouseholdInvitations_UserAccounts_CreatedByUserAccountId",
                        column: x => x.CreatedByUserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HouseholdInvitations_UserAccounts_RevokedByUserAccountId",
                        column: x => x.RevokedByUserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvitations_AcceptedByUserAccountId",
                table: "HouseholdInvitations",
                column: "AcceptedByUserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvitations_CreatedByUserAccountId",
                table: "HouseholdInvitations",
                column: "CreatedByUserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvitations_HouseholdId_CreatedAt",
                table: "HouseholdInvitations",
                columns: new[] { "HouseholdId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvitations_HouseholdId_IntendedEmailNormalized",
                table: "HouseholdInvitations",
                columns: new[] { "HouseholdId", "IntendedEmailNormalized" },
                unique: true,
                filter: "\"Status\" = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvitations_HouseholdId_IntendedEmailNormalized_St~",
                table: "HouseholdInvitations",
                columns: new[] { "HouseholdId", "IntendedEmailNormalized", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvitations_RevokedByUserAccountId",
                table: "HouseholdInvitations",
                column: "RevokedByUserAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvitations_TokenHash",
                table: "HouseholdInvitations",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HouseholdInvitations");
        }
    }
}
