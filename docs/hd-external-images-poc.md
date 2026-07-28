# HD External Images proof of concept

This proof of concept lets selected `art`, `gump`, `land`, `texmap`, and body-animation overrides use a 2x or 4x source image while retaining the original UO asset's logical size, placement, and hit area.

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

## Required dimensions

The HD canvas must be an exact multiple of the original asset canvas:

| Original | `@2x` | `@4x` |
| --- | --- | --- |
| 40 x 60 | 80 x 120 | 160 x 240 |

Do not trim transparent borders after upscaling. TazUO compares the replacement dimensions with the original asset at first use. A mismatch is logged and the original UO asset is used instead.

An HD image larger than the current 4096 x 4096 atlas page is also rejected by this POC.

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

## POC coverage

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
- automatic animation alpha-mask and partial-hue-mask reconstruction from the original frame;
- flat 44 x 44 land art and the 64/128 texmaps used when terrain is stretched by elevation;
- automatic linear filtering while tagged HD images are active;
- loose files and the existing `tuoassets.zip` registration paths;
- automatic fallback when dimensions are invalid.

Not implemented yet:

- a bulk UOFiddler export/rename/upscale automation tool;
- runtime-rasterized minimap backgrounds (`0x1392`/`0x1393`, decimal 5010/5011);
- mipmapped/padded HD atlas pages, streaming, or an LRU cache;
- partial-hue mask restoration during image preprocessing.

The modern `NineSliceControl`/`NineSliceGump` classes use standalone UI textures rather than UO gump IDs, so they are outside the `ExternalImages/gumps` replacement path. UO server `resizepic` entries use the HD-aware `ResizePic` path covered above.

The minimap paints live map pixels directly into two classic gump backgrounds and depends on exact 1x mask colors. Tagged HD overrides for IDs 5010 and 5011 are therefore rejected with a warning and automatically fall back to the original client gumps; ordinary 1x overrides retain their previous behavior.

For a terrain set, replace both representations. Flat cells render the Land Tile image, while sloped/elevated cells sample the TexID referenced by that land tile. Replacing only one side will produce visible transitions between classic and HD terrain.

## Visual caveats

For body animations, TazUO restores the binary alpha and grayscale/partial-hue masks from the original frame. Static art and gumps do not yet receive that restoration, so Upscayl can still alter hueable regions or introduce alpha-edge halos in those categories. A dedicated preprocessing pipeline that separates RGB, alpha, and hue masks will give the most predictable final asset pack.
