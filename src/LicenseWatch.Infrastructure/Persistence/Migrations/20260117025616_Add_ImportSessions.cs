using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LicenseWatch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_ImportSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    StoredFileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    TotalRows = table.Column<int>(type: "INTEGER", nullable: false),
                    ValidRows = table.Column<int>(type: "INTEGER", nullable: false),
                    InvalidRows = table.Column<int>(type: "INTEGER", nullable: false),
                    NewLicenses = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedLicenses = table.Column<int>(type: "INTEGER", nullable: false),
                    NewCategories = table.Column<int>(type: "INTEGER", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ImportSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RowNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    LicenseIdRaw = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    LicenseId = table.Column<Guid>(type: "TEXT", nullable: true),
                    LicenseName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CategoryName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Vendor = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SeatsPurchased = table.Column<int>(type: "INTEGER", nullable: true),
                    SeatsAssigned = table.Column<int>(type: "INTEGER", nullable: true),
                    ExpiresOnUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    IsValid = table.Column<bool>(type: "INTEGER", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportRows_ImportSessions_ImportSessionId",
                        column: x => x.ImportSessionId,
                        principalTable: "ImportSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportRows_ImportSessionId",
                table: "ImportRows",
                column: "ImportSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportRows_RowNumber",
                table: "ImportRows",
                column: "RowNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ImportSessions_CreatedAtUtc",
                table: "ImportSessions",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportRows");

            migrationBuilder.DropTable(
                name: "ImportSessions");
        }
    }
}
