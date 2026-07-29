# TazUO HD Assets pipeline

This tool reads an installed Ultima Online Classic client directly. UOFiddler is not required for bulk conversion.

It exports land art, static art, gumps, texmaps, and animation frames; packs opaque, color-bled inputs into GPU-efficient sheets; runs the official `upscayl-ncnn` backend; then splits the sheets into TazUO's `ExternalImages` layout. The finalizer restores the original alpha silhouette and grayscale partial-hue mask before writing each `@2x` or `@4x` PNG.

The pipeline builds only the small `HDAssets` utility against already-deployed TazUO DLLs. It does not build the TazUO client.

## macOS example

```bash
python3 tools/HDAssets/run_pipeline.py \
  --uo "/Applications/TazUO-Launcher.osx-arm64/Ultima Online Classic" \
  --tazuo-bin "/Applications/TazUO-Launcher.osx-arm64/TazUO" \
  --work "/Applications/TazUO-Launcher.osx-arm64/HDAssetsWork" \
  --output "/Applications/TazUO-Launcher.osx-arm64/TazUO/ExternalImages" \
  --scale 2 \
  --model high-fidelity-4x \
  --animation-model realesr-animevideov3-x4
```

On macOS the script downloads the current official universal `upscayl-ncnn` backend release and the selected model, verifies their SHA-256 hashes, and makes the backend executable. Metal access is required. A complete 2x pack is recommended before trying 4x because animations dominate both disk usage and conversion time.

After a sheet is finalized successfully, the pipeline writes a durable completion marker and removes that large upscaled sheet. This keeps peak disk usage bounded while preserving restart safety: completed sheets are skipped by both the upscale and finalization stages.

The default Upscayl tile size is 256 pixels, which keeps High Fidelity within the unified-memory budget on an Apple M4 while producing the same output. It can be changed with `--tile-size` for a GPU with more or less memory.

By default, land, static art, and texmaps use High Fidelity. Gumps and animation frames use the native 4x AnimeVideo model, which preserves outlined shapes more cleanly and avoids the accumulated stylization of two consecutive 2x passes. A rare sheet containing more than one category uses the fallback `--model`. Work is divided into 25-sheet batches, so an interrupted run resumes from the first incomplete batch.

Each category can be tuned independently with `--land-model`, `--art-model`, `--gump-model`, `--texmap-model`, and `--animation-model`. Unspecified land, art, or texmap models inherit `--model`.

For a much faster native 2x pass, use `--model realesr-animevideov3-x2 --gump-model realesr-animevideov3-x2 --animation-model realesr-animevideov3-x2`. This model comes from the official `upscayl/custom-models` repository and avoids computing an intermediate 4x image. It is particularly effective for outlined UO statics and animation frames. The High Fidelity and Upscayl Lite models remain available when texture reconstruction or faster exhaustive processing is preferred.

The work directory is resumable: an existing export manifest skips extraction, and a complete Upscayl sheet directory skips AI processing. Use a new work directory to change scale, sheet size, padding, categories, or model.

For a small end-to-end sample, append `--max-assets 100`. Supported category names are `land`, `art`, `gumps`, `texmaps`, and `animations`.

Generated Ultima Online artwork remains subject to the game's license. Keep generated packs for installations where you are entitled to use the source assets; do not commit or redistribute them with TazUO.
