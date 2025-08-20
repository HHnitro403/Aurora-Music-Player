using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuroraMusic.Migrations
{
    /// <inheritdoc />
    public partial class AddIsMenuFixedToSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMenuFixed",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsMenuFixed",
                table: "Settings");
        }
    }
}
