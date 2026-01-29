using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LicenseWatch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_Optimization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CostPerSeatMonthly",
                table: "Licenses",
                type: "TEXT",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Licenses",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "OptimizationInsights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    LicenseId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DetectedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EvidenceJson = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptimizationInsights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OptimizationInsights_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_OptimizationInsights_Licenses_LicenseId",
                        column: x => x.LicenseId,
                        principalTable: "Licenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Recommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    EstimatedMonthlySavings = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    EstimatedAnnualSavings = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    LicenseId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OptimizationInsightId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Recommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Recommendations_Licenses_LicenseId",
                        column: x => x.LicenseId,
                        principalTable: "Licenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Recommendations_OptimizationInsights_OptimizationInsightId",
                        column: x => x.OptimizationInsightId,
                        principalTable: "OptimizationInsights",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OptimizationInsights_CategoryId",
                table: "OptimizationInsights",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_OptimizationInsights_IsActive",
                table: "OptimizationInsights",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_OptimizationInsights_Key",
                table: "OptimizationInsights",
                column: "Key");

            migrationBuilder.CreateIndex(
                name: "IX_OptimizationInsights_LicenseId",
                table: "OptimizationInsights",
                column: "LicenseId");

            migrationBuilder.CreateIndex(
                name: "IX_OptimizationInsights_Severity",
                table: "OptimizationInsights",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_LicenseId",
                table: "Recommendations",
                column: "LicenseId");

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_OptimizationInsightId",
                table: "Recommendations",
                column: "OptimizationInsightId");

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_Status",
                table: "Recommendations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Recommendations_UpdatedAtUtc",
                table: "Recommendations",
                column: "UpdatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Recommendations");

            migrationBuilder.DropTable(
                name: "OptimizationInsights");

            migrationBuilder.DropColumn(
                name: "CostPerSeatMonthly",
                table: "Licenses");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Licenses");
        }
    }
}
