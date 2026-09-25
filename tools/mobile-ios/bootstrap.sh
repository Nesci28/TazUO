#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
build_root="${repo_root}/mobile-ios/build"
project_dir="${build_root}/MobileUO"
mobileuo_url="https://github.com/MobileUO/MobileUO.git"
mobileuo_commit="1ae20dcc0ff6c01e7eb86844a6364b127d766571"
overlay_dir="${repo_root}/mobile-ios/unity-overlay"
mobileuo_source="${TAZUO_MOBILEUO_SOURCE:-}"
tazuo_source="${TAZUO_CURRENT_SOURCE:-${repo_root}/src/ClassicUO.Client}"

if [[ ! -f "${overlay_dir}/legion/pyodide/pyodide.mjs" && "${TAZUO_SKIP_PYODIDE:-0}" != "1" ]]; then
  echo "Pyodide is missing. Run tools/mobile-ios/fetch-pyodide.sh first (or set TAZUO_SKIP_PYODIDE=1 for source-only validation)." >&2
  exit 2
fi

mkdir -p "${build_root}"
if [[ ! -d "${project_dir}/.git" ]]; then
  if [[ -n "${mobileuo_source}" ]]; then
    cp -R "${mobileuo_source}" "${project_dir}"
  else
    git clone --recurse-submodules "${mobileuo_url}" "${project_dir}"
  fi
fi

if [[ -z "${mobileuo_source}" ]]; then
  git -C "${project_dir}" fetch --quiet origin "${mobileuo_commit}"
fi
git -C "${project_dir}" checkout --quiet "${mobileuo_commit}"
if [[ "${TAZUO_USE_CURRENT_SOURCE:-1}" != "1" ]]; then
  # Remove files left by a previous current-TazUO parity attempt before
  # restoring the pinned fixture. This only operates inside the generated
  # Unity checkout, never the repository source tree.
  git -C "${project_dir}" reset --hard --quiet "${mobileuo_commit}"
  git -C "${project_dir}" clean -fdq
fi
if [[ -z "${mobileuo_source}" ]]; then
  git -C "${project_dir}" submodule update --init --recursive
fi

# MobileUO stores its SemVer metadata in bundleVersion (for example
# 0.9.0+1). Apple rejects the '+' suffix, so normalize the generated Unity
# project before the iOS build starts. BuildIOS.cs also sets this at runtime,
# but keeping the project file valid lets Unity import it cleanly.
python3 - "${project_dir}/ProjectSettings/ProjectSettings.asset" <<'PY'
from pathlib import Path
import sys
path = Path(sys.argv[1])
text = path.read_text()
text = text.replace("  bundleVersion: 0.9.0+1", "  bundleVersion: 0.9.0")
path.write_text(text)
PY

# MobileUO's pre-build hook appends "+<build>" to the Unity bundle version.
# That is useful for desktop SemVer but invalid for Apple's short bundle
# version. Keep the numeric base version on iOS.
python3 - "${project_dir}/Assets/Editor/PreBuildScript/PreBuildScript.cs" <<'PY'
from pathlib import Path
import sys
path = Path(sys.argv[1])
if path.exists():
    text = path.read_text()
    text = text.replace('string fullVersionNumber = $"{baseVersionNumber}+{buildNumber}";',
                        'string fullVersionNumber = baseVersionNumber;')
    path.write_text(text)
PY

mkdir -p "${project_dir}/Assets/Scripts/TazUOMobile"
mkdir -p "${project_dir}/Assets/Scripts/ClassicUO/src/Utility"
mkdir -p "${project_dir}/Assets/Scripts/ClassicUO/src/IO/Resources"
mkdir -p "${project_dir}/Assets/Scripts/ClassicUO/src/Game/Scenes"
mkdir -p "${project_dir}/Assets/Editor/TazUO"
mkdir -p "${project_dir}/Assets/Plugins/iOS"
mkdir -p "${project_dir}/Assets/StreamingAssets/legion"
mkdir -p "${project_dir}/Assets/StreamingAssets/legion/pyodide"
cp "${overlay_dir}/TazUOMobileControls.cs" "${project_dir}/Assets/Scripts/TazUOMobile/"
cp "${overlay_dir}/TazUOInputDiagnostics.cs" "${project_dir}/Assets/Scripts/TazUOMobile/"
cp "${overlay_dir}/TazUOLegionHost.cs" "${project_dir}/Assets/Scripts/TazUOMobile/"
cp "${overlay_dir}/GameScene.MobileControls.cs" "${project_dir}/Assets/Scripts/ClassicUO/src/Game/Scenes/"

