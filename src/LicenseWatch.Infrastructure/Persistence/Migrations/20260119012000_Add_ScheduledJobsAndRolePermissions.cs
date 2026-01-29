using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LicenseWatch.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add_ScheduledJobsAndRolePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RoleName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PermissionKey = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    GrantedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    GrantedByUserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    JobType = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    CronExpression = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ParametersJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleName",
                table: "RolePermissions",
                column: "RoleName");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleName_PermissionKey",
                table: "RolePermissions",
                columns: new[] { "RoleName", "PermissionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobs_JobType",
                table: "ScheduledJobs",
                column: "JobType");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobs_Key",
                table: "ScheduledJobs",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "ScheduledJobs");
        }
    }
}
