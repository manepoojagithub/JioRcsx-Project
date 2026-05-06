using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JioCxRcsWrapper.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateMediaMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocalMediaPath",
                table: "MessageTemplates",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaContentType",
                table: "MessageTemplates",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RcsMediaUrl",
                table: "MessageTemplates",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocalMediaPath",
                table: "MessageTemplates");

            migrationBuilder.DropColumn(
                name: "MediaContentType",
                table: "MessageTemplates");

            migrationBuilder.DropColumn(
                name: "RcsMediaUrl",
                table: "MessageTemplates");
        }
    }
}
