using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAALogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InventoryCountsAndReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_counts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    posted_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_counts", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventory_counts_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inventory_count_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inventory_count_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: false),
                    system_quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    counted_quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_count_lines", x => x.id);
                    table.CheckConstraint("ck_inventory_count_lines_quantities", "system_quantity >= 0 AND counted_quantity >= 0");
                    table.ForeignKey(
                        name: "fk_inventory_count_lines_inventory_counts_inventory_count_id",
                        column: x => x.inventory_count_id,
                        principalTable: "inventory_counts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_inventory_count_lines_materials_material_id",
                        column: x => x.material_id,
                        principalTable: "materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_count_lines_inventory_count_id",
                table: "inventory_count_lines",
                column: "inventory_count_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_count_lines_material_id",
                table: "inventory_count_lines",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_counts_number",
                table: "inventory_counts",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inventory_counts_warehouse_id",
                table: "inventory_counts",
                column: "warehouse_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_count_lines");

            migrationBuilder.DropTable(
                name: "inventory_counts");
        }
    }
}
