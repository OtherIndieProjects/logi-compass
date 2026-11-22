using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAALogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ResidualAndV1Core : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "suspended_utc",
                table: "identity_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "suspension_reason",
                table: "identity_users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.Sql(
                """
                DROP POLICY IF EXISTS customer_isolation ON legal_acceptances;
                CREATE POLICY customer_isolation ON legal_acceptances
                    USING (
                        COALESCE(current_setting('app.customer_id', true), '') = ''
                        OR customer_id::text = current_setting('app.customer_id', true))
                    WITH CHECK (
                        COALESCE(current_setting('app.customer_id', true), '') = ''
                        OR customer_id::text = current_setting('app.customer_id', true));

                -- Transfer ownership to aaa_owner when the role exists (production init).
                DO $owner$
                DECLARE
                  tbl text;
                BEGIN
                  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'aaa_owner') THEN
                    RETURN;
                  END IF;
                  FOR tbl IN
                    SELECT c.relname
                    FROM pg_class c
                    JOIN pg_namespace n ON n.oid = c.relnamespace
                    WHERE n.nspname = current_schema()
                      AND c.relkind = 'r'
                      AND c.relname NOT LIKE 'pg_%'
                  LOOP
                    EXECUTE format('ALTER TABLE %I OWNER TO aaa_owner', tbl);
                    EXECUTE format(
                      'GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE %I TO aaa_app',
                      tbl);
                  END LOOP;
                  BEGIN
                    EXECUTE 'REVOKE UPDATE, DELETE, TRUNCATE ON TABLE audit_log_entries FROM aaa_app';
                  EXCEPTION WHEN OTHERS THEN
                    NULL;
                  END;
                END
                $owner$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP POLICY IF EXISTS customer_isolation ON legal_acceptances;
                CREATE POLICY customer_isolation ON legal_acceptances
                    USING (
                        current_setting('app.customer_id', true) = ''
                        OR customer_id IS NULL
                        OR customer_id::text = current_setting('app.customer_id', true))
                    WITH CHECK (
                        current_setting('app.customer_id', true) = ''
                        OR customer_id IS NULL
                        OR customer_id::text = current_setting('app.customer_id', true));
                """);

            migrationBuilder.DropColumn(
                name: "suspended_utc",
                table: "identity_users");

            migrationBuilder.DropColumn(
                name: "suspension_reason",
                table: "identity_users");
        }
    }
}
