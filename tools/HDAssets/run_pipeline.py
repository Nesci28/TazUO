#!/usr/bin/env python3
"""Build an HD ExternalImages pack from an installed Ultima Online Classic client."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import stat
import subprocess
import sys
import tempfile
import urllib.request
import zipfile


BACKEND_URL = (
    "https://github.com/upscayl/upscayl-ncnn/releases/download/20251207-174704/"
    "upscayl-bin-20251207-174704-macos.zip"
)
BACKEND_SHA256 = "277419791281a56eae0c739c70120b974d7267cf7c2de8e86dc09798d4b314db"
MODEL_FILES = {
    "digital-art-4x": {
        "bin": (
            "https://raw.githubusercontent.com/upscayl/upscayl/main/resources/models/"
            "digital-art-4x.bin",
            "fe01c269cfd10cdef8e018ab66ebe750cf79c7af4d1f9c16c737e1295229bacc",
        ),
        "param": (
            "https://raw.githubusercontent.com/upscayl/upscayl/main/resources/models/"
            "digital-art-4x.param",
            "2b8fb6e0ae4d2d85704ca08c119a2f5ea40add4f2ecd512eb7f4cd44b6127ed4",
        ),
    },
    "high-fidelity-4x": {
        "bin": (
            "https://raw.githubusercontent.com/upscayl/upscayl/main/resources/models/"
            "high-fidelity-4x.bin",
            "8a135402b4f39286121b76abb47601a6b7b7e8d4f3e999a5aaa45ed277824fb4",
        ),
        "param": (
            "https://raw.githubusercontent.com/upscayl/upscayl/main/resources/models/"
            "high-fidelity-4x.param",
            "4576ed5c2fc5fa250d3c3d585ef02248f26abdfc1867088078f501fe71e5d61e",
        ),
    },
    "upscayl-lite-4x": {
        "bin": (
            "https://raw.githubusercontent.com/upscayl/upscayl/main/resources/models/"
            "upscayl-lite-4x.bin",
            "85ee266b632a765a725425ba6a5620c088c8aa2939a03063b2d83b3462724cc1",
        ),
        "param": (
            "https://raw.githubusercontent.com/upscayl/upscayl/main/resources/models/"
            "upscayl-lite-4x.param",
            "22174924330297357434ad21ed0af7f4b820008d2a502b492754d130d4142714",
        ),
    },
    "realesr-animevideov3-x2": {
        "bin": (
            "https://raw.githubusercontent.com/upscayl/custom-models/main/models/"
            "realesr-animevideov3-x2.bin",
            "548a36f9c3f4ab8da56cd3b13badf23968bee207b396dad14d04b830e5f2ab2d",
        ),
        "param": (
            "https://raw.githubusercontent.com/upscayl/custom-models/main/models/"
            "realesr-animevideov3-x2.param",
            "b88ff4f00ebf019a7fdac17fdd45a7fd3665d37509efc5baf2e4da2e24420a04",
        ),
    },
    "realesr-animevideov3-x4": {
        "bin": (
            "https://raw.githubusercontent.com/upscayl/custom-models/main/models/"
            "realesr-animevideov3-x4.bin",
            "548a36f9c3f4ab8da56cd3b13badf23968bee207b396dad14d04b830e5f2ab2d",
        ),
        "param": (
            "https://raw.githubusercontent.com/upscayl/custom-models/main/models/"
            "realesr-animevideov3-x4.param",
            "850a248e7c14c27e5bd8cf7265113a9441036a7db63963bb8aa5169d788a435e",
        ),
    },
}
MODEL_SCALES = {
    "digital-art-4x": 4,
    "high-fidelity-4x": 4,
    "upscayl-lite-4x": 4,
    "realesr-animevideov3-x2": 2,
    "realesr-animevideov3-x4": 4,
}

CATEGORY_MODEL_ARGUMENTS = {
    "animations": "animation_model",
    "art": "art_model",
    "gumps": "gump_model",
    "land": "land_model",
    "texmaps": "texmap_model",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--uo", required=True, type=Path, help="Ultima Online data directory")
    parser.add_argument("--tazuo-bin", required=True, type=Path, help="Deployed TazUO DLL directory")
    parser.add_argument("--work", required=True, type=Path, help="Pipeline work directory")
    parser.add_argument("--output", required=True, type=Path, help="Final ExternalImages directory")
    parser.add_argument(
        "--hdpack",
        type=Path,
        help="HD pack path (default: tuoassets.hdpack beside ExternalImages)",
    )
    parser.add_argument(
        "--skip-hdpack",
        action="store_true",
        help="Keep only the loose ExternalImages output",
    )
    parser.add_argument("--scale", type=int, choices=(2, 4), default=2)
    parser.add_argument("--sheet-size", type=int, default=1024)
    parser.add_argument("--padding", type=int, default=16)
    parser.add_argument("--tile-size", type=int, default=256, help="Upscayl GPU tile size")
    parser.add_argument(
        "--categories",
        default="land,art,gumps,texmaps,animations",
        help="Comma-separated asset categories",
    )
    parser.add_argument(
        "--model",
        choices=tuple(MODEL_FILES),
        default="high-fidelity-4x",
        help="Fallback model and model for sheets containing multiple categories",
    )
    parser.add_argument(
        "--land-model",
        choices=tuple(MODEL_FILES),
        help="Override the model used for land sheets",
    )
    parser.add_argument(
        "--art-model",
        choices=tuple(MODEL_FILES),
        help="Override the model used for static-art sheets",
    )
    parser.add_argument(
        "--gump-model",
        choices=tuple(MODEL_FILES),
        default="realesr-animevideov3-x4",
        help="Model for gump sheets (default: AnimeVideo native 4x)",
    )
    parser.add_argument(
        "--texmap-model",
        choices=tuple(MODEL_FILES),
        help="Override the model used for texmap sheets",
    )
    parser.add_argument(
        "--animation-model",
        choices=tuple(MODEL_FILES),
        default="realesr-animevideov3-x4",
        help="Model for sheets containing only animation frames",
    )
    parser.add_argument("--batch-size", type=int, default=25, help="Resume granularity in sheets")
    parser.add_argument("--upscayl-bin", type=Path, help="Use an existing upscayl-ncnn binary")
    parser.add_argument("--models-dir", type=Path, help="Use an existing Upscayl models directory")
    parser.add_argument("--max-assets", type=int, default=0, help="Non-zero creates a sample pack")
    parser.add_argument("--skip-export", action="store_true")
    parser.add_argument("--skip-upscale", action="store_true")
    return parser.parse_args()


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def download(url: str, path: Path, expected_sha256: str) -> None:
    if path.exists() and sha256(path) == expected_sha256:
        return

    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + ".part")
    print(f"Downloading {url}")
    with urllib.request.urlopen(url) as response, temporary.open("wb") as output:
        shutil.copyfileobj(response, output)

    actual = sha256(temporary)
    if actual != expected_sha256:
        raise RuntimeError(f"SHA-256 mismatch for {path.name}: {actual}")
    os.replace(temporary, path)


def ensure_backend(args: argparse.Namespace) -> Path:
    if args.upscayl_bin:
        return args.upscayl_bin.resolve()

    if platform.system() != "Darwin":
        raise RuntimeError("Automatic backend installation is currently macOS-only; pass --upscayl-bin.")

    backend_dir = args.work / "tools" / "upscayl-ncnn"
    binary = backend_dir / "upscayl-bin"
    if binary.exists():
        binary.chmod(binary.stat().st_mode | stat.S_IXUSR)
        return binary

    archive = args.work / "downloads" / "upscayl-ncnn-macos.zip"
    download(BACKEND_URL, archive, BACKEND_SHA256)
    extract_dir = args.work / "downloads" / "upscayl-ncnn-extracted"
    extract_dir.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(archive) as zip_file:
        zip_file.extractall(extract_dir)

    matches = list(extract_dir.rglob("upscayl-bin"))
    if len(matches) != 1:
        raise RuntimeError("Could not identify upscayl-bin in the official archive.")
    backend_dir.mkdir(parents=True, exist_ok=True)
    shutil.copy2(matches[0], binary)
    binary.chmod(binary.stat().st_mode | stat.S_IXUSR)
    return binary


def ensure_models(args: argparse.Namespace) -> Path:
    if args.models_dir:
        return args.models_dir.resolve()

    model_dir = args.work / "tools" / "models"
    for model in selected_models(args):
        for extension, (url, digest) in MODEL_FILES[model].items():
            download(url, model_dir / f"{model}.{extension}", digest)
    return model_dir


def selected_models(args: argparse.Namespace) -> set[str]:
    models = {args.model}
    for argument in CATEGORY_MODEL_ARGUMENTS.values():
        model = getattr(args, argument)
        if model is not None:
            models.add(model)
    return models


def model_for_sheet(args: argparse.Namespace, categories: list[str]) -> str:
    if len(categories) != 1:
        return args.model

    argument = CATEGORY_MODEL_ARGUMENTS.get(categories[0])
    if argument is None:
        return args.model
    return getattr(args, argument) or args.model


def build_tool(repo_root: Path, args: argparse.Namespace) -> Path:
    project = repo_root / "tools" / "HDAssets" / "HDAssets.csproj"
    artifacts = args.work / "tool-artifacts"
    command = [
        "dotnet",
        "build",
        str(project),
        "--configuration",
        "Release",
        f"-p:UseDeployedAssemblies=true",
        f"-p:TazUOAssembliesPath={args.tazuo_bin.resolve()}",
        "--artifacts-path",
        str(artifacts),
    ]
    subprocess.run(command, check=True)
    tool = artifacts / "bin" / "HDAssets" / "release" / "HDAssets.dll"
    if not tool.exists():
        raise RuntimeError(f"HDAssets tool was not produced: {tool}")
    return tool


def run_export(tool: Path, args: argparse.Namespace) -> None:
    manifest = args.work / "manifest.json"
    summary_path = args.work / "pipeline-summary.json"
    if manifest.exists() and summary_path.exists():
        data = json.loads(summary_path.read_text())
        expected_categories = sorted(
            part.strip() for part in args.categories.split(",") if part.strip()
        )
        actual_categories = sorted(data.get("categories", []))
        mismatches = []
        for key, expected in (
            ("scale", args.scale),
            ("sheetSize", args.sheet_size),
            ("padding", args.padding),
            ("maxAssets", args.max_assets),
        ):
            if data.get(key) != expected:
                mismatches.append(f"{key}={data.get(key)} (requested {expected})")
        if actual_categories != expected_categories:
            mismatches.append(
                f"categories={','.join(actual_categories)} "
                f"(requested {','.join(expected_categories)})"
            )
        if mismatches:
            raise RuntimeError(
                "The existing work manifest is incompatible: "
                + "; ".join(mismatches)
                + ". Use a new --work directory."
            )

    if args.skip_export or manifest.exists():
        print(f"Using existing export manifest: {manifest}")
        return

    command = [
        "dotnet",
        str(tool),
        "export",
        "--uo",
        str(args.uo.resolve()),
        "--work",
        str(args.work.resolve()),
        "--scale",
        str(args.scale),
        "--sheet-size",
        str(args.sheet_size),
        "--padding",
        str(args.padding),
        "--categories",
        args.categories,
        "--max-assets",
        str(args.max_assets),
    ]
    subprocess.run(command, check=True)


def ensure_plan(tool: Path, args: argparse.Namespace) -> dict:
    summary_path = args.work / "pipeline-summary.json"
    if not summary_path.exists():
        subprocess.run(
            ["dotnet", str(tool), "plan", "--work", str(args.work.resolve())],
            check=True,
        )
    return json.loads(summary_path.read_text())


def run_upscayl_batch(
    binary: Path,
    models: Path,
    model: str,
    input_paths: list[Path],
    output: Path,
    args: argparse.Namespace,
) -> None:
    with tempfile.TemporaryDirectory(prefix=f"{model}-", dir=args.work / "staging") as temporary:
        input_directory = Path(temporary)
        for source in input_paths:
            os.link(source, input_directory / source.name)

        command = [
            str(binary),
            "-i",
            str(input_directory),
            "-o",
            str(output),
            "-m",
            str(models),
            "-n",
            model,
            "-z",
            str(MODEL_SCALES[model]),
            "-s",
            str(args.scale),
            "-f",
            "png",
            "-t",
            str(args.tile_size),
            "-v",
        ]
        subprocess.run(command, check=True)


def run_model_batches(
    binary: Path,
    models: Path,
    model: str,
    sheet_names: list[str],
    sheets: Path,
    output: Path,
    args: argparse.Namespace,
) -> None:
    completed = args.work / f"finalized-{args.scale}x"
    pending = [
        sheets / name
        for name in sheet_names
        if not (output / name).exists()
        and not (completed / f"{Path(name).stem}.done").exists()
    ]
    if not pending:
        print(f"All {len(sheet_names)} {model} sheets are already complete")
        return

    print(f"Processing {len(pending)}/{len(sheet_names)} sheets with {model}")
    for start in range(0, len(pending), args.batch_size):
        batch = pending[start : start + args.batch_size]
        batch_number = start // args.batch_size + 1
        batch_count = (len(pending) + args.batch_size - 1) // args.batch_size
        print(f"{model}: batch {batch_number}/{batch_count} ({len(batch)} sheets)", flush=True)
        run_upscayl_batch(binary, models, model, batch, output, args)


def run_upscayl(binary: Path, models: Path, summary: dict, args: argparse.Namespace) -> Path:
    sheets = args.work / "sheets"
    upscaled = args.work / f"upscaled-{args.scale}x"
    upscaled.mkdir(parents=True, exist_ok=True)
    (args.work / "staging").mkdir(parents=True, exist_ok=True)

    input_sheets = sorted(sheets.glob("*.png"))
    if not input_sheets:
        raise RuntimeError(f"No input sheets found in {sheets}")
    completed_markers = args.work / f"finalized-{args.scale}x"
    completed_count = sum(
        1
        for sheet in input_sheets
        if (upscaled / sheet.name).exists()
        or (completed_markers / f"{sheet.stem}.done").exists()
    )
    if args.skip_upscale or completed_count == len(input_sheets):
        print(f"Using {completed_count} completed Upscayl sheets: {upscaled}")
        return upscaled

    free_gib = shutil.disk_usage(args.work).free / (1024**3)
    print(f"Upscaling {len(input_sheets)} sheets; {free_gib:.1f} GiB free")
    sheets_by_model: dict[str, list[str]] = {}
    for name, categories in sorted(summary["sheets"].items()):
        model = model_for_sheet(args, categories)
        sheets_by_model.setdefault(model, []).append(name)

    for model, sheet_names in sheets_by_model.items():
        run_model_batches(
            binary, models, model, sheet_names, sheets, upscaled, args
        )
    return upscaled


def run_finalize(tool: Path, upscaled: Path, args: argparse.Namespace) -> None:
    subprocess.run(
        [
            "dotnet",
            str(tool),
            "finalize",
            "--work",
            str(args.work.resolve()),
            "--upscaled",
            str(upscaled.resolve()),
            "--output",
            str(args.output.resolve()),
            "--delete-upscaled",
            "true",
        ],
        check=True,
    )
    subprocess.run(
        [
            "dotnet",
            str(tool),
            "validate",
            "--work",
            str(args.work.resolve()),
            "--output",
            str(args.output.resolve()),
        ],
        check=True,
    )
    if not args.skip_hdpack:
        hdpack = args.hdpack or (args.output.resolve().parent / "tuoassets.hdpack")
        subprocess.run(
            [
                "dotnet",
                str(tool),
                "pack",
                "--work",
                str(args.work.resolve()),
                "--input",
                str(args.output.resolve()),
                "--output",
                str(hdpack.resolve()),
            ],
            check=True,
        )


def main() -> int:
    args = parse_args()
    for model in selected_models(args):
        if MODEL_SCALES[model] > args.scale:
            continue
        if MODEL_SCALES[model] != args.scale:
            raise RuntimeError(
                f"Model {model} is native {MODEL_SCALES[model]}x and cannot produce {args.scale}x."
            )
    args.work.mkdir(parents=True, exist_ok=True)
    repo_root = Path(__file__).resolve().parents[2]
    tool = build_tool(repo_root, args)
    run_export(tool, args)
    summary = ensure_plan(tool, args)
    backend = ensure_backend(args)
    models = ensure_models(args)
    upscaled = run_upscayl(backend, models, summary, args)
    run_finalize(tool, upscaled, args)
    print(f"Complete HD pack: {args.output.resolve()}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
