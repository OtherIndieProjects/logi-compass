#!/usr/bin/env bash
# Copia un Host publicado e instala/arranca la unidad systemd.
# Uso: sudo ./deploy/install-linux.sh <directorio-publicado>
set -euo pipefail

if [[ "${EUID}" -ne 0 ]]; then
  echo "Ejecute este script como root." >&2
  exit 1
fi

PUBLISH_DIR="${1:-}"
if [[ -z "${PUBLISH_DIR}" || ! -d "${PUBLISH_DIR}" ]]; then
  echo "Uso: sudo ./deploy/install-linux.sh <directorio-publicado>" >&2
  exit 1
fi

PREFIX="${PREFIX:-/opt/aaalogistics}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if ! id aaalogistics >/dev/null 2>&1; then
  useradd --system --home-dir "${PREFIX}" --shell /usr/sbin/nologin aaalogistics
fi

install -d -m 0755 "${PREFIX}"
install -d -m 0750 "${PREFIX}/Data"
cp -a "${PUBLISH_DIR}/." "${PREFIX}/"
if [[ -f "${PREFIX}/AAALogistics.Server" ]]; then
  chmod 0755 "${PREFIX}/AAALogistics.Server"
fi
chown -R aaalogistics:aaalogistics "${PREFIX}/Data"

install -m 0644 "${ROOT}/aaalogistics.service" /etc/systemd/system/aaalogistics.service
systemctl daemon-reload
systemctl enable --now aaalogistics.service
systemctl --no-pager --full status aaalogistics.service || true
echo "Servicio aaalogistics habilitado. Datos y bloqueo: ${PREFIX}/Data"
