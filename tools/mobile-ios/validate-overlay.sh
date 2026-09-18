#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
overlay="${repo_root}/mobile-ios/unity-overlay"

required=(
  "${overlay}/TazUOMobileControls.cs"
  "${overlay}/GameScene.MobileControls.cs"
  "${overlay}/BuildIOS.cs"
  "${overlay}/TazUOIOSPostBuild.cs"
  "${overlay}/TazUOLegionHost.cs"
  "${overlay}/TazUOLegionHost.mm"
  "${overlay}/legion/index.html"
)
for file in "${required[@]}"; do
  [[ -s "${file}" ]] || { echo "missing overlay file: ${file}" >&2; exit 1; }
done

grep -q 'BuildTarget.iOS' "${overlay}/BuildIOS.cs"
grep -q 'com.tazuo.mobile' "${overlay}/BuildIOS.cs"
grep -q 'UISupportedInterfaceOrientations' "${overlay}/TazUOIOSPostBuild.cs"
grep -q 'TazUO_LegionStart' "${overlay}/TazUOLegionHost.mm"
grep -q 'loadPyodide' "${overlay}/legion/index.html"
grep -q 'SetMobileDirection' "${overlay}/GameScene.MobileControls.cs"
grep -q 'TargetManager.SetTargeting' "${overlay}/GameScene.MobileControls.cs"
grep -q 'GameActions.Say' "${overlay}/GameScene.MobileControls.cs"
grep -q 'ContextMenuControl' "${overlay}/GameScene.MobileControls.cs"
if grep -R -n --exclude-dir=pyodide -E 'WebSocket|remote\.mjs|127\.0\.0\.1' "${overlay}"; then
  echo "the iOS runtime overlay must not depend on the browser bridge" >&2
  exit 1
fi

if [[ -d "${overlay}/legion/pyodide" ]]; then
  [[ -f "${overlay}/legion/pyodide/pyodide.mjs" ]] || {
    echo "Pyodide directory is incomplete" >&2
    exit 1
  }
fi

echo "TazUO iOS overlay validation passed"
