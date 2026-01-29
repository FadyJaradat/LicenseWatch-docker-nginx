#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT_DIR"

dotnet restore
dotnet build LicenseWatch.slnx -c Release
dotnet test LicenseWatch.slnx -c Release

if [ "${1:-}" = "--docker" ]; then
  docker build -t licensewatch:ci .
fi

echo "CI checks completed."
