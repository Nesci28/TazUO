# HD External Images and conversion pipeline

This implementation lets `art`, `gump`, `land`, `texmap`, and body-animation overrides use a 2x or 4x source image while retaining the original UO asset's logical size, placement, and hit area. The bundled conversion tool can extract those categories directly from an installed Ultima Online Classic client and generate a complete Upscayl pack.

## File naming

Place loose PNG or BMP files under the normal external-image folders and add an explicit scale suffix:

```text
ExternalImages/
  art/
    0x0EED@2x.png
    3921@4x.png
  gumps/
    0x0834@2x.png
    2100@4x.png
  land/
    0x0003@4x.png
  texmaps/
    0x0003@4x.png
  animations/
    0x0190/          # body ID
      0/             # action/group
        1/           # canonical direction (0-4)
          0@4x.png   # frame
          Mob 0x0190-1@4x.png
```

Both decimal and hexadecimal IDs are accepted. Art filenames use the UO item/static ID as shown by UOFiddler; do not add the internal `0x4000` art offset. Land filenames use the Land Tile ID. Texmap filenames use the Texture/TexID, which is not guaranteed to be the same as the Land Tile ID.

Animation folders and filenames also accept decimal or hexadecimal numbers. A frame may be named only by its number (`0@4x.png`) or retain UOFiddler's `Mob <body>-<frame>`/`Equipment <body>-<frame>` form. The containing directories provide the body, selected action, and direction that UOFiddler omits from its exported filename.

Files without an `@2x` or `@4x` suffix retain the existing one-to-one replacement behaviour.

## In-game display modes

The **Use 2x assets** option is under Video → Post-processing and is applied on the next client
start. Its display-mode selector provides three behaviours:

- **Same size** keeps original UO logical dimensions and field of view. HD pixels are filtered into
  the original on-screen footprint.
- **Native world** renders world content at 200% while leaving the UI at its normal size. At the
  default 100% camera zoom, a tagged `@2x` world sprite is sampled close to one source pixel per
  output pixel. The visible world width and height are each roughly halved.
- **HiDPI** asks FNA/SDL for a high-pixel-density window before graphics initialization. TazUO then
  renders world targets, classic gumps, Myra windows, text, and the cursor using the physical-to-
  logical window pixel ratio. On a 2x Retina display this retains the normal layout and field of
  view while mapping `@2x` assets close to their native pixel dimensions.
- **HiDPI Balanced (1.5x)** keeps the HiDPI layout and high-density window but caps TazUO's render
  density at 1.5x. On a 2x Retina display this renders 2.25 pixels per logical pixel instead of 4,
  reducing screen-composition, world-target, and light-target pixels by 43.75% while retaining more
  HD detail than Same size. TazUO composes a complete 1.5x screen target and performs one linear
  upscale to the native 2x drawable during presentation.

If the display reports a 1x pixel density, HiDPI safely behaves like Same size. Native world does
not require a Retina/HiDPI display. Both HiDPI modes require a restart because the high-density SDL
window must be requested before graphics initialization.

## Required dimensions

The HD canvas must be an exact multiple of the original asset canvas:

| Original | `@2x` | `@4x` |
| --- | --- | --- |
| 40 x 60 | 80 x 120 | 160 x 240 |

Do not trim transparent borders after upscaling. TazUO compares the replacement dimensions with the original asset at first use. A mismatch is logged and the original UO asset is used instead.

An HD image larger than the current 4096 x 4096 atlas page is also rejected.

## Automated complete conversion

The macOS pipeline reads UOP/MUL files directly, so UOFiddler is not required for the complete pack:

```bash
python3 tools/HDAssets/run_pipeline.py \
  --uo "/Applications/TazUO-Launcher.osx-arm64/Ultima Online Classic" \
  --tazuo-bin "/Applications/TazUO-Launcher.osx-arm64/TazUO" \
  --work "/Applications/TazUO-Launcher.osx-arm64/HDAssetsWork" \
  --output "/Applications/TazUO-Launcher.osx-arm64/TazUO/ExternalImages" \
  --scale 2 \
  --model high-fidelity-4x
```

The tool exports original PNGs, fills transparent RGB with nearby sprite colors, and packs the images into padded 1024-pixel sheets. The official `upscayl-ncnn` backend processes those sheets with bounded GPU tiles. The finalizer then splits them, restores the original alpha and partial-hue masks, writes the exact `@2x`/`@4x` paths, and validates every output dimension against the extraction manifest.

After validation, the pipeline writes `tuoassets.hdpack` beside `ExternalImages`. Its compact binary
index maps each asset type and ID directly to the unchanged encoded PNG bytes. At startup TazUO
reads only this index; image bytes and pixels are loaded on demand. Categories present in the pack
are not recursively scanned in the loose `ExternalImages` tree, while categories absent from the
pack continue to use loose files normally. A missing or invalid pack logs an error and falls back to
the loose-file path.

The loose files remain on disk for validation and safe pipeline resume, but they are ignored for a
category represented by the pack. Use `--skip-hdpack` to disable pack generation or `--hdpack` to
select its output path. Existing finalized images can also be packed directly with the HDAssets
tool's `pack --work ... --input ... --output ...` command; no new upscale pass is required.

