#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="${1:-Release}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"

cd "$ROOT"

echo "Restoring packages..."
dotnet restore

echo "Building ($CONFIGURATION)..."
dotnet build --configuration "$CONFIGURATION" --no-restore

echo "Build succeeded!"
