#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
sdk_root="$repo_root/mobile-ios/.dotnet"
archive_path="${repo_root}/mobile-ios/build/native/TazUO.xcarchive"
publish_dir="${repo_root}/mobile-ios/native/bin/Release/net10.0-ios/ios-arm64"

[[ -x "$sdk_root/dotnet" ]] || {
  echo "Missing local .NET iOS SDK. Run tools/mobile-ios/install-dotnet-ios.sh first." >&2
  exit 2
}
[[ -n "${DEVELOPMENT_TEAM:-}" ]] || {
  echo "Set DEVELOPMENT_TEAM to archive a signed device IPA." >&2
  exit 2
}
export DOTNET_ROOT="$sdk_root"
export PATH="$DOTNET_ROOT:$PATH"

"$repo_root/tools/mobile-ios/build-native-libs.sh" ios-arm64
"$repo_root/tools/mobile-ios/generate-mono-unix-symbols.sh" ios-arm64
rm -rf "$archive_path" "$publish_dir"
cd "$repo_root/mobile-ios/native"
"$DOTNET_ROOT/dotnet" publish TazUO.iOS.csproj \
  --configuration Release --runtime ios-arm64 -m:1 -nr:false \
  -p:TAZUO_IOS=true -p:ArchiveOnBuild=false -p:BuildIpa=false \
  -p:EnableCodeSigning=true -p:CodesignKey="${CODESIGN_KEY:-Apple Development}" \
  -p:CodesignProvision="${CODESIGN_PROVISION:-}" \
  -p:DevelopmentTeam="$DEVELOPMENT_TEAM" \
  -p:CustomAfterMicrosoftCommonTargets="$repo_root/mobile-ios/native/BuildAdapters/Platform.targets" \
  -p:RunGenerateDocs=false

app="$publish_dir/TazUO.iOS.app"
uo_source="${TAZUO_UO_PATH:-/Applications/TazUO-Launcher.osx-arm64/UO}"
if [[ -d "$uo_source" && -d "$app" && ! -d "$app/UO" ]]; then
  ditto "$uo_source" "$app/UO"
fi

# Package after the player data has been copied into the app bundle. This
# keeps the same legal, user-provided UO data layout as the simulator build.
"$DOTNET_ROOT/dotnet" publish TazUO.iOS.csproj \
  --configuration Release --runtime ios-arm64 --no-build -m:1 -nr:false \
  -p:TAZUO_IOS=true -p:BuildIpa=true -p:ArchiveOnBuild=false \
  -p:EnableCodeSigning=true -p:CodesignKey="${CODESIGN_KEY:-Apple Development}" \
  -p:CodesignProvision="${CODESIGN_PROVISION:-}" -p:DevelopmentTeam="$DEVELOPMENT_TEAM" \
  -p:CustomAfterMicrosoftCommonTargets="$repo_root/mobile-ios/native/BuildAdapters/Platform.targets" \
  -p:RunGenerateDocs=false

ipa="$(find "$publish_dir" -maxdepth 1 -name '*.ipa' -print -quit 2>/dev/null || true)"
if [[ -z "$ipa" ]]; then
  ipa="$(find "$repo_root/mobile-ios/native/bin/Release" -name '*.ipa' -print -quit 2>/dev/null || true)"
fi
[[ -n "$ipa" ]] || { echo "Build succeeded but no IPA was produced; inspect the publish log." >&2; exit 1; }
echo "IPA: $ipa"
