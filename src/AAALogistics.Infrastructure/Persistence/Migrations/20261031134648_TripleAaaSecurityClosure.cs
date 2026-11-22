using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAALogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TripleAaaSecurityClosure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE legal_acceptances ENABLE ROW LEVEL SECURITY;
                ALTER TABLE legal_acceptances FORCE ROW LEVEL SECURITY;

                CREATE POLICY customer_isolation ON legal_acceptances
                    USING (
                        current_setting('app.customer_id', true) = ''
                        OR customer_id IS NULL
                        OR customer_id::text = current_setting('app.customer_id', true))
                    WITH CHECK (
                        current_setting('app.customer_id', true) = ''
                        OR customer_id IS NULL
                        OR customer_id::text = current_setting('app.customer_id', true));

                -- Harden audit ledger privileges for the current app role when it is
                -- not the table owner. Owners retain rights; production init uses
                -- aaa_owner + grants so REVOKE takes effect for aaa_app.
                DO $revoke$
                BEGIN
                  EXECUTE format(
                    'REVOKE UPDATE, DELETE, TRUNCATE ON TABLE audit_log_entries FROM %I',
                    current_user);
                EXCEPTION
                  WHEN insufficient_privilege OR undefined_object THEN
                    NULL;
                END
                $revoke$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP POLICY IF EXISTS customer_isolation ON legal_acceptances;
                ALTER TABLE legal_acceptances NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE legal_acceptances DISABLE ROW LEVEL SECURITY;
                """);
        }
    }
}
