# Browser scene assets

The browser cannot read Ultima Online `*.mul`/`*.uop` files directly as a game scene. The native
client decodes those files through `ClassicUO.Assets` and uploads the resulting textures to FNA.
Safari needs a browser-safe export (PNG/WebP tile and animation images plus chunk metadata) served
as static files or fetched from a shard asset endpoint.

The repository already contains the legal local conversion utility at `tools/HDAssets`. A real
scene export requires a user supplied Ultima Online client directory containing at least:

```text
map0.mul (or map0LegacyMUL.uop)
statics0.mul / staidx0.mul (or UOP equivalents)
art.mul / artidx.mul (or artLegacyMUL.uop)
tiledata.mul
```

Those client files are intentionally not committed or redistributed. No such directory is present
in this checkout. Generate a real map window with:

```bash
dotnet run --project tools/HDAssets/HDAssets.csproj -- scene \
  --uo "/Applications/TazUO-Launcher.osx-arm64/UO" \
  --output /tmp/tazuo-browser-assets/scene.json \
  --map 0 --x 1420 --y 1620 --width 32 --height 32
```

The browser sample uses the source PNGs with alpha for the visible IDs; the opaque HD upscaling
sheet is not used for runtime composition. The export remains local and ignored by Git because the
source artwork is licensed game data. The next step is exporting player animation frames and
connecting live shard packets to the same scene document.

The animation exporter supports both UOP and classic MUL animation data:

```bash
dotnet run --project tools/HDAssets/HDAssets.csproj -- animation \
  --uo "/path/to/UO" --output /tmp/player.png --body 400 --group 4 --direction 0
```
