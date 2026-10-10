#!/bin/zsh
set -euo pipefail

repo_root="${0:A:h:h:h}"
env_file="$repo_root/.env.tazuo-test"
if [[ ! -f "$env_file" ]]; then
  print -u2 "Missing $env_file"
  exit 1
fi
source "$env_file"
: "${TAZUO_ACCOUNT:?TAZUO_ACCOUNT is required}"
: "${TAZUO_PASSWORD:?TAZUO_PASSWORD is required}"
: "${TAZUO_SERVER_IP:?TAZUO_SERVER_IP is required}"
: "${TAZUO_SERVER_PORT:?TAZUO_SERVER_PORT is required}"

proxy_pid=""
cleanup() {
  [[ -n "$proxy_pid" ]] && kill "$proxy_pid" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

if nc -z 127.0.0.1 2594 2>/dev/null; then
  print "Using existing TazUO relay on 127.0.0.1:2594"
else
  TAZUO_PACKET_LOG=1 \
  TAZUO_WS_SOURCE="127.0.0.1:2594" \
  TAZUO_TCP_TARGET="$TAZUO_SERVER_IP:$TAZUO_SERVER_PORT" \
  node "$repo_root/tools/ws/proxy.mjs" &
  proxy_pid=$!
  sleep 1
fi

profile_dir="${TAZUO_TEST_PROFILE_DIR:-/tmp/tazuo-real-test-profiles}"
mkdir -p "$profile_dir"
settings_file="$profile_dir/settings.json"
cat >"$settings_file" <<JSON
{"username":"$TAZUO_ACCOUNT","password":"$TAZUO_PASSWORD","ip":"127.0.0.1","port":2594,"ultimaonlinedirectory":"/Applications/TazUO-Launcher.osx-arm64/UO","profilespath":"$profile_dir","skipserverselect":true,"skiploginscreen":true}
JSON

TAZUO_MCP_AUTOSTART=1 TAZUO_MCP_PORT="${TAZUO_MCP_PORT:-8098}" \
/Applications/TazUO-Launcher.osx-arm64/TazUO/TazUO \
  -settings "$settings_file" \
  -ip 127.0.0.1 -port 2594 -uopath /Applications/TazUO-Launcher.osx-arm64/UO \
  -profilespath "$profile_dir"
