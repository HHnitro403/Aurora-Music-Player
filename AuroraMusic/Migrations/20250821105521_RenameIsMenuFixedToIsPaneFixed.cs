using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuroraMusic.Migrations
{
    /// <inheritdoc />
    public partial class RenameIsMenuFixedToIsPaneFixed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsMenuFixed",
                table: "Settings",
                newName: "IsPaneOpen");

            migrationBuilder.AddColumn<bool>(
                name: "IsPaneFixed",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPaneFixed",
                table: "Settings");

            migrationBuilder.RenameColumn(
                name: "IsPaneOpen",
                table: "Settings",
                newName: "IsMenuFixed");
        }
    }
}
