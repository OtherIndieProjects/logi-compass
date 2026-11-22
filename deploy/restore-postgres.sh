#!/usr/bin/env bash
# Restauración operativa de un dump ya descifrado (formato custom de pg_restore).
# Preferible: comando del Host, que descifra .enc y verifica SHA-256:
#   AAALogistics.Server restore --file <ruta.dump.enc> --confirm YES
#
# Este script no hace nada si el primer argumento no es exactamente YES.
set -euo pipefail

if [[ "${1:-}" != "YES" || -z "${2:-}" ]]; then
  echo "Uso: restore-postgres.sh YES /ruta/al/archivo.dump" >&2
  echo "Sin YES exacto no se restaura nada." >&2
  echo "Los backups de producción son *.enc (AES-GCM). Descifre con el Host:" >&2
  echo "  dotnet run --project src/AAALogistics.Host -- restore --file archivo.dump.enc --confirm YES" >&2
  echo "Variables: PGHOST PGPORT PGDATABASE PGUSER (y PGPASSWORD si hace falta)." >&2
  exit 2
fi

if [[ ! -f "$2" ]]; then
  echo "No existe el archivo: $2" >&2
  exit 1
fi

if [[ "$2" == *.enc ]]; then
  echo "El archivo está cifrado (.enc). Use el comando restore del Host, no pg_restore directo." >&2
  exit 1
fi

: "${PGHOST:=127.0.0.1}"
: "${PGPORT:=5432}"
: "${PGDATABASE:=aaalogistics}"
: "${PGUSER:=aaa_app}"

pg_restore \
  --clean \
  --if-exists \
  --no-owner \
  --no-privileges \
  --exit-on-error \
  --single-transaction \
  --dbname="host=${PGHOST} port=${PGPORT} dbname=${PGDATABASE} user=${PGUSER}" \
  "$2"
