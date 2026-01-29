#!/usr/bin/env bash
set -euo pipefail

ARCHIVE_PATH="${1:-}"
FORCE="${2:-}"
NO_STOP="${3:-}"

if [ -z "$ARCHIVE_PATH" ]; then
  echo "Usage: ./ops/restore.sh /path/to/backup.tar.gz [--force] [--no-stop]"
  exit 1
fi

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
ARCHIVE_PATH="$(cd "$(dirname "$ARCHIVE_PATH")" && pwd)/$(basename "$ARCHIVE_PATH")"

if [ ! -f "$ARCHIVE_PATH" ]; then
  echo "Archive not found: $ARCHIVE_PATH"
  exit 1
fi

ARCHIVE_NAME="$(basename "$ARCHIVE_PATH")"

if ! tar -tzf "$ARCHIVE_PATH" >/dev/null 2>&1; then
  echo "Archive is not a valid tar.gz file."
  exit 1
fi

if tar -tzf "$ARCHIVE_PATH" | awk '($0 ~ /^\//) || ($0 ~ /\.\./) || ($0 !~ /^App_Data(\/|$)/) {exit 1}'; then
  :
else
  echo "Archive contents are unsafe or unexpected. Restore aborted."
  exit 1
fi

if [ "$FORCE" != "--force" ]; then
  read -r -p "This will overwrite App_Data. Type RESTORE to continue: " CONFIRM
  if [ "$CONFIRM" != "RESTORE" ]; then
    echo "Restore cancelled."
    exit 1
  fi
fi

cd "$ROOT_DIR"
if [ "$NO_STOP" != "--no-stop" ]; then
  docker compose --profile dev --profile prod down
fi

VOLUME_NAME="${LICENSEWATCH_VOLUME:-}"
if [ -z "$VOLUME_NAME" ]; then
  VOLUME_NAME="$(docker volume ls --filter label=com.docker.compose.volume=identity_data --format "{{.Name}}" | head -n 1)"
fi

if [ -z "$VOLUME_NAME" ]; then
  echo "Could not locate the App_Data volume. Set LICENSEWATCH_VOLUME to override."
  exit 1
fi

ARCHIVE_DIR="$(dirname "$ARCHIVE_PATH")"

docker run --rm -v "${VOLUME_NAME}:/data" -v "${ARCHIVE_DIR}:/backup" alpine sh -c "rm -rf /data/* /data/.[!.]* /data/..?*; tar -xzf /backup/${ARCHIVE_NAME} -C /data --strip-components=1 App_Data"

echo "Restore complete. Start the app with: docker compose --profile dev up -d"
