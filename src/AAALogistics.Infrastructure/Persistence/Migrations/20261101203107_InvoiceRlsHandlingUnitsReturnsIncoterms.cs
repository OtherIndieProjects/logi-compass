using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAALogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InvoiceRlsHandlingUnitsReturnsIncoterms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "customs_reference",
                table: "sales_orders",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "incoterms",
                table: "sales_orders",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "handling_units",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sscc = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    sales_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_id = table.Column<Guid>(type: "uuid", nullable: true),
                    package_count = table.Column<int>(type: "integer", nullable: false),
                    created_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_handling_units", x => x.id);
                    table.ForeignKey(
                        name: "fk_handling_units_deliveries_delivery_id",
                        column: x => x.delivery_id,
                        principalTable: "deliveries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_handling_units_sales_orders_sales_order_id",
                        column: x => x.sales_order_id,
                        principalTable: "sales_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_handling_units_delivery_id",
                table: "handling_units",
                column: "delivery_id");

            migrationBuilder.CreateIndex(
                name: "ix_handling_units_sales_order_id",
                table: "handling_units",
                column: "sales_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_handling_units_sscc",
                table: "handling_units",
                column: "sscc",
                unique: true);

            migrationBuilder.Sql(
                """
                ALTER TABLE invoices ENABLE ROW LEVEL SECURITY;
                ALTER TABLE invoices FORCE ROW LEVEL SECURITY;
                ALTER TABLE invoice_lines ENABLE ROW LEVEL SECURITY;
                ALTER TABLE invoice_lines FORCE ROW LEVEL SECURITY;

                CREATE POLICY customer_isolation ON invoices
                    USING (
                        current_setting('app.customer_id', true) = ''
                        OR customer_id::text = current_setting('app.customer_id', true))
                    WITH CHECK (
                        current_setting('app.customer_id', true) = ''
                        OR customer_id::text = current_setting('app.customer_id', true));

                CREATE POLICY customer_isolation ON invoice_lines
                    USING (
                        current_setting('app.customer_id', true) = ''
                        OR EXISTS (
                            SELECT 1
                            FROM invoices AS parent
                            WHERE parent.id = invoice_lines.invoice_id
                              AND parent.customer_id::text = current_setting('app.customer_id', true)))
                    WITH CHECK (
                        current_setting('app.customer_id', true) = ''
                        OR EXISTS (
                            SELECT 1
                            FROM invoices AS parent
                            WHERE parent.id = invoice_lines.invoice_id
                              AND parent.customer_id::text = current_setting('app.customer_id', true)));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP POLICY IF EXISTS customer_isolation ON invoice_lines;
                DROP POLICY IF EXISTS customer_isolation ON invoices;
                ALTER TABLE invoice_lines NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE invoice_lines DISABLE ROW LEVEL SECURITY;
                ALTER TABLE invoices NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE invoices DISABLE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropTable(
                name: "handling_units");

            migrationBuilder.DropColumn(
                name: "customs_reference",
                table: "sales_orders");

            migrationBuilder.DropColumn(
                name: "incoterms",
                table: "sales_orders");
        }
    }
}
