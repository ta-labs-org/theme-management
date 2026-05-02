using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThemeManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddThemeTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Themes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Themes");
        }
    }
}
