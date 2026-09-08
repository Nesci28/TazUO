# World map icons

Source: [Lucide](https://github.com/lucide-icons/lucide/tree/a537cb6eb323b885f4c60baf3cec1a995982d167/icons),
revision `a537cb6eb323b885f4c60baf3cec1a995982d167`.
The original SVGs and upstream license are retained here. The license is also
embedded in the assets assembly and copied to `licenses/Lucide.txt` in builds.

The client uses the adjacent `map-*.png` files: 18 × 18 transparent icons with
off-white strokes (`#f5f5f5`), rendered at four times the final icon resolution
and downsampled for smooth edges. No SVG library is needed at runtime.

To regenerate from the repository root with Node.js and ImageMagick 7, install
the development renderer in a temporary directory:

```sh
npm install --prefix /tmp/tazuo-icon-render --ignore-scripts @resvg/resvg-js@2.6.2
NODE_PATH=/tmp/tazuo-icon-render/node_modules node src/ClassicUO.Assets/gumpartassets/lucide/render-icons.cjs
```
