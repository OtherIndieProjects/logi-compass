using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAALogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AppendixLabModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "purchase_requisitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: false),
                    warehouse_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_purchase_requisitions", x => x.id);
                    table.CheckConstraint("ck_purchase_requisitions_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_purchase_requisitions_materials_material_id",
                        column: x => x.material_id,
                        principalTable: "materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_purchase_requisitions_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "rate_cards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rate_cards", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "transport_routes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    carrier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    distance_km = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    base_tariff = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    rate_per_km = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transport_routes", x => x.id);
                    table.ForeignKey(
                        name: "fk_transport_routes_carriers_carrier_id",
                        column: x => x.carrier_id,
                        principalTable: "carriers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "unit_conversions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    factor = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_unit_conversions", x => x.id);
                    table.CheckConstraint("ck_unit_conversions_factor", "factor > 0");
                    table.ForeignKey(
                        name: "fk_unit_conversions_units_of_measure_from_unit_id",
                        column: x => x.from_unit_id,
                        principalTable: "units_of_measure",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_unit_conversions_units_of_measure_to_unit_id",
                        column: x => x.to_unit_id,
                        principalTable: "units_of_measure",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "waves",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    created_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_waves", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rate_card_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rate_card_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    unit_label = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rate_card_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_rate_card_lines_rate_cards_rate_card_id",
                        column: x => x.rate_card_id,
                        principalTable: "rate_cards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transport_stops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    sales_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    delivery_id = table.Column<Guid>(type: "uuid", nullable: true),
                    address_snapshot = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transport_stops", x => x.id);
                    table.ForeignKey(
                        name: "fk_transport_stops_transport_routes_route_id",
                        column: x => x.route_id,
                        principalTable: "transport_routes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wave_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wave_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sales_order_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wave_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_wave_lines_sales_orders_sales_order_id",
                        column: x => x.sales_order_id,
                        principalTable: "sales_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_wave_lines_waves_wave_id",
                        column: x => x.wave_id,
                        principalTable: "waves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_purchase_requisitions_material_id",
                table: "purchase_requisitions",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "ix_purchase_requisitions_number",
                table: "purchase_requisitions",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_purchase_requisitions_warehouse_id",
                table: "purchase_requisitions",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "ix_rate_card_lines_rate_card_id_service_code",
                table: "rate_card_lines",
                columns: new[] { "rate_card_id", "service_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rate_cards_code",
                table: "rate_cards",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transport_routes_carrier_id",
                table: "transport_routes",
                column: "carrier_id");

            migrationBuilder.CreateIndex(
                name: "ix_transport_routes_code",
                table: "transport_routes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transport_stops_route_id_sequence",
                table: "transport_stops",
                columns: new[] { "route_id", "sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_unit_conversions_from_unit_id_to_unit_id",
                table: "unit_conversions",
                columns: new[] { "from_unit_id", "to_unit_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_unit_conversions_to_unit_id",
                table: "unit_conversions",
                column: "to_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_wave_lines_sales_order_id",
                table: "wave_lines",
                column: "sales_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_wave_lines_wave_id_sales_order_id",
                table: "wave_lines",
                columns: new[] { "wave_id", "sales_order_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_waves_number",
                table: "waves",
                column: "number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "purchase_requisitions");

            migrationBuilder.DropTable(
                name: "rate_card_lines");

            migrationBuilder.DropTable(
                name: "transport_stops");

            migrationBuilder.DropTable(
                name: "unit_conversions");

            migrationBuilder.DropTable(
                name: "wave_lines");

            migrationBuilder.DropTable(
                name: "rate_cards");

            migrationBuilder.DropTable(
                name: "transport_routes");

            migrationBuilder.DropTable(
                name: "waves");
        }
    }
}
