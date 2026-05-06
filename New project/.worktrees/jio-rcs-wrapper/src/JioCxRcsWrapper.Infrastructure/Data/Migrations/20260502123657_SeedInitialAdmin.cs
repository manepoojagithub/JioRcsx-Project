using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JioCxRcsWrapper.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedInitialAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "ClientId", "CreatedAt", "Email", "IsActive", "Name", "PasswordHash", "RoleId" },
                values: new object[] { 1, null, new DateTimeOffset(new DateTime(2026, 5, 2, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "admin@local.test", true, "System Admin", "AQAAAAIAAYagAAAAECm1p9KdJkz9/8pNO5Yb/5v6s+zxD9nBzWjydYpQ3ytXUvutLz6rvpLLecJxW7iWVQ==", 1 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1);
        }
    }
}
