using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IRETP.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnershipMortgagePlaybook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PlaybookStepsJson",
                table: "RiskThresholds",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlaybookProgressJson",
                table: "RiskAlerts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BeneficialOwners",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeveloperId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OwnerNameAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OwnerType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CountryOfIncorporation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OwnershipPct = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DisclosedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DisclosureSource = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeneficialOwners", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BeneficialOwners_Developers_DeveloperId",
                        column: x => x.DeveloperId,
                        principalTable: "Developers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BeneficialOwners_DeveloperId",
                table: "BeneficialOwners",
                column: "DeveloperId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BeneficialOwners");

            migrationBuilder.DropColumn(
                name: "PlaybookStepsJson",
                table: "RiskThresholds");

            migrationBuilder.DropColumn(
                name: "PlaybookProgressJson",
                table: "RiskAlerts");
        }
    }
}
