using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace JioCxRcsWrapper.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApiSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ApiSettings",
                columns: new[] { "Id", "Description", "Key", "Value" },
                values: new object[,]
                {
                    { 1, "Base URL for JioCX API", "JioCx_BaseUrl", "https://rcsapi.jiocx.com" },
                    { 2, "Path for uploading media", "JioCx_UploadFilePath", "/api/v1/uploadFile" },
                    { 3, "Path for sending messages", "JioCx_SendMessagePath", "/api/v1/sendMessage" },
                    { 4, "Path for checking RCS capability", "JioCx_CheckCapabilityPath", "/api/v1/checkCapability" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiSettings");
        }
    }
}
