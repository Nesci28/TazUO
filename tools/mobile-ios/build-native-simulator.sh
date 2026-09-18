#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
sdk_root="$repo_root/mobile-ios/.dotnet"
if [[ ! -x "$sdk_root/dotnet" ]]; then
  echo "Missing local .NET iOS SDK. Run tools/mobile-ios/install-dotnet-ios.sh first." >&2
  exit 2
fi
export DOTNET_ROOT="$sdk_root"
export PATH="$DOTNET_ROOT:$PATH"

configuration="${CONFIGURATION:-Debug}"
rid="${RUNTIME_IDENTIFIER:-iossimulator-arm64}"
"$repo_root/tools/mobile-ios/generate-mono-unix-symbols.sh" "$rid"
cd "$repo_root/mobile-ios/native"
# The local 10.0.200 iOS workload has no arm64 MSBuild task host for the
# SDK's ComputeManagedAssemblies ILLink task. Simulator builds use explicit
# no-link mode; device/release builds keep the project defaults.
linker_options=()
if [[ "$rid" == iossimulator-* ]]; then
  linker_options=(-p:_MustTrim=false -p:PublishTrimmed= -p:MtouchLink=None)
fi
"$DOTNET_ROOT/dotnet" build TazUO.iOS.csproj \
  --configuration "$configuration" \
  -m:1 -nr:false \
  -p:UseSharedCompilation=false \
  -p:RunGenerateDocs=false \
  -p:TAZUO_IOS=true \
  "${linker_options[@]}" \
  -p:CustomAfterMicrosoftCommonTargets="$repo_root/mobile-ios/native/BuildAdapters/Platform.targets" \
  -p:RuntimeIdentifier="$rid" \
  -p:EnableCodeSigning=true \
  -p:CodesignKey=- \
  "$@"

# UO client data is user-provided and remains outside source control. Bundle it
# only for local runs when the standard launcher installation is present.
app="$repo_root/mobile-ios/native/bin/$configuration/net10.0-ios/$rid/TazUO.iOS.app"
uo_source="${TAZUO_UO_PATH:-/Applications/TazUO-Launcher.osx-arm64/UO}"
if [[ -d "$uo_source" && ! -d "$app/UO" ]]; then
  echo "Bundling UO data from $uo_source"
  ditto "$uo_source" "$app/UO"
fi
