#!/usr/bin/env bash
set -euo pipefail
umask 077
repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
udid="${1:-2FE53C8F-9A9D-49B6-86E9-39FD81F47102}"
app="$repo_root/mobile-ios/native/bin/Debug/net10.0-ios/iossimulator-arm64/TazUO.iOS.app"
[[ -d "$app" ]] || "$repo_root/tools/mobile-ios/build-native-simulator.sh"
xcrun simctl install "$udid" "$app"
data_container="$(xcrun simctl get_app_container "$udid" com.tazuo.ios data)"
rm -f "$data_container/Documents/native-runtime.log"
rm -f "$data_container/Documents/native-test-args.txt"
rm -f "$data_container/Documents/native-legion-smoke.request"
rm -f "$data_container/Documents/TazUO/native-legion-smoke-result.txt"
if [[ "${TAZUO_TEST_LEGION:-0}" == "1" ]]; then
  mkdir -p "$data_container/Documents/TazUO/LegionScripts"
  cp "$repo_root/tools/mobile-ios/native-legion-smoke.py" "$data_container/Documents/TazUO/LegionScripts/NativeSmokeTest.py"
  touch "$data_container/Documents/native-legion-smoke.request"
fi
if [[ -n "${TAZUO_TEST_ACCOUNT:-}" ]]; then
  test_server="${TAZUO_TEST_SERVER:-login.uocrossroads.net:2593}"
  test_host="${test_server%%:*}"
  test_port="${test_server##*:}"
  test_user="${TAZUO_TEST_ACCOUNT%%:*}"
  test_password="${TAZUO_TEST_ACCOUNT#*:}"
  printf '%s\n' \
    '-ip' "$test_host" \
    '-port' "$test_port" \
    '-username' "$test_user" \
    '-password' "$test_password" \
    '-saveaccount' 'false' \
    '-skiploginscreen' \
    '-autologin' 'true' \
    '-reconnect' 'false' \
    '-ignore_relay_ip' 'false' > "$data_container/Documents/native-test-args.txt"
fi
xcrun simctl terminate "$udid" com.tazuo.ios >/dev/null 2>&1 || true
xcrun simctl launch "$udid" com.tazuo.ios >/tmp/tazuo-native-launch.txt
sleep "${TAZUO_TEST_WAIT:-5}"
rm -f "$data_container/Documents/native-test-args.txt"
log_file="$repo_root/mobile-ios/native-runtime.log"
if [[ -f "$data_container/Documents/native-runtime.log" ]]; then
  cp "$data_container/Documents/native-runtime.log" "$log_file"
  cat "$log_file"
else
  echo "The app did not write native-runtime.log; inspect the simulator console." >&2
  exit 1
fi
if [[ -f "$data_container/Documents/native-client.log" ]]; then
  cp "$data_container/Documents/native-client.log" "$repo_root/mobile-ios/native-client.log"
  echo "Client log: $repo_root/mobile-ios/native-client.log"
fi
xcrun simctl io "$udid" screenshot "$repo_root/mobile-ios/native-runtime.png" >/dev/null
echo "Screenshot: $repo_root/mobile-ios/native-runtime.png"
if grep -q ': FAIL' "$log_file" || ! grep -q 'Legion engine: PASS' "$log_file" || ! grep -q 'Native GameController: READY' "$log_file"; then
  exit 1
fi
if [[ -n "${TAZUO_TEST_ACCOUNT:-}" ]] && ! grep -q 'Native world: READY' "$data_container/Documents/native-runtime.log"; then
  echo "The account test did not reach the in-game world." >&2
  exit 1
fi
if [[ "${TAZUO_TEST_LEGION:-0}" == "1" ]] && ! grep -q 'Legion live API: PASS' "$log_file"; then
  echo "The live Legion API smoke test did not pass." >&2
  exit 1
fi
