using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThemeManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddThemeCarryOver : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ThemeCarryOvers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ThemeId = table.Column<int>(type: "INTEGER", nullable: false),
                    FiscalYear = table.Column<int>(type: "INTEGER", nullable: false),
                    IsFirstHalf = table.Column<bool>(type: "INTEGER", nullable: false),
                    CarryOverHours = table.Column<decimal>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThemeCarryOvers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ThemeCarryOvers_Themes_ThemeId",
                        column: x => x.ThemeId,
                        principalTable: "Themes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ThemeCarryOvers_ThemeId_FiscalYear_IsFirstHalf",
                table: "ThemeCarryOvers",
                columns: new[] { "ThemeId", "FiscalYear", "IsFirstHalf" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ThemeCarryOvers");
        }
    }
}
