# Real-client mobile bridge

The browser canvas remains a renderer/Legion contract test. The `tools/ws/remote.mjs` bridge is the
development path for the actual TazUO experience on an iOS Simulator: TazUO runs natively on the
Mac, the bridge captures only its game window, and Safari displays the JPEG stream with the mobile
controls layered above it.

1. Start the normal TazUO native client and enter the world.
2. Identify its window and start the localhost bridge:

   ```bash
   cd tools/ws
   swiftc window-id.swift -o /tmp/tazuo-window-id
   TAZUO_WINDOW_ID=$(/tmp/tazuo-window-id) node remote.mjs
   ```

3. Serve `tools/legion-wasm-poc/static` and open `/?game=1&remote=1` in the iOS Simulator.

The bridge is localhost-only by default. macOS Screen Recording and Accessibility permissions are
required. The current input path forwards joystick movement to the native client; the native
window remains authoritative for rendering, inventory, gumps, network state, and Legion scripts.
This gives the same game scene immediately while the native iOS IPA is built. It is a diagnostic
tool only: the production application under `mobile-ios/` must use direct TCP networking and its
own Unity renderer, with no macOS process or WebSocket proxy.

For a separate real-server smoke session, keep the credentials in `.env.tazuo-test` and run
`tools/ws/run-real-session.sh`. It routes the native client's TCP connection through
`127.0.0.1:2594` to the configured shard without changing packet bytes. The script reuses an
already-running local relay and writes its temporary profile under `/tmp`.

Once the native client is in the world, `tools/ws/run-mobile-bridge.sh` starts the screen bridge
and static web server together. It prints the simulator URL and cleans up both services when
stopped.
