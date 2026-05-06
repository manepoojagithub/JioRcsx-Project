using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JioCxRcsWrapper.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUniqueClientAgentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Clients_AgentId",
                table: "Clients");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_AgentId",
                table: "Clients",
                column: "AgentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Clients_AgentId",
                table: "Clients");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_AgentId",
                table: "Clients",
                column: "AgentId",
                unique: true);
        }
    }
}
