#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
dotnet_root="${repo_root}/mobile-ios/.dotnet"
sdk_version="10.0.200"
workload_version="10.0.200"

if [[ ! -x "${dotnet_root}/dotnet" ]]; then
  installer="${TMPDIR:-/tmp}/dotnet-install-tazuo.sh"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o "${installer}"
  bash "${installer}" --version "${sdk_version}" --install-dir "${dotnet_root}" --no-path
fi

export DOTNET_ROOT="${dotnet_root}"
export PATH="${dotnet_root}:${PATH}"
# The repository global.json intentionally tracks the desktop SDK and is not
# changed for the mobile toolchain. Put a local selector beside this SDK so
# workload resolution uses the pinned 10.0.200 feature band.
cat > "${dotnet_root}/global.json" <<JSON
{"sdk":{"version":"${sdk_version}","rollForward":"disable"}}
JSON
(cd "${dotnet_root}" && "${dotnet_root}/dotnet" workload install ios --version "${workload_version}")
(cd "${dotnet_root}" && "${dotnet_root}/dotnet" workload list)
