#!/bin/zsh
set -euo pipefail

repo_root="${0:A:h:h:h}"
if [[ ! -x /tmp/tazuo-window-id ]]; then
  swiftc "$repo_root/tools/ws/window-id.swift" -o /tmp/tazuo-window-id
fi
if [[ ! -x /tmp/tazuo-input ]]; then
  swiftc "$repo_root/tools/ws/input.swift" -o /tmp/tazuo-input
fi
window_id="${TAZUO_WINDOW_ID:-$(/tmp/tazuo-window-id 2>/dev/null || true)}"
if [[ -z "$window_id" ]]; then
  print -u2 "No TazUO game window found. Start the native client and enter the world first."
  exit 1
fi

remote_pid=""; http_pid=""
cleanup() {
  [[ -n "$remote_pid" ]] && kill "$remote_pid" 2>/dev/null || true
  [[ -n "$http_pid" ]] && kill "$http_pid" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

TAZUO_REMOTE_HOST=127.0.0.1 \
TAZUO_WINDOW_ID="$window_id" \
node "$repo_root/tools/ws/remote.mjs" &
remote_pid=$!

python3 -m http.server "${TAZUO_WEB_PORT:-8093}" \
  --bind 127.0.0.1 --directory "$repo_root/tools/legion-wasm-poc/static" &
http_pid=$!

print "Open http://127.0.0.1:${TAZUO_WEB_PORT:-8093}/?game=1&remote=1 in the iOS Simulator"
wait "$remote_pid"
