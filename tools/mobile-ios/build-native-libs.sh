#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
rid="${1:-iossimulator-arm64}"
case "$rid" in
  iossimulator-arm64) sdk=iphonesimulator ;;
  ios-arm64) sdk=iphoneos ;;
  *) echo "Unsupported iOS runtime: $rid" >&2; exit 2 ;;
esac

sdl_version=3.4.14
sdl_sha256=30d4aa2b3037718142b32dffd4e72f917ebb6cc5227150e7bb9c45efb2153aeb
source_dir="$repo_root/mobile-ios/build/sources"
out="$repo_root/mobile-ios/build/native/$rid"
sdl_source="$source_dir/SDL3-$sdl_version"
archive="$source_dir/SDL3-$sdl_version.tar.gz"
mkdir -p "$source_dir" "$out/lib"
if [[ ! -f "$archive" ]]; then
  curl --fail --location --retry 2 "https://www.libsdl.org/release/SDL3-$sdl_version.tar.gz" -o "$archive"
fi
printf '%s  %s\n' "$sdl_sha256" "$archive" | shasum -a 256 --check
if [[ ! -d "$sdl_source" ]]; then
  tar -xzf "$archive" -C "$source_dir"
fi

# SDL's UIKit bridge used SDL_HasKeyboard() to decide whether to synthesize
# backspace events. iOS simulators report a keyboard device even when the
# software keyboard is active, so deletion was dropped from login fields.
# Only suppress the synthetic event while a physical key is actually held.
uikit_controller="$sdl_source/src/video/uikit/SDL_uikitviewcontroller.m"
if [[ -f "$uikit_controller" ]]; then
  sed -i '' 's/matchLength < committedText.length && !SDL_HasKeyboard()/matchLength < committedText.length \&\& !SDL_HardwareKeyboardKeyPressed()/g' "$uikit_controller"
fi

cmake_args=(-G Xcode -DCMAKE_SYSTEM_NAME=iOS -DCMAKE_OSX_SYSROOT="$sdk"
  -DCMAKE_OSX_ARCHITECTURES=arm64 -DCMAKE_OSX_DEPLOYMENT_TARGET=15.0)
cmake -S "$sdl_source" -B "$out/SDL3" "${cmake_args[@]}" \
  -DSDL_SHARED=OFF -DSDL_STATIC=ON -DSDL_TESTS=OFF -DSDL_EXAMPLES=OFF \
  -DSDL_VULKAN=OFF -DSDL_METAL=ON
cmake --build "$out/SDL3" --config Release --parallel "${BUILD_JOBS:-4}" -- CODE_SIGNING_ALLOWED=NO
sdl_lib="$out/SDL3/Release-$sdk/libSDL3.a"
cp "$sdl_lib" "$out/lib/libSDL3.a"

cmake -S "$repo_root/mobile-ios/native/NativeLibraries" -B "$out/FNA" "${cmake_args[@]}" \
  -DTAZUO_ROOT="$repo_root" -DSDL3_SOURCE="$sdl_source" -DSDL3_ARCHIVE="$sdl_lib"
cmake --build "$out/FNA" --config Release --parallel "${BUILD_JOBS:-4}" -- CODE_SIGNING_ALLOWED=NO
for name in FNA3D mojoshader FAudio; do
  cp "$out/FNA/lib/Release/lib$name.a" "$out/lib/lib$name.a"
done

# Managed DllImports resolve against the executable on Mono/iOS. Export only
# the public APIs that the managed FNA bindings use, not every internal symbol.
{
  echo '<Project><ItemGroup>'
  nm -gU "$out"/lib/libSDL3.a "$out"/lib/libFNA3D.a "$out"/lib/libmojoshader.a | awk '$2 == "T" && $3 ~ /^_(SDL_|FNA3D_)/ { sub(/^_/, "", $3); print $3 }' | sort -u | while IFS= read -r symbol; do
    printf '  <ReferenceNativeSymbol Include="%s" SymbolType="Function" />\n' "$symbol"
  done
  echo '</ItemGroup></Project>'
} > "$out/FnaSymbols.props"
echo "Native libraries ready: $out/lib"
