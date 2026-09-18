#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
project="${1:-${repo_root}/mobile-ios/build/MobileUO}"

[[ -f "${project}/ProjectSettings/ProjectVersion.txt" ]] || {
  echo "not a Unity project: ${project}" >&2
  exit 1
}
grep -q '6000.3.' "${project}/ProjectSettings/ProjectVersion.txt"
if [[ "${TAZUO_USE_CURRENT_SOURCE:-1}" == "1" ]]; then
  copied_build_output="$(find "${project}/Assets/Scripts/ClassicUO/src" -type d \( -name bin -o -name obj \) -print -quit)"
  [[ -z "${copied_build_output}" ]] || {
    echo "Desktop build output must not be imported into Unity: ${copied_build_output}" >&2
    exit 1
  }
  grep -R -q 'new Socket' "${project}/Assets/Scripts/ClassicUO/src"
else
  grep -q 'new Socket' "${project}/Assets/Scripts/ClassicUO/src/Network/NetClient.cs"
fi
grep -q 'TazUOMobileControls' "${project}/Assets/Scripts/TazUOMobile/TazUOMobileControls.cs"
grep -q 'BuildTarget.iOS' "${project}/Assets/Editor/TazUO/BuildIOS.cs"
grep -q 'UISupportedInterfaceOrientations' "${project}/Assets/Editor/TazUO/TazUOIOSPostBuild.cs"
grep -q 'loadPyodide' "${project}/Assets/StreamingAssets/legion/index.html"
test -s "${project}/Assets/StreamingAssets/legion/legion-smoke.py"
test -f "${project}/Assets/Plugins/iOS/TazUOLegionHost.mm"
grep -q 'ShowMobileContext' "${project}/Assets/Scripts/ClassicUO/src/Game/Scenes/GameScene.MobileControls.cs"

echo "Unity project surface validation passed: ${project}"
