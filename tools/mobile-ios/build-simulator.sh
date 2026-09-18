#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
unity_bin="${UNITY_BIN:-}"
project="${repo_root}/mobile-ios/build/MobileUO"
xcode_dir="${repo_root}/mobile-ios/build/MobileUO/Xcode"
device_id="${TAZUO_SIMULATOR_UDID:-}"
bundle_id="${TAZUO_BUNDLE_ID:-com.tazuo.mobile}"

if [[ -z "${unity_bin}" ]]; then
  unity_bin="$(command -v Unity 2>/dev/null || true)"
fi
if [[ -z "${unity_bin}" ]]; then
  unity_bin="/Applications/Unity/Hub/Editor/6000.3.24f1/Unity.app/Contents/MacOS/Unity"
fi
[[ -x "${unity_bin}" ]] || {
  echo "Unity editor not found. Install Unity 6000.3.24f1 with iOS Build Support, then set UNITY_BIN if needed." >&2
  exit 2
}

"${repo_root}/tools/mobile-ios/fetch-pyodide.sh"
"${repo_root}/tools/mobile-ios/bootstrap.sh"
rm -rf "${xcode_dir}"
TAZUO_IOS_SIMULATOR=1 "${unity_bin}" -batchmode -quit \
  -projectPath "${project}" \
  -buildTarget iOS \
  -executeMethod TazUO.Editor.BuildIOS.Perform \
  -logFile "${repo_root}/mobile-ios/build/unity-ios.log"

xcode_project="$(find "${xcode_dir}" -maxdepth 1 -name '*.xcodeproj' -print -quit)"
[[ -n "${xcode_project}" ]] || { echo "Unity did not produce an Xcode project" >&2; exit 1; }
xcode_name="$(basename "${xcode_project}" .xcodeproj)"
xcodebuild -project "${xcode_project}" -scheme "${xcode_name}" \
  -configuration Debug -sdk iphonesimulator \
  -derivedDataPath "${repo_root}/mobile-ios/build/DerivedData" \
  CODE_SIGNING_ALLOWED=NO build

app_path="$(find "${repo_root}/mobile-ios/build/DerivedData/Build/Products" -maxdepth 2 -name '*.app' -print -quit)"
[[ -n "${app_path}" ]] || { echo "Xcode did not produce a simulator app" >&2; exit 1; }

if [[ -z "${device_id}" ]]; then
  device_id="$(xcrun simctl list devices available | awk -F '[()]' '/iPhone 17 .*Shutdown|iPhone 17 .*Booted/ { print $2; exit }')"
fi
[[ -n "${device_id}" ]] || { echo "No available iPhone simulator found" >&2; exit 1; }
xcrun simctl boot "${device_id}" 2>/dev/null || true
xcrun simctl bootstatus "${device_id}" -b
xcrun simctl install "${device_id}" "${app_path}"
xcrun simctl launch "${device_id}" "${bundle_id}"
echo "Installed ${bundle_id} on simulator ${device_id}"
