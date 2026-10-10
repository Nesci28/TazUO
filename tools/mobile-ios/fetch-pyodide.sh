#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
destination="${repo_root}/mobile-ios/unity-overlay/legion/pyodide"
version="${PYODIDE_VERSION:-0.27.2}"
archive="/tmp/tazuo-pyodide-${version}.tar.bz2"
url="https://github.com/pyodide/pyodide/releases/download/${version}/pyodide-${version}.tar.bz2"

mkdir -p "${destination}"
if [[ -f "${destination}/pyodide.mjs" ]]; then
  echo "Pyodide ${version} already installed in ${destination}"
  exit 0
fi
curl --fail --location --retry 3 --output "${archive}" "${url}"
rm -rf "${destination}.extract"
mkdir -p "${destination}.extract"
tar -xjf "${archive}" -C "${destination}.extract"
source_dir="${destination}.extract/pyodide"
[[ -f "${source_dir}/pyodide.mjs" ]] || { echo "invalid Pyodide archive" >&2; exit 1; }
rm -rf "${destination}"
mv "${source_dir}" "${destination}"
rm -rf "${destination}.extract"
echo "Pyodide ${version} installed in ${destination}"
