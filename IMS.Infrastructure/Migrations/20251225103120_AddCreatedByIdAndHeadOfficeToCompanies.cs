using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMS.Infrastructure.Migrations
{
    public partial class AddCreatedByIdAndHeadOfficeToCompanies : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add CreatedById column
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedById",
                schema: "inventory",
                table: "Companies",
                type: "uniqueidentifier",
                nullable: true);

            // Add HeadOffice column
            migrationBuilder.AddColumn<string>(
                name: "HeadOffice",
                schema: "inventory",
                table: "Companies",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            // Create index for CreatedById
            migrationBuilder.CreateIndex(
                name: "IX_Companies_CreatedById",
                schema: "inventory",
                table: "Companies",
                column: "CreatedById",
                unique: true,
                filter: "[CreatedById] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop index for CreatedById
            migrationBuilder.DropIndex(
                name: "IX_Companies_CreatedById",
                schema: "inventory",
                table: "Companies");

            // Drop HeadOffice column
            migrationBuilder.DropColumn(
                name: "HeadOffice",
                schema: "inventory",
                table: "Companies");

            // Drop CreatedById column
            migrationBuilder.DropColumn(
                name: "CreatedById",
                schema: "inventory",
                table: "Companies");
        }
    }
}
