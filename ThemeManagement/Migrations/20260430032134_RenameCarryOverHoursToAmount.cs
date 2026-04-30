using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThemeManagement.Migrations
{
    /// <inheritdoc />
    public partial class RenameCarryOverHoursToAmount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CarryOverHours",
                table: "ThemeCarryOvers",
                newName: "CarryOverAmount");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CarryOverAmount",
                table: "ThemeCarryOvers",
                newName: "CarryOverHours");
        }
    }
}
