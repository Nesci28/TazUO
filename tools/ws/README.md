# Test WebSocket TCP proxy
This is a development gateway for testing browser WebSocket connections against a TCP shard.

**Do not use it in production it's not built for that.**

## Installation
Install a recent version of Node.js (tested with node v20+)

Run:
```bash
npm install
```

## Usage
- Start a shard TCP endpoint, for example `127.0.0.1:2593`.
- Run `node proxy.mjs`. The default browser endpoint is `ws://127.0.0.1:2594`.
- Override endpoints when the shard uses another address:

  ```bash
  TAZUO_WS_SOURCE=0.0.0.0:2594 TAZUO_TCP_TARGET=shard.example:2593 node proxy.mjs
  ```

- Point the browser transport at `ws://127.0.0.1:2594` (use `wss://` behind TLS in deployment).
- Set `TAZUO_PACKET_LOG=1` to print packet direction, byte count, and the first bytes for a local
  smoke test. The proxy forwards the complete binary payload unchanged.

The proxy only proves transport reachability. It does not authenticate users, enforce browser
origins, rate-limit clients, or translate packets; those are required before production use.

## Real TazUO screen bridge

To reproduce the native TazUO experience in Safari during development, run the native client on
the Mac and start:

```bash
swiftc window-id.swift -o /tmp/tazuo-window-id
TAZUO_WINDOW_ID=$(/tmp/tazuo-window-id) node remote.mjs
```

If the native client's MCP bridge is enabled, set `TAZUO_MCP_URL` (and its bearer token, when
configured) as well. Joystick walking and chat then call the native `LegionAPI` (`Walk`/`Msg`), so
the normal client packet path is used:

```bash
TAZUO_WINDOW_ID=$(/tmp/tazuo-window-id) \
TAZUO_MCP_URL=http://127.0.0.1:8088/api/mcp \
TAZUO_MCP_TOKEN="$TAZUO_MCP_TOKEN" node remote.mjs
```

This captures the native display and streams JPEG frames over `ws://127.0.0.1:2595` (localhost
only by default). Open the
browser POC with `/?game=1&remote=1` to display that stream. Movement actions are forwarded to the
foreground TazUO process through macOS accessibility events. The terminal needs Screen Recording
and Accessibility permission. This bridge lets the iOS simulator exercise the real renderer,
gumps, network state, and Legion runtime while a native iOS renderer is prepared.

Taps on the streamed game view are also forwarded to the native window. Set `TAZUO_WINDOW_X`,
`TAZUO_WINDOW_Y`, `TAZUO_WINDOW_WIDTH`, and `TAZUO_WINDOW_HEIGHT` when the game window is not at
the default 2498×1415 position used by the launcher.

For a physical iPhone, provide TLS certificates and a shared token through the environment:

```bash
TAZUO_REMOTE_HOST=192.168.1.20 \
TAZUO_TLS_CERT=/path/fullchain.pem TAZUO_TLS_KEY=/path/privkey.pem \
TAZUO_REMOTE_TOKEN='replace-with-a-long-random-value' \
TAZUO_WINDOW_ID=$(/tmp/tazuo-window-id) node remote.mjs
```

Serve the web client over HTTPS and open it with `remoteSecure=1&remoteToken=...`. Without these
variables the bridge remains loopback-only and unauthenticated for local simulator development.
