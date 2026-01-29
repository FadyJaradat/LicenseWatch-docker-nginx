using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LicenseWatch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_ReportPresets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReportPresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReportKey = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FiltersJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUsedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportPresets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportPresets_ReportKey",
                table: "ReportPresets",
                column: "ReportKey");

            migrationBuilder.CreateIndex(
                name: "IX_ReportPresets_ReportKey_Name",
                table: "ReportPresets",
                columns: new[] { "ReportKey", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportPresets");
        }
    }
}
