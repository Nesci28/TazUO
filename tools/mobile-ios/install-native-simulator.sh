#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
udid="${1:-2FE53C8F-9A9D-49B6-86E9-39FD81F47102}"
app="$repo_root/mobile-ios/native/bin/Debug/net10.0-ios/iossimulator-arm64/TazUO.iOS.app"
[[ -d "$app" ]] || "$repo_root/tools/mobile-ios/build-native-simulator.sh"
# Reuse the local developer-only credentials file used by the real-session
# smoke test. It is ignored by git and is never copied into the app bundle.
if [[ -f "$repo_root/.env.tazuo-test" ]]; then
  source "$repo_root/.env.tazuo-test"
  : "${TAZUO_TEST_USERNAME:=${TAZUO_ACCOUNT:-}}"
  : "${TAZUO_TEST_PASSWORD:=${TAZUO_PASSWORD:-}}"
  : "${TAZUO_TEST_SERVER:=${TAZUO_SERVER_IP:-login.uocrossroads.net}:${TAZUO_SERVER_PORT:-2593}}"
fi
xcrun simctl install "$udid" "$app"
xcrun simctl terminate "$udid" com.tazuo.ios >/dev/null 2>&1 || true

# Keep test credentials outside the repository.  The simulator test account
# defaults to `nesci` when no local test environment is present; override it
# with TAZUO_TEST_USERNAME or use TAZUO_TEST_ACCOUNT for a complete
# `user:password` pair. TAZUO_TEST_ACCOUNT is consumed only for this simulator
# install; the temporary argument file is deleted by the native host after it
# has loaded the settings.
data_container="$(xcrun simctl get_app_container "$udid" com.tazuo.ios data)"
test_args="$data_container/Documents/native-test-args.txt"
rm -f "$test_args"
if [[ "${TAZUO_SKIP_TEST_PREFILL:-0}" != "1" ]]; then
  test_server="${TAZUO_TEST_SERVER:-login.uocrossroads.net:2593}"
  test_host="${test_server%%:*}"
  test_port="${test_server##*:}"
  if [[ -n "${TAZUO_TEST_ACCOUNT:-}" ]]; then
    test_user="${TAZUO_TEST_ACCOUNT%%:*}"
    test_password="${TAZUO_TEST_ACCOUNT#*:}"
    if [[ -z "$test_user" || "$test_user" == "$TAZUO_TEST_ACCOUNT" ]]; then
      echo "TAZUO_TEST_ACCOUNT must use the format user:password" >&2
      exit 2
    fi
  else
    test_user="${TAZUO_TEST_USERNAME:-nesci}"
    test_password="${TAZUO_TEST_PASSWORD:-}"
  fi
  printf '%s\n' \
    '-ip' "$test_host" \
    '-port' "$test_port" \
    '-username' "$test_user" \
    '-password' "$test_password" \
    '-saveaccount' 'true' \
    '-autologin' 'false' \
    '-reconnect' 'false' \
    '-ignore_relay_ip' 'false' > "$test_args"
fi
xcrun simctl launch "$udid" com.tazuo.ios
