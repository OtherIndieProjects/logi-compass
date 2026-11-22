using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAALogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameDefaultLocationRoots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename legacy DEFAULT roots to warehouse code (or R-{code} on conflict) / name «Raíz».
            migrationBuilder.Sql(
                """
                UPDATE location_nodes AS ln
                SET code = CASE
                        WHEN EXISTS (
                            SELECT 1
                            FROM location_nodes AS other
                            WHERE other.warehouse_id = ln.warehouse_id
                              AND other.id <> ln.id
                              AND other.code = w.code)
                        THEN left('R-' || w.code, 40)
                        ELSE w.code
                    END,
                    name = 'Raíz'
                FROM warehouses AS w
                WHERE ln.warehouse_id = w.id
                  AND ln.code = 'DEFAULT'
                  AND ln.kind = 'Root';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE location_nodes AS ln
                SET code = 'DEFAULT',
                    name = 'Ubicación por defecto'
                FROM warehouses AS w
                WHERE ln.warehouse_id = w.id
                  AND ln.kind = 'Root'
                  AND ln.parent_id IS NULL
                  AND (
                      ln.code = w.code
                      OR ln.code = left('R-' || w.code, 40)
                      OR ln.name = 'Raíz');
                """);
        }
    }
}