# Use the repository's current TazUO source by default. The pinned MobileUO
# fixture remains available only with TAZUO_USE_CURRENT_SOURCE=0.
if [[ "${TAZUO_USE_CURRENT_SOURCE:-1}" == "1" ]]; then
  [[ -d "${tazuo_source}" ]] || { echo "TazUO source tree not found: ${tazuo_source}" >&2; exit 2; }
  rm -rf "${project_dir}/Assets/Scripts/ClassicUO/src"
  mkdir -p "${project_dir}/Assets/Scripts/ClassicUO/src"
  mkdir -p "${project_dir}/Assets/Scripts/ClassicUO/src/Utility"
  mkdir -p "${project_dir}/Assets/Scripts/ClassicUO/src/IO/Resources"
  mkdir -p "${project_dir}/Assets/Scripts/ClassicUO/src/Game/Scenes"
  # Desktop build outputs are not Unity plugins. In particular, copying
  # bin/obj imports stale TazUO/FNA assemblies into every Unity assembly and
  # masks source errors with type conflicts in Unity's own editor packages.
  copy_tazuo_sources() {
    rsync -a --exclude='bin/' --exclude='obj/' --exclude='.git/' \
      --exclude='*.csproj' --exclude='*.csproj.*' --exclude='*.sln' \
      --exclude='*.user' "$1/" "${project_dir}/Assets/Scripts/ClassicUO/src/"
  }
  copy_tazuo_sources "${tazuo_source}"
  # The current TazUO client is split across these source projects. Unity
  # compiles them as one script assembly, so bring their source trees along
  # while leaving project files and desktop launchers out of the export.
  for dependency in ClassicUO.Utility ClassicUO.Assets ClassicUO.Renderer ClassicUO.CUOAPI ClassicUO.IO; do
    dependency_dir="${repo_root}/src/${dependency}"
    if [[ -d "${dependency_dir}" ]]; then
      copy_tazuo_sources "${dependency_dir}"
    fi
  done
  cp "${overlay_dir}/GameScene.MobileControls.cs" "${project_dir}/Assets/Scripts/ClassicUO/src/Game/Scenes/"
  echo "Using current TazUO source tree: ${tazuo_source}"
fi
cp "${overlay_dir}/BwtDecompress.cs" "${project_dir}/Assets/Scripts/ClassicUO/src/Utility/"
cp "${overlay_dir}/GumpPixels.cs" "${project_dir}/Assets/Scripts/ClassicUO/src/Utility/"
cp "${overlay_dir}/GumpsLoader.cs" "${project_dir}/Assets/Scripts/ClassicUO/src/IO/Resources/"
if [[ "${TAZUO_USE_CURRENT_SOURCE:-1}" != "1" ]]; then
  python3 "${repo_root}/tools/mobile-ios/patch-asset-loaders.py" "${project_dir}"
fi

# Current UO distributions store Cliloc in BWT-compressed form (marker 0x8e),
# while this MobileUO revision's loader expects the uncompressed payload.
if [[ "${TAZUO_USE_CURRENT_SOURCE:-1}" != "1" ]]; then
python3 - "${project_dir}/Assets/Scripts/ClassicUO/src/IO/Resources/ClilocLoader.cs" <<'PYCLILOC'
from pathlib import Path
import sys
p = Path(sys.argv[1])
s = p.read_text()
old = '''                using (BinaryReader reader = new BinaryReader(new FileStream(path, FileMode.Open, FileAccess.Read)))
                {
                    reader.ReadInt32();'''
new = '''                byte[] compressed = File.ReadAllBytes(path);
                byte[] bytes = compressed.Length > 3 && compressed[3] == 0x8E
                    ? ClassicUO.Utility.BwtDecompress.Decompress(compressed)
                    : compressed;
                using (BinaryReader reader = new BinaryReader(new MemoryStream(bytes)))
                {
                    reader.ReadInt32();'''
if old not in s and new not in s:
    raise SystemExit('ClilocLoader pattern not found')
p.write_text(s.replace(old, new))
PYCLILOC
fi

# Init initializes preferences early (-500), but entering the saved server in
# Awake runs before the dispatcher's and ClientRunner's Awake methods. Defer
# only the state transition until every scene component has initialized.
python3 - "${project_dir}/Assets/Scripts/Init.cs" <<'PYINIT'
from pathlib import Path
import sys
p = Path(sys.argv[1])
s = p.read_text()
old = '        StateManager.GoToState<BootState>();'
if 'private void Start()' not in s:
    if old not in s:
        raise SystemExit('Init boot transition not found')
    s = s.replace(old, '')
    s = s.replace('    private IEnumerator FetchUpdatedSupportedServerConfigurations()',
                  '    private void Start()\n    {\n        StateManager.GoToState<BootState>();\n    }\n\n    private IEnumerator FetchUpdatedSupportedServerConfigurations()')
p.write_text(s)
PYINIT
cp "${overlay_dir}/BuildIOS.cs" "${project_dir}/Assets/Editor/TazUO/"
cp "${overlay_dir}/TazUOIOSPostBuild.cs" "${project_dir}/Assets/Editor/TazUO/"
cp "${overlay_dir}/TazUOLegionHost.mm" "${project_dir}/Assets/Plugins/iOS/"
cp "${overlay_dir}/legion/index.html" "${project_dir}/Assets/StreamingAssets/legion/"
cp "${repo_root}/mobile-ios/legion-smoke.py" "${project_dir}/Assets/StreamingAssets/legion/"

if [[ -f "${overlay_dir}/legion/pyodide/pyodide.mjs" ]]; then
  cp -R "${overlay_dir}/legion/pyodide/." "${project_dir}/Assets/StreamingAssets/legion/pyodide/"
fi

printf '%s\n' "Generated Unity project: ${project_dir}"
printf '%s\n' "Open it with Unity 6000.3.24f1 and select iOS as the build target."
"${repo_root}/tools/mobile-ios/validate-unity-project.sh" "${project_dir}"
