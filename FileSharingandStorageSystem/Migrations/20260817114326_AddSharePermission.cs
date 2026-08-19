using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileSharingandStorageSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddSharePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Permission",
                table: "FileShares",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Permission",
                table: "FileShares");
        }
    }
}
