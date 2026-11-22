using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAALogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LotsFefoStockStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_stock_balances_material_id_warehouse_id_location_id",
                table: "stock_balances");

            migrationBuilder.AddColumn<Guid>(
                name: "lot_id",
                table: "stock_movements",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "lot_id",
                table: "stock_balances",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "status",
                table: "stock_balances",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "requires_lot",
                table: "materials",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "lots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lots", x => x.id);
                    table.ForeignKey(
                        name: "fk_lots_materials_material_id",
                        column: x => x.material_id,
                        principalTable: "materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prep_pick_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_order_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: false),
                    suggested_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    suggested_lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    confirmed_quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    confirmed_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prep_pick_lines", x => x.id);
                    table.CheckConstraint("ck_prep_pick_lines_confirmed", "confirmed_quantity >= 0 AND confirmed_quantity <= quantity");
                    table.CheckConstraint("ck_prep_pick_lines_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_prep_pick_lines_location_nodes_suggested_location_id",
                        column: x => x.suggested_location_id,
                        principalTable: "location_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prep_pick_lines_materials_material_id",
                        column: x => x.material_id,
                        principalTable: "materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prep_pick_lines_sales_order_lines_sales_order_line_id",
                        column: x => x.sales_order_line_id,
                        principalTable: "sales_order_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_prep_pick_lines_sales_orders_sales_order_id",
                        column: x => x.sales_order_id,
                        principalTable: "sales_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stock_balances_material_id_warehouse_id_location_id_lot_id",
                table: "stock_balances",
                columns: new[] { "material_id", "warehouse_id", "location_id", "lot_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lots_material_id_code",
                table: "lots",
                columns: new[] { "material_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_prep_pick_lines_material_id",
                table: "prep_pick_lines",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "ix_prep_pick_lines_sales_order_id",
                table: "prep_pick_lines",
                column: "sales_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_prep_pick_lines_sales_order_line_id",
                table: "prep_pick_lines",
                column: "sales_order_line_id");

            migrationBuilder.CreateIndex(
                name: "ix_prep_pick_lines_suggested_location_id",
                table: "prep_pick_lines",
                column: "suggested_location_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lots");

            migrationBuilder.DropTable(
                name: "prep_pick_lines");

            migrationBuilder.DropIndex(
                name: "ix_stock_balances_material_id_warehouse_id_location_id_lot_id",
                table: "stock_balances");

            migrationBuilder.DropColumn(
                name: "lot_id",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "lot_id",
                table: "stock_balances");

            migrationBuilder.DropColumn(
                name: "status",
                table: "stock_balances");

            migrationBuilder.DropColumn(
                name: "requires_lot",
                table: "materials");

            migrationBuilder.CreateIndex(
                name: "ix_stock_balances_material_id_warehouse_id_location_id",
                table: "stock_balances",
                columns: new[] { "material_id", "warehouse_id", "location_id" },
                unique: true);
        }
    }
}
