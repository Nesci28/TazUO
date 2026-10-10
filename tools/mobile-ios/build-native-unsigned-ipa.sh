#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
sdk_root="$repo_root/mobile-ios/.dotnet"
[[ -x "$sdk_root/dotnet" ]] || {
  echo "Missing local .NET iOS SDK. Run tools/mobile-ios/install-dotnet-ios.sh first." >&2
  exit 2
}
export DOTNET_ROOT="$sdk_root"
export PATH="$DOTNET_ROOT:$PATH"

cd "$repo_root/mobile-ios/native"
# Keep the iOS linker enabled: it generates the static runtime registrations
# and a matching set of managed assemblies, AOT data and native entry points.
"$DOTNET_ROOT/dotnet" build TazUO.iOS.csproj \
  --configuration Release --runtime ios-arm64 -m:1 -nr:false \
  -p:TAZUO_IOS=true -p:MtouchLink=None \
  -p:EnableCodeSigning=false -p:CodesignRequireProvisioningProfile=false \
  -p:UseSharedCompilation=false -p:RunGenerateDocs=false -p:NuGetAudit=false \
  -p:CustomAfterMicrosoftCommonTargets="$PWD/BuildAdapters/Platform.targets"

app="$PWD/bin/Release/net10.0-ios/ios-arm64/TazUO.iOS.app"
build_dir="$repo_root/mobile-ios/build"
mkdir -p "$build_dir"
stage="$(mktemp -d "$build_dir/unsigned-package.XXXXXX")"
trap 'rm -rf "$stage"' EXIT

# Sys's static initializer needs this exported function before managed Main.
xcrun nm -gU "$app/TazUO.iOS" > "$stage/native-symbols.txt"
if ! /usr/bin/grep -q ' _SystemNative_LChflagsCanSetHiddenFlag$' "$stage/native-symbols.txt"; then
  echo "Missing System.Native entry points in the device executable; refusing to package." >&2
  exit 1
fi

mkdir -p "$stage/Payload"
ditto --norsrc --noextattr --noqtn "$app" "$stage/Payload/TazUO.iOS.app"
uo_source="${TAZUO_UO_PATH:-/Applications/TazUO-Launcher.osx-arm64/UO}"
if [[ -d "$uo_source" && ! -d "$stage/Payload/TazUO.iOS.app/UO" ]]; then
  ditto --norsrc --noextattr --noqtn "$uo_source" "$stage/Payload/TazUO.iOS.app/UO"
fi

version="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$app/Info.plist")"
ipa="$build_dir/TazUO-$version-unsigned.ipa"
cd "$stage"
/usr/bin/zip -qry archive.ipa Payload
/usr/bin/unzip -tq archive.ipa
mv archive.ipa "$ipa"
echo "Unsigned IPA: $ipa"
