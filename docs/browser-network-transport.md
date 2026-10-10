# Browser network transport

The native client now exposes a transport seam in `AsyncNetClient` for a browser WebSocket. The
browser host owns the WebSocket; the game continues to use the existing packet queue and parser.

The JavaScript bridge should perform this sequence:

1. Call `AsyncNetClient.AttachBrowserTransport()` and open the WebSocket to the TazUO gateway.
2. Call `SetBrowserTransportConnected(true)` after the socket opens.
3. For each binary WebSocket message, call `OnBrowserDataReceived`.
4. Poll `TryDequeueBrowserOutgoing` once per browser turn and send each returned byte array as a
   binary WebSocket message.
5. On close, call `SetBrowserTransportConnected(false)` and `DetachBrowserTransport()`.

Outgoing packets pass through the existing plugin, encryption, compression, statistics, and packet
logging code before they enter the browser queue. Incoming bytes use the existing receive path and
are parsed by `PacketParser` from the normal game loop, including packets split across WebSocket
messages.

This is the integration seam for the upcoming .NET WASM host. The static POC in
`tools/legion-wasm-poc/static` proves the same gateway and binary framing with JavaScript; it does
not yet load the full game assembly.
