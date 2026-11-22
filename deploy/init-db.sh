#!/bin/bash
# Crea el rol de aplicación aaa_app con privilegios mínimos.
# Se ejecuta una vez al inicializar el volumen de Postgres (usuario bootstrap).
set -euo pipefail

APP_PASSWORD="${AAA_APP_PASSWORD:-${POSTGRES_PASSWORD}}"
if [[ -z "${APP_PASSWORD}" ]]; then
  echo "AAA_APP_PASSWORD or POSTGRES_PASSWORD is required" >&2
  exit 1
fi

# Escape single quotes for a SQL string literal ('').
ESCAPED_PASSWORD="${APP_PASSWORD//\'/\'\'}"

psql -v ON_ERROR_STOP=1 --username "${POSTGRES_USER}" --dbname "${POSTGRES_DB}" <<SQL
DO \$\$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'aaa_owner') THEN
    CREATE ROLE aaa_owner NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
  ELSE
    ALTER ROLE aaa_owner WITH NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'aaa_app') THEN
    CREATE ROLE aaa_app LOGIN PASSWORD '${ESCAPED_PASSWORD}'
      NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
  ELSE
    ALTER ROLE aaa_app WITH LOGIN PASSWORD '${ESCAPED_PASSWORD}'
      NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
  END IF;
END
\$\$;

GRANT CONNECT ON DATABASE ${POSTGRES_DB} TO aaa_app;
GRANT USAGE, CREATE ON SCHEMA public TO aaa_app;
GRANT aaa_owner TO ${POSTGRES_USER};
ALTER SCHEMA public OWNER TO aaa_owner;
GRANT USAGE ON SCHEMA public TO aaa_app;
ALTER DEFAULT PRIVILEGES FOR ROLE aaa_owner IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO aaa_app;
ALTER DEFAULT PRIVILEGES FOR ROLE aaa_owner IN SCHEMA public
  GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO aaa_app;
SQL

echo "aaa_app role ready (NOSUPERUSER NOBYPASSRLS)."
