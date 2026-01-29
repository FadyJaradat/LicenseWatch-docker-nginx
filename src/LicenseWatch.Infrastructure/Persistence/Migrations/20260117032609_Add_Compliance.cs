using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LicenseWatch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_Compliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComplianceViolations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LicenseId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RuleKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    EvidenceJson = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    DetectedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastEvaluatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AcknowledgedByUserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceViolations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComplianceViolations_Licenses_LicenseId",
                        column: x => x.LicenseId,
                        principalTable: "Licenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UsageDailySummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LicenseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UsageDateUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MaxSeatsUsed = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsageDailySummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsageDailySummaries_Licenses_LicenseId",
                        column: x => x.LicenseId,
                        principalTable: "Licenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceViolations_LicenseId_RuleKey",
                table: "ComplianceViolations",
                columns: new[] { "LicenseId", "RuleKey" });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceViolations_LicenseId_Status",
                table: "ComplianceViolations",
                columns: new[] { "LicenseId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceViolations_Status_Severity",
                table: "ComplianceViolations",
                columns: new[] { "Status", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_UsageDailySummaries_LicenseId_UsageDateUtc",
                table: "UsageDailySummaries",
                columns: new[] { "LicenseId", "UsageDateUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComplianceViolations");

            migrationBuilder.DropTable(
                name: "UsageDailySummaries");
        }
    }
}
