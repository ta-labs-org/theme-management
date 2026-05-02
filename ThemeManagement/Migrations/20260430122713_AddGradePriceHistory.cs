using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThemeManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddGradePriceHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GradePriceHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GradeId = table.Column<int>(type: "INTEGER", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    UnitSalePrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    UnitCostPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradePriceHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GradePriceHistories_Grades_GradeId",
                        column: x => x.GradeId,
                        principalTable: "Grades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GradePriceHistories_GradeId",
                table: "GradePriceHistories",
                column: "GradeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GradePriceHistories");
        }
    }
}
