#!/bin/sh
set -eu

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
output_dir="$script_dir/../../src/ClassicUO.Assets/gumpartassets"

for source in "$script_dir"/*.svg; do
    name=$(basename "$source" .svg)
    if command -v inkscape >/dev/null 2>&1; then
        inkscape "$source" \
            --export-filename="$output_dir/$name.png" \
            --export-background-opacity=0
    elif command -v rsvg-convert >/dev/null 2>&1; then
        rsvg-convert "$source" --output "$output_dir/$name.png"
    else
        echo "Inkscape or rsvg-convert is required to generate XML gump assets." >&2
        exit 1
    fi
done

echo "Generated Legion XML gump assets in $output_dir"
