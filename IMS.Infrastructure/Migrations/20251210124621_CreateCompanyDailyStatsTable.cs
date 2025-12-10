using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateCompanyDailyStatsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyDailyStats",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TotalInventoryValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TopProductsBySalesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LowOnStockProductsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsUpdated = table.Column<bool>(type: "bit", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyDailyStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyDailyStats_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "inventory",
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyDailyStats_CompanyId",
                schema: "inventory",
                table: "CompanyDailyStats",
                column: "CompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyDailyStats",
                schema: "inventory");
        }
    }
}
