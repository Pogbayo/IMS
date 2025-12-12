using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNullableWarehousesToStockTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FromWarehouseId",
                schema: "inventory",
                table: "StockTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ToWarehouseId",
                schema: "inventory",
                table: "StockTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsUpdated",
                schema: "inventory",
                table: "AppUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "inventory",
                table: "AppUsers",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_FromWarehouseId",
                schema: "inventory",
                table: "StockTransactions",
                column: "FromWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_ToWarehouseId",
                schema: "inventory",
                table: "StockTransactions",
                column: "ToWarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_StockTransactions_Warehouses_FromWarehouseId",
                schema: "inventory",
                table: "StockTransactions",
                column: "FromWarehouseId",
                principalSchema: "inventory",
                principalTable: "Warehouses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StockTransactions_Warehouses_ToWarehouseId",
                schema: "inventory",
                table: "StockTransactions",
                column: "ToWarehouseId",
                principalSchema: "inventory",
                principalTable: "Warehouses",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockTransactions_Warehouses_FromWarehouseId",
                schema: "inventory",
                table: "StockTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTransactions_Warehouses_ToWarehouseId",
                schema: "inventory",
                table: "StockTransactions");

            migrationBuilder.DropIndex(
                name: "IX_StockTransactions_FromWarehouseId",
                schema: "inventory",
                table: "StockTransactions");

            migrationBuilder.DropIndex(
                name: "IX_StockTransactions_ToWarehouseId",
                schema: "inventory",
                table: "StockTransactions");

            migrationBuilder.DropColumn(
                name: "FromWarehouseId",
                schema: "inventory",
                table: "StockTransactions");

            migrationBuilder.DropColumn(
                name: "ToWarehouseId",
                schema: "inventory",
                table: "StockTransactions");

            migrationBuilder.DropColumn(
                name: "IsUpdated",
                schema: "inventory",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "inventory",
                table: "AppUsers");
        }
    }
}
