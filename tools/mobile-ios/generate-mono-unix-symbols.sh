#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
rid="${1:-iossimulator-arm64}"
version="7.1.0-final.1.21458.1"
archive="$HOME/.nuget/packages/mono.unix/$version/runtimes/$rid/native/libMono.Unix.a"
out="$repo_root/mobile-ios/build/native/$rid/MonoUnixSymbols.props"
if [[ ! -f "$archive" ]]; then
  echo "Mono.Unix archive not found: $archive" >&2
  exit 2
fi
mkdir -p "$(dirname "$out")"
{
  echo '<Project><ItemGroup>'
  nm -gU "$archive" | awk '$2 == "T" && $3 ~ /^_/ { sub(/^_/, "", $3); print $3 }' | sort -u | while IFS= read -r symbol; do
    printf '    <ReferenceNativeSymbol Include="%s" SymbolType="Function" />\n' "$symbol"
  done
  echo '</ItemGroup></Project>'
} > "$out"
