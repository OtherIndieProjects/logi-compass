using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAALogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StockMovementOwnerCustomerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_stock_movements_material_id",
                table: "stock_movements");

            migrationBuilder.AddColumn<Guid>(
                name: "owner_customer_id",
                table: "stock_movements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_material_id_warehouse_id_owner_customer_id",
                table: "stock_movements",
                columns: new[] { "material_id", "warehouse_id", "owner_customer_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_stock_movements_material_id_warehouse_id_owner_customer_id",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "owner_customer_id",
                table: "stock_movements");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_material_id",
                table: "stock_movements",
                column: "material_id");
        }
    }
}
