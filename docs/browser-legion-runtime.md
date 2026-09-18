# Browser Legion runtime

The desktop Legion implementation owns an IronPython or Roslyn engine and executes each script on
an operating-system thread. That model cannot be used by the iOS browser client: a script that
blocks the WebAssembly thread blocks rendering and browser input as well.

`BrowserLegionScriptScheduler` defines the replacement boundary. A browser runtime implements
`IBrowserLegionScriptRuntime` and is pumped by the game loop:

```text
load source -> start -> pump (bounded slice) -> yield/wait -> pump -> complete/stop
```

Each `PumpAsync` call must return control to JavaScript when a script waits, processes callbacks, or
reaches its requested budget. The scheduler serializes all script pumps, which works on iOS without
WASM shared-memory threads. It measures each call independently and terminates a runtime that does
not return before the watchdog limit.

The first browser adapter should host Python in a JavaScript/WASM runtime such as Pyodide. It must
provide a compatibility module that exposes the existing `LegionAPI` surface and translates browser
events into API callbacks. It must not expose DOM, arbitrary JavaScript, local files, or unrestricted
network access to scripts. Script packages should be loaded by the browser content provider and
identified by their stable Legion relative path.

The current desktop `LegionScripting` class remains unchanged until the adapter passes the browser
proof-of-concept suite. The unit tests in `BrowserLegionScriptSchedulerTests` specify the minimum
lifecycle and watchdog behavior required by that adapter.
