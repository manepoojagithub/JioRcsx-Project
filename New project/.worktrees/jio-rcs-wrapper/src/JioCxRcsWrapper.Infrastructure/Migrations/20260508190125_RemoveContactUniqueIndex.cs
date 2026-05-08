using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JioCxRcsWrapper.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveContactUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contacts_CampaignId_MobileNumber",
                table: "Contacts");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_CampaignId_MobileNumber",
                table: "Contacts",
                columns: new[] { "CampaignId", "MobileNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contacts_CampaignId_MobileNumber",
                table: "Contacts");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_CampaignId_MobileNumber",
                table: "Contacts",
                columns: new[] { "CampaignId", "MobileNumber" },
                unique: true);
        }
    }
}
