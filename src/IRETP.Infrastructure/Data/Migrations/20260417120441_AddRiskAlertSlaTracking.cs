using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IRETP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRiskAlertSlaTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AcknowledgeDeadline",
                table: "RiskAlerts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoEscalated",
                table: "RiskAlerts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastEscalatedAt",
                table: "RiskAlerts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolutionDeadline",
                table: "RiskAlerts",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcknowledgeDeadline",
                table: "RiskAlerts");

            migrationBuilder.DropColumn(
                name: "AutoEscalated",
                table: "RiskAlerts");

            migrationBuilder.DropColumn(
                name: "LastEscalatedAt",
                table: "RiskAlerts");

            migrationBuilder.DropColumn(
                name: "ResolutionDeadline",
                table: "RiskAlerts");
        }
    }
}
