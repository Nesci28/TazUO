# Legion WASM proof of concept

This is the first browser risk spike. It has two WebAssembly layers:

* the Blazor/.NET shell, built for `browser-wasm`;
* Pyodide, which executes the included Python Legion script.

The script exercises the Legion-shaped `API.Print`, `API.Pause`, `API.ProcessCallbacks`,
`API.OnStop`, and `API.Stop` operations. `API.Pause` returns a JavaScript promise, so Python yields
to the browser event loop instead of blocking it. This is the behavior the production
`IBrowserLegionScriptRuntime` adapter must preserve.

## Run locally

### No .NET workload required

To run the Python/WASM portion immediately, serve the `static` directory:

```bash
python3 -m http.server 8080 --directory tools/legion-wasm-poc/static
```

Open `http://localhost:8080`. This loads Pyodide from its CDN and runs the real Python script.
For an unattended smoke test, open `http://localhost:8080/?autorun=1`; the script starts on page load.
On an iPhone or iPad rotated to landscape, use `http://localhost:8080/?game=1` for the gameplay
viewport layout. The page loads a scene exported from the supplied UO data directory, keeps the
HUD tabs and character bars pinned at the top, opens the Inventory panel, and places a MobileUO-style
analog joystick plus action controls over the scene. Select Character, Inventory, Journal, Chat, World Map, or Legion Script
to open the corresponding in-game panel; `?panel=map` (for example) selects one on launch.

```bash
dotnet publish tools/legion-wasm-poc/legion-wasm-poc.csproj -c Release -o /tmp/tazuo-legion-poc
python3 -m http.server 8080 --directory /tmp/tazuo-legion-poc/wwwroot
```

Open `http://localhost:8080`, click **Run Legion script**, and verify the output contains:

```text
LEGION: script started
LEGION: OnStop callback invoked
LEGION: OnStop callback completed
LEGION: script resumed after API.Pause
LEGION: script completed
```

The Pyodide module is loaded from its CDN on first run, so the first launch requires network access.

The page also includes the mobile input proof: the analog joystick emits `move:north`,
`move:south`, `move:east`, and `move:west` with a dead zone and repeated steps; Target, Context,
and Chat emit matching actions. Pointer Events make these work with touch and iPad trackpads.
Holding a control for 500 ms emits a `longpress` phase,
which is the browser equivalent of the desktop context action. The production client will route the
same `tazuo-control` events into its existing input/action dispatcher.

The canvas uses land and static art exported by `tools/HDAssets` from `map0LegacyMUL.uop` and the
classic art files. Movement events update the player position and recenter the visible map every
animation frame. The export is intentionally small for the proof of concept; production will stream
nearby blocks and decode the remaining art/animation groups as the FNA renderer is ported.

## Real native TazUO mode

The canvas is only a browser contract test. To see the actual native client in the iOS Simulator,
start TazUO first, then run the localhost-only screen/control bridge:

```bash
cd tools/ws
swiftc window-id.swift -o /tmp/tazuo-window-id
TAZUO_WINDOW_ID=$(/tmp/tazuo-window-id) node remote.mjs
```

Serve this directory and open `/?game=1&remote=1`. Safari receives the native TazUO window,
including its real renderer, gumps, inventory, Script Manager, and Legion state, while joystick
movement is forwarded to the native process.

The **Test WebSocket gateway** button sends `01020304` to `ws://127.0.0.1:2594` and prints the
binary response. With the development proxy and a mock TCP shard running, the expected response is
`aabbcc`. Use `?network=1` to run this check automatically after page load.

`TazuoWebSocketTransport` is the browser transport seam. It uses binary `ArrayBuffer` packets,
reports `connecting/open/error/closed` states, exposes a bounded receive queue, and rejects sends
while disconnected. The production host will connect its `onPacket` callback to
`AsyncNetClient.OnBrowserDataReceived` and drain `AsyncNetClient.TryDequeueBrowserOutgoing` into
`send`. The C# transport contract and integration sequence are documented in
`docs/browser-network-transport.md`.

The shared client contract for those events is `BrowserControlEvent` and
`BrowserControlActionParser` in `src/ClassicUO.Client/Input/Browser`. It validates the action before
the game layer receives it, so arbitrary browser strings cannot become input actions.

The demo script now also calls `API.SysMsg` and `API.Walk`, showing how game actions will be mapped
from Python into the client action dispatcher. These methods currently log/emit actions; the next
adapter step will bind them to `LegionAPI.SysMsg` and `LegionAPI.Walk` against a live `World`.
