using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAALogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SecurityHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "legal_version",
                table: "registration_requests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                table: "registration_requests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "legal_acceptances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    accepted_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    registration_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_legal_acceptances", x => x.id);
                    table.ForeignKey(
                        name: "fk_legal_acceptances_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_legal_acceptances_registration_requests_registration_reques",
                        column: x => x.registration_request_id,
                        principalTable: "registration_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    replaced_by_token_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_identity_users_user_id",
                        column: x => x.user_id,
                        principalTable: "identity_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_legal_acceptances_customer_id",
                table: "legal_acceptances",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_legal_acceptances_registration_request_id",
                table: "legal_acceptances",
                column: "registration_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_family_id",
                table: "refresh_tokens",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.Sql(
                """
                ALTER TABLE customers ENABLE ROW LEVEL SECURITY;
                ALTER TABLE customers FORCE ROW LEVEL SECURITY;
                ALTER TABLE sales_orders ENABLE ROW LEVEL SECURITY;
                ALTER TABLE sales_orders FORCE ROW LEVEL SECURITY;
                ALTER TABLE sales_order_lines ENABLE ROW LEVEL SECURITY;
                ALTER TABLE sales_order_lines FORCE ROW LEVEL SECURITY;
                ALTER TABLE deliveries ENABLE ROW LEVEL SECURITY;
                ALTER TABLE deliveries FORCE ROW LEVEL SECURITY;
                ALTER TABLE delivery_lines ENABLE ROW LEVEL SECURITY;
                ALTER TABLE delivery_lines FORCE ROW LEVEL SECURITY;
                ALTER TABLE registration_requests ENABLE ROW LEVEL SECURITY;
                ALTER TABLE registration_requests FORCE ROW LEVEL SECURITY;

                CREATE POLICY customer_isolation ON customers
                    USING (
                        current_setting('app.customer_id', true) = ''
                        OR id::text = current_setting('app.customer_id', true))
                    WITH CHECK (
                        current_setting('app.customer_id', true) = ''
                        OR id::text = current_setting('app.customer_id', true));

                CREATE POLICY customer_isolation ON sales_orders
                    USING (
                        current_setting('app.customer_id', true) = ''
                        OR customer_id::text = current_setting('app.customer_id', true))
                    WITH CHECK (
                        current_setting('app.customer_id', true) = ''
                        OR customer_id::text = current_setting('app.customer_id', true));

                CREATE POLICY customer_isolation ON sales_order_lines
                    USING (
                        current_setting('app.customer_id', true) = ''
                        OR EXISTS (
                            SELECT 1
                            FROM sales_orders AS parent
                            WHERE parent.id = sales_order_lines.sales_order_id
                              AND parent.customer_id::text = current_setting('app.customer_id', true)))
                    WITH CHECK (
                        current_setting('app.customer_id', true) = ''
                        OR EXISTS (
                            SELECT 1
                            FROM sales_orders AS parent
                            WHERE parent.id = sales_order_lines.sales_order_id
                              AND parent.customer_id::text = current_setting('app.customer_id', true)));

                CREATE POLICY customer_isolation ON deliveries
                    USING (
                        current_setting('app.customer_id', true) = ''
                        OR customer_id::text = current_setting('app.customer_id', true))
                    WITH CHECK (
                        current_setting('app.customer_id', true) = ''
                        OR customer_id::text = current_setting('app.customer_id', true));

                CREATE POLICY customer_isolation ON delivery_lines
                    USING (
                        current_setting('app.customer_id', true) = ''
                        OR EXISTS (
                            SELECT 1
                            FROM deliveries AS parent
                            WHERE parent.id = delivery_lines.delivery_id
                              AND parent.customer_id::text = current_setting('app.customer_id', true)))
                    WITH CHECK (
                        current_setting('app.customer_id', true) = ''
                        OR EXISTS (
                            SELECT 1
                            FROM deliveries AS parent
                            WHERE parent.id = delivery_lines.delivery_id
                              AND parent.customer_id::text = current_setting('app.customer_id', true)));

                CREATE POLICY customer_isolation ON registration_requests
                    USING (
                        current_setting('app.customer_id', true) = ''
                        OR customer_id::text = current_setting('app.customer_id', true))
                    WITH CHECK (
                        current_setting('app.customer_id', true) = ''
                        OR customer_id::text = current_setting('app.customer_id', true));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP POLICY IF EXISTS customer_isolation ON registration_requests;
                DROP POLICY IF EXISTS customer_isolation ON delivery_lines;
                DROP POLICY IF EXISTS customer_isolation ON deliveries;
                DROP POLICY IF EXISTS customer_isolation ON sales_order_lines;
                DROP POLICY IF EXISTS customer_isolation ON sales_orders;
                DROP POLICY IF EXISTS customer_isolation ON customers;
                ALTER TABLE registration_requests NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE registration_requests DISABLE ROW LEVEL SECURITY;
                ALTER TABLE delivery_lines NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE delivery_lines DISABLE ROW LEVEL SECURITY;
                ALTER TABLE deliveries NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE deliveries DISABLE ROW LEVEL SECURITY;
                ALTER TABLE sales_order_lines NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE sales_order_lines DISABLE ROW LEVEL SECURITY;
                ALTER TABLE sales_orders NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE sales_orders DISABLE ROW LEVEL SECURITY;
                ALTER TABLE customers NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE customers DISABLE ROW LEVEL SECURITY;
                """);

            migrationBuilder.DropTable(
                name: "legal_acceptances");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "legal_version",
                table: "registration_requests");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                table: "registration_requests");
        }
    }
}
