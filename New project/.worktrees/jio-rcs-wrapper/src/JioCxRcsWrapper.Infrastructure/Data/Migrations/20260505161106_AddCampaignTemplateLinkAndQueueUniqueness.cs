using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JioCxRcsWrapper.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignTemplateLinkAndQueueUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TemplateId",
                table: "CampaignMessages",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                ;WITH DuplicateQueueItems AS (
                    SELECT
                        Id,
                        ROW_NUMBER() OVER (PARTITION BY CampaignId, ContactId ORDER BY Id DESC) AS RowNumber
                    FROM CampaignQueueItems
                )
                DELETE FROM DuplicateQueueItems
                WHERE RowNumber > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignQueueItems_CampaignId_ContactId",
                table: "CampaignQueueItems",
                columns: new[] { "CampaignId", "ContactId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CampaignQueueItems_CampaignId_ContactId",
                table: "CampaignQueueItems");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "CampaignMessages");
        }
    }
}
