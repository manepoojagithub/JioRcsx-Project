using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JioCxRcsWrapper.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceAuditAndRetries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequestPayload",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponseJson",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestPayload",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "ResponseJson",
                table: "AuditLogs");
        }
    }
}
