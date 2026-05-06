using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace JioCxRcsWrapper.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserCreditsAndMessageBuilderPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Credits",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "Id", "Module", "PermissionId", "RoleId" },
                values: new object[,]
                {
                    { 47, "MessageBuilder", 1, 1 },
                    { 48, "MessageBuilder", 2, 1 },
                    { 49, "MessageBuilder", 3, 1 },
                    { 50, "MessageBuilder", 4, 1 },
                    { 51, "MessageBuilder", 5, 1 },
                    { 52, "MessageBuilder", 1, 2 },
                    { 53, "MessageBuilder", 2, 2 },
                    { 54, "MessageBuilder", 3, 2 },
                    { 55, "MessageBuilder", 4, 2 },
                    { 56, "MessageBuilder", 1, 3 }
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "Credits",
                value: 100000);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 48);

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 49);

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 51);

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 53);

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 54);

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 55);

            migrationBuilder.DeleteData(
                table: "RolePermissions",
                keyColumn: "Id",
                keyValue: 56);

            migrationBuilder.DropColumn(
                name: "Credits",
                table: "Users");
        }
    }
}