The hdpack path is optimized for a locally generated, trusted pack. The runtime validates the
container header, sorted index, offsets, and entry bounds, then trusts the pipeline's dimension and
mask validation. It therefore skips per-image CRC recalculation, original-asset comparison, and
runtime mask reconstruction. Independent offset-based reads allow multiple worker threads to read
different entries concurrently. Loose files and ZIP replacements continue through the defensive
validation path.

For a bounded disk footprint, finalization records a durable per-sheet completion marker and removes the large upscaled sheet only after every asset in it was written successfully. A resumed run recognizes those markers, so it neither re-upscales nor re-finalizes completed sheets.

A complete 2x pass is the practical default. It uses one quarter of the output pixels of 4x while covering the same assets; 4x is best reserved for a machine with substantially more free disk space. The detailed options and resume behavior are documented in [`tools/HDAssets/README.md`](../tools/HDAssets/README.md).

## Suggested manual demonstration

1. In UOFiddler, export a small selection of Items, Gumps, Land Tiles, and Textures as PNG.
2. Preserve the PNG alpha channel and original canvas.
3. Upscale with Upscayl using `Digital Art 4x` or `High Fidelity 4x`.
4. Rename the output to the numeric ID plus `@4x`, removing UOFiddler's `Item `, `Gump `, `LandTile `, or `Texture ` prefix.
5. Copy it to the corresponding `ExternalImages/art`, `gumps`, `land`, or `texmaps` folder beside the TazUO executable.
6. Restart the client; external images are indexed at startup.

For a 2x experiment, resize the 4x result to exactly twice the original dimensions before using the `@2x` suffix.

For an animation experiment:

1. Select a body/equipment ID, action, and facing in UOFiddler's Animation tab.
2. Use **Export Animation → PNG**; this produces one `Mob` or `Equipment` file per frame.
3. Upscale every frame without cropping and append `@4x` before `.png`.
4. Place the frames under `animations/<body>/<action>/<direction>/`.
5. Start with one complete direction. Replaced and original frames can coexist, but completing a whole action avoids visible resolution changes while it plays.

Current UOFiddler animation PNG export paints the transparent canvas white. TazUO reconstructs the binary alpha silhouette from the corresponding original frame at load time. It also restores which source pixels are grayscale so partial equipment hues continue to work. Frame centers are retained from the original animation data, so no sidecar metadata is required.

## Coverage

Implemented:

- normal world item/static rendering, anchoring, shadows, outlines, and pixel selection;
- common static-art UI rendering;
- common gump pictures, buttons, item gumps, cropped gump pictures, and tiled gumps;
- UO `ResizePic` nine-part panels, including tiled edges, tiled centers, and pixel selection;
- composed UI controls such as checkboxes, expandable scrolls, scrollbars, scroll flags, and horizontal sliders;
- scaled static paperdoll previews and logical item drag centers;
- specialized gump placement and rendering for health bars, spell controls, buffs, durability bars, menus, and viewport borders;
- logical-to-physical source cropping in classic/modern shops, trades, paperdolls, loot grids, nearby-item views, counter bars, hue previews, and Myra art widgets;
- mobile, monster, equipment, mount, and corpse animation frames, including shadows, outlines, sitting deformation, depth slices, centering, and pixel selection;
- automatic alpha-mask reconstruction for HD art, land, gumps, and animation frames, plus partial-hue-mask reconstruction where hues apply;
- flat 44 x 44 land art and the 64/128 texmaps used when terrain is stretched by elevation;
- automatic linear filtering on dedicated HD atlas pages while legacy sprites and text retain point sampling;
- one-texel edge extrusion around HD atlas entries to prevent linear-filter bleeding from neighboring assets;
- direct UOP/MUL bulk extraction, padded sheet generation, official Upscayl execution, mask-aware finalization, and manifest validation;
- loose files, the existing `tuoassets.zip` registration paths, and lazy indexed
  `tuoassets.hdpack` loading;
- automatic fallback when dimensions are invalid.

Not implemented yet:

- mipmapped HD atlas pages or a GPU-atlas LRU cache;

The modern `NineSliceControl`/`NineSliceGump` classes use standalone UI textures rather than UO gump IDs, so they are outside the `ExternalImages/gumps` replacement path. UO server `resizepic` entries use the HD-aware `ResizePic` path covered above.

The minimap backgrounds (`0x1392`/`0x1393`, decimal 5010/5011) may be replaced in HD. Their live map pixels are rendered into a separate logical-size overlay, so the dynamic map no longer mutates the shared gump atlas.

For a terrain set, replace both representations. Flat cells render the Land Tile image, while sloped/elevated cells sample the TexID referenced by that land tile. Replacing only one side will produce visible transitions between classic and HD terrain.

## Visual caveats

For tagged HD art, land, gumps, and body animations, TazUO restores the alpha silhouette from the original asset at load time. Art, gumps, and animations also recover the grayscale/partial-hue mask; land colors remain untouched. Legacy 1x replacements keep their existing behavior.

External PNG and BMP files are decoded to premultiplied RGBA pixels entirely on the CPU. Lazy requests from scripting or worker threads therefore never perform a GPU texture readback or force a Metal command-buffer flush while the main renderer has an active encoder.

The automated pipeline additionally color-bleeds transparent source pixels before AI processing, preventing black or white canvas colors from contaminating visible edges. It reapplies the masks before saving, while the runtime restoration remains as a safety net for hand-made or UOFiddler-based replacements.
