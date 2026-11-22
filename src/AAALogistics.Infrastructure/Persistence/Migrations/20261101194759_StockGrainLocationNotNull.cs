using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAALogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StockGrainLocationNotNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure every warehouse has a DEFAULT location node for backfill.
            migrationBuilder.Sql(
                """
                INSERT INTO location_nodes (id, warehouse_id, parent_id, code, name, kind, is_active)
                SELECT gen_random_uuid(), w.id, NULL, 'DEFAULT', 'Ubicación por defecto', 'Root', TRUE
                FROM warehouses w
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM location_nodes ln
                    WHERE ln.warehouse_id = w.id
                      AND ln.code = 'DEFAULT');
                """);

            // Merge legacy null-location balances into an existing DEFAULT balance when both exist.
            migrationBuilder.Sql(
                """
                UPDATE stock_balances AS target
                SET quantity = target.quantity + source.quantity,
                    min_quantity = GREATEST(target.min_quantity, source.min_quantity)
                FROM stock_balances AS source
                INNER JOIN location_nodes ln
                    ON ln.warehouse_id = source.warehouse_id
                   AND ln.code = 'DEFAULT'
                WHERE source.location_id IS NULL
                  AND target.location_id = ln.id
                  AND target.material_id = source.material_id
                  AND target.warehouse_id = source.warehouse_id;

                DELETE FROM stock_balances AS source
                USING location_nodes ln
                WHERE source.location_id IS NULL
                  AND ln.warehouse_id = source.warehouse_id
                  AND ln.code = 'DEFAULT'
                  AND EXISTS (
                      SELECT 1
                      FROM stock_balances target
                      WHERE target.material_id = source.material_id
                        AND target.warehouse_id = source.warehouse_id
                        AND target.location_id = ln.id);
                """);

            // Assign remaining null balances to the warehouse DEFAULT node.
            migrationBuilder.Sql(
                """
                UPDATE stock_balances AS sb
                SET location_id = ln.id
                FROM location_nodes ln
                WHERE sb.location_id IS NULL
                  AND ln.warehouse_id = sb.warehouse_id
                  AND ln.code = 'DEFAULT';
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "location_id",
                table: "stock_balances",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // Add movement location_id nullable first, backfill DEFAULT, then harden.
            migrationBuilder.AddColumn<Guid>(
                name: "location_id",
                table: "stock_movements",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE stock_movements AS sm
                SET location_id = ln.id
                FROM location_nodes ln
                WHERE sm.location_id IS NULL
                  AND ln.warehouse_id = sm.warehouse_id
                  AND ln.code = 'DEFAULT';
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "location_id",
                table: "stock_movements",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_location_id",
                table: "stock_movements",
                column: "location_id");

            migrationBuilder.AddForeignKey(
                name: "fk_stock_movements_location_nodes_location_id",
                table: "stock_movements",
                column: "location_id",
                principalTable: "location_nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_stock_movements_location_nodes_location_id",
                table: "stock_movements");

            migrationBuilder.DropIndex(
                name: "ix_stock_movements_location_id",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "location_id",
                table: "stock_movements");

            migrationBuilder.AlterColumn<Guid>(
                name: "location_id",
                table: "stock_balances",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }
    }
}
