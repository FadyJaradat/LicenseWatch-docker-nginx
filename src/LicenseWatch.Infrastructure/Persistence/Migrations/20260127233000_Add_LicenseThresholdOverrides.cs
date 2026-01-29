using System;
using LicenseWatch.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LicenseWatch.Infrastructure.Persistence.Migrations;

[Migration("20260127233000_Add_LicenseThresholdOverrides")]
public partial class Add_LicenseThresholdOverrides : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "UseCustomThresholds",
            table: "Licenses",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "CriticalThresholdDays",
            table: "Licenses",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "WarningThresholdDays",
            table: "Licenses",
            type: "INTEGER",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "UseCustomThresholds",
            table: "Licenses");

        migrationBuilder.DropColumn(
            name: "CriticalThresholdDays",
            table: "Licenses");

        migrationBuilder.DropColumn(
            name: "WarningThresholdDays",
            table: "Licenses");
    }
}
