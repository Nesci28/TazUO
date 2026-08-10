# OSI Vendor Search web bridge

TazUO mirrors the shard-provided Vendor Search interface at `http://localhost:8089/`. It does not search vendor data locally and does not bypass shard rules: every search, criteria change, page change that requires a server response, and vendor-map request is sent back through the active server gump.

## Usage

1. Open your character's context menu and select **Vendor Search**.
2. Click the **Web** button added to the recognized Vendor Search gump.
3. Use the browser interface. Keep TazUO running and connected while the page is open.

The native gump remains usable. Closing or replacing it invalidates the corresponding browser state, so an old browser tab cannot replay an action against a newer gump.

## Packet analysis

Vendor Search does not have a dedicated network opcode. It is an application built from the standard UO gump and object-property protocols:

| Direction | Packet | Role |
| --- | --- | --- |
| server → client | `0xB0` | Uncompressed gump: sender serial, gump/type ID, X/Y, ASCII layout, UTF-16BE text lines |
| server → client | `0xDD` | Compressed gump: the same identity and layout, with independently zlib-compressed layout and text blocks |
| server → client | `0xD6` | MegaCliloc/object property list (OPL) for each result's `itemproperty` serial |
| client → server | `0xB1` | Gump response: sender serial, gump/type ID, button ID, selected switches, and UTF-16BE text-entry values |

TazUO's packet handlers decode `0xB0` and `0xDD` into the same layout command stream before the bridge sees them. The bridge identifies the OSI-compatible stages by their title clilocs:

- `1154508`: Vendor Search Query
- `1154509`: Vendor Search Results
- `1154678`: waiting for the search to complete

The query is represented by standard `textentry`, `button`, page, and localized HTML commands. Common OSI-compatible response conventions include button `1` for search, button `2` for clearing criteria, text entry `1` for the item name, entries `7` and `8` for minimum/maximum price, and result buttons starting at `100` for creating vendor maps. The bridge does not assume that those are the only valid controls; it captures the current gump and permits only reply buttons, entries, and switches actually present in that packet.

Results combine item-art commands (`buttontileart`, `tilepic`, or OSI's `tilepicasgumppic`) with `itemproperty <serial>`. Captured OSI-compatible layouts can place `itemproperty` before or after the art. The analyzer therefore associates equal-sized art/property sets by ordinal and uses a nearest unused property fallback for custom layouts. OPL data arriving through `0xD6` supplies the browser tooltip name and property lines.

The field layouts and server-side response validation were cross-checked against TazUO's packet handlers and the [ServUO gump packet implementation](https://github.com/ServUO/ServUO/blob/pub57/Server/Network/Packets.cs) and [Vendor Search gump](https://github.com/ServUO/ServUO/blob/pub57/Scripts/Services/Vendor%20Searching/VendorSearchGump.cs).

## Local API

- `GET /api/vendor-search` returns the current recognized gump model and its version.
- `POST /api/vendor-search/respond` submits a current gump version, reply button, entries, and switches.
- `GET /api/vendor-search/art?graphic=…&hue=…` renders only item art referenced by the current result gump.

The listener binds to `localhost` only. Mutating requests are same-origin checked, request bodies and text entries are bounded, controls are allow-listed from the current gump, and all game/UI work is dispatched to TazUO's main thread. The web page inserts shard text with `textContent`, not executable HTML, and is served with a restrictive content-security policy.
