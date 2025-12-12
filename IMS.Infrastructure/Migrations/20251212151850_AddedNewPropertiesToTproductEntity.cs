using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IMS.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedNewPropertiesToTproductEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Price",
                schema: "inventory",
                table: "Products",
                newName: "RetailPrice");

            migrationBuilder.AddColumn<decimal>(
                name: "CostPrice",
                schema: "inventory",
                table: "Products",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Profit",
                schema: "inventory",
                table: "Products",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostPrice",
                schema: "inventory",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Profit",
                schema: "inventory",
                table: "Products");

            migrationBuilder.RenameColumn(
                name: "RetailPrice",
                schema: "inventory",
                table: "Products",
                newName: "Price");
        }
    }
}
