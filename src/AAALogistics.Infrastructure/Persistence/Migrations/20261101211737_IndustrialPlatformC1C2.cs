using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAALogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IndustrialPlatformC1C2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_stock_balances_material_id_warehouse_id_location_id_lot_id",
                table: "stock_balances");

            migrationBuilder.AddColumn<Guid>(
                name: "owner_customer_id",
                table: "stock_balances",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "company_id",
                table: "identity_users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tax_id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_companies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "exchange_rates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    to_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    rate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    valid_on = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_exchange_rates", x => x.id);
                    table.CheckConstraint("ck_exchange_rates_rate", "rate > 0");
                });

            migrationBuilder.CreateTable(
                name: "customer_commercial_terms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_term_days = table.Column<int>(type: "integer", nullable: false),
                    credit_limit = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    open_receivables = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    is_credit_blocked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_commercial_terms", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_commercial_terms_companies_company_id",
                        column: x => x.company_id,
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_customer_commercial_terms_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stock_balances_material_id_warehouse_id_location_id_lot_id_",
                table: "stock_balances",
                columns: new[] { "material_id", "warehouse_id", "location_id", "lot_id", "owner_customer_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_companies_code",
                table: "companies",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customer_commercial_terms_company_id",
                table: "customer_commercial_terms",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_commercial_terms_customer_id_company_id",
                table: "customer_commercial_terms",
                columns: new[] { "customer_id", "company_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exchange_rates_from_currency_to_currency_valid_on",
                table: "exchange_rates",
                columns: new[] { "from_currency", "to_currency", "valid_on" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_commercial_terms");

            migrationBuilder.DropTable(
                name: "exchange_rates");

            migrationBuilder.DropTable(
                name: "companies");

            migrationBuilder.DropIndex(
                name: "ix_stock_balances_material_id_warehouse_id_location_id_lot_id_",
                table: "stock_balances");

            migrationBuilder.DropColumn(
                name: "owner_customer_id",
                table: "stock_balances");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "identity_users");

            migrationBuilder.CreateIndex(
                name: "ix_stock_balances_material_id_warehouse_id_location_id_lot_id",
                table: "stock_balances",
                columns: new[] { "material_id", "warehouse_id", "location_id", "lot_id" },
                unique: true);
        }
    }
}
