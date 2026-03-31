#!/usr/bin/env bash
set -euo pipefail

API_KEY="${1:-${NUGET_API_KEY:-}}"
VERSION="${2:-}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJECT="$ROOT/src/Atomic.CodeGen/Atomic.CodeGen.csproj"
OUTPUT_DIR="$ROOT/nupkg"

if [ -z "$API_KEY" ]; then
    echo "Usage: $0 <NUGET_API_KEY> [VERSION]"
    echo "Or set NUGET_API_KEY environment variable."
    exit 1
fi

cd "$ROOT"

# Clean output
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# Build
echo "Building Release..."
dotnet build "$PROJECT" --configuration Release

# Pack
PACK_ARGS=("pack" "$PROJECT" "--configuration" "Release" "--no-build" "--output" "$OUTPUT_DIR")
if [ -n "$VERSION" ]; then
    PACK_ARGS+=("/p:Version=$VERSION")
fi
echo "Packing..."
dotnet "${PACK_ARGS[@]}"

NUPKG=$(find "$OUTPUT_DIR" -name "*.nupkg" | head -1)
echo "Package: $(basename "$NUPKG")"

# Push
echo "Pushing to NuGet.org..."
dotnet nuget push "$NUPKG" --api-key "$API_KEY" --source https://api.nuget.org/v3/index.json --skip-duplicate

echo "Published successfully!"
