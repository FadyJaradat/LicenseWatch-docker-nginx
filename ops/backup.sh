#!/usr/bin/env bash
set -euo pipefail

CONTAINER_NAME="${1:-${LICENSEWATCH_CONTAINER:-licensewatch-web}}"
ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
BACKUP_DIR="$ROOT_DIR/backups"

mkdir -p "$BACKUP_DIR"

if ! docker ps --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
  echo "Container '${CONTAINER_NAME}' is not running. Start it first (docker compose --profile dev up -d)."
  exit 1
fi

STAMP="$(date -u +%Y%m%d-%H%M%S)"
ARCHIVE="licensewatch-backup-${STAMP}.tar.gz"

docker exec "$CONTAINER_NAME" sh -c "tar -czf /tmp/${ARCHIVE} -C /app App_Data --exclude=App_Data/imports/tmp --exclude=App_Data/backups"
docker cp "${CONTAINER_NAME}:/tmp/${ARCHIVE}" "${BACKUP_DIR}/${ARCHIVE}"
docker exec "$CONTAINER_NAME" rm -f "/tmp/${ARCHIVE}"

echo "Backup saved to ${BACKUP_DIR}/${ARCHIVE}"
