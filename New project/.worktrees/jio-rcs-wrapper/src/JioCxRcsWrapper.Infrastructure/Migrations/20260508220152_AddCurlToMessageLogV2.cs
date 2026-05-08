using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JioCxRcsWrapper.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCurlToMessageLogV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequestPayload",
                table: "MessageLogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponseJson",
                table: "MessageLogs",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestPayload",
                table: "MessageLogs");

            migrationBuilder.DropColumn(
                name: "ResponseJson",
                table: "MessageLogs");
        }
    }
}
