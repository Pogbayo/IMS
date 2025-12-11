using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsActiveToBaseEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "inventory",
                table: "Warehouses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MadeActive",
                schema: "inventory",
                table: "Warehouses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                schema: "inventory",
                table: "Warehouses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "MadeActive",
                schema: "inventory",
                table: "Suppliers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                schema: "inventory",
                table: "Suppliers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "inventory",
                table: "StockTransactions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MadeActive",
                schema: "inventory",
                table: "StockTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                schema: "inventory",
                table: "StockTransactions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "MadeActive",
                schema: "inventory",
                table: "ProductWarehouses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                schema: "inventory",
                table: "ProductWarehouses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "ImgUrl",
                schema: "inventory",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "inventory",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MadeActive",
                schema: "inventory",
                table: "Products",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                schema: "inventory",
                table: "Products",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "inventory",
                table: "Expenses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MadeActive",
                schema: "inventory",
                table: "Expenses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                schema: "inventory",
                table: "Expenses",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "inventory",
                table: "CompanyDailyStats",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MadeActive",
                schema: "inventory",
                table: "CompanyDailyStats",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                schema: "inventory",
                table: "CompanyDailyStats",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "inventory",
                table: "Companies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MadeActive",
                schema: "inventory",
                table: "Companies",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                schema: "inventory",
                table: "Companies",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "inventory",
                table: "Categories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MadeActive",
                schema: "inventory",
                table: "Categories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                schema: "inventory",
                table: "Categories",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "inventory",
                table: "AuditLogs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "MadeActive",
                schema: "inventory",
                table: "AuditLogs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                schema: "inventory",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "inventory",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "MadeActive",
                schema: "inventory",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                schema: "inventory",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "MadeActive",
                schema: "inventory",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                schema: "inventory",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "inventory",
                table: "StockTransactions");

            migrationBuilder.DropColumn(
                name: "MadeActive",
                schema: "inventory",
                table: "StockTransactions");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                schema: "inventory",
                table: "StockTransactions");

            migrationBuilder.DropColumn(
                name: "MadeActive",
                schema: "inventory",
                table: "ProductWarehouses");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                schema: "inventory",
                table: "ProductWarehouses");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "inventory",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MadeActive",
                schema: "inventory",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                schema: "inventory",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "inventory",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "MadeActive",
                schema: "inventory",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                schema: "inventory",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "inventory",
                table: "CompanyDailyStats");

            migrationBuilder.DropColumn(
                name: "MadeActive",
                schema: "inventory",
                table: "CompanyDailyStats");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                schema: "inventory",
                table: "CompanyDailyStats");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "inventory",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "MadeActive",
                schema: "inventory",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                schema: "inventory",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "inventory",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "MadeActive",
                schema: "inventory",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                schema: "inventory",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "inventory",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "MadeActive",
                schema: "inventory",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                schema: "inventory",
                table: "AuditLogs");

            migrationBuilder.AlterColumn<string>(
                name: "ImgUrl",
                schema: "inventory",
                table: "Products",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
