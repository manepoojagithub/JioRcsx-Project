using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JioCxRcsWrapper.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserDeveloperFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeveloper",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE Users SET IsDeveloper = 1 WHERE RoleId = 1");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "IsDeveloper",
                value: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDeveloper",
                table: "Users");
        }
    }
}
