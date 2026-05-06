using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JioCxRcsWrapper.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RestoreConfigurableCredits : Migration
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

            migrationBuilder.AddColumn<int>(
                name: "CreditCostPerMessage",
                table: "Clients",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Credits",
                table: "Clients",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LowCreditThreshold",
                table: "Clients",
                type: "int",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.Sql("""
                UPDATE Clients
                SET Credits = 100,
                    CreditCostPerMessage = 1,
                    LowCreditThreshold = 10;

                UPDATE Users
                SET Credits = 100
                WHERE ClientId IS NOT NULL;

                UPDATE Users
                SET Credits = 100000
                WHERE ClientId IS NULL;

                ;WITH DuplicateBrands AS (
                    SELECT
                        Id,
                        ROW_NUMBER() OVER (PARTITION BY BrandName ORDER BY Id) AS RowNumber
                    FROM Clients
                )
                UPDATE Clients
                SET BrandName = CONCAT(BrandName, ' ', Clients.Id)
                FROM Clients
                INNER JOIN DuplicateBrands ON DuplicateBrands.Id = Clients.Id
                WHERE DuplicateBrands.RowNumber > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_BrandName",
                table: "Clients",
                column: "BrandName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Clients_BrandName",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "Credits",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreditCostPerMessage",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "Credits",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "LowCreditThreshold",
                table: "Clients");
        }
    }
}
