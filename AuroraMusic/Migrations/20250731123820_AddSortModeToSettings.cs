using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuroraMusic.Migrations
{
    /// <inheritdoc />
    public partial class AddSortModeToSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SortMode",
                table: "Settings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SortMode",
                table: "Settings");
        }
    }
}
