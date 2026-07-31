# XML gumps

XML gumps are loaded from `Data/XmlGumps`. They can be opened from the **Xml Gumps** top-bar
menu and optionally reopened automatically with the current profile.

## Embedded theme images

Use `embedded_image` for a fixed image bundled with TazUO. Width and height are optional; when
omitted, the image uses its natural dimensions.

```xml
<embedded_image texture="LegionXmlPortraitFrame.png" x="5" y="7" alpha="1" />
```

Use `nine_slice` to resize a bundled frame without stretching its corners. The `border` value is
the number of source pixels preserved on each edge.

```xml
<nine_slice
    texture="LegionXmlWindow.png"
    x="0"
    y="0"
    width="250"
    height="98"
    border="10" />
```

Both tags accept `x`, `y`, `width`, `height`, `hue`, and `alpha`. The bundled Legion XML theme
contains these reusable assets:

| Texture | Border | Purpose |
| --- | ---: | --- |
| `LegionXmlWindow.png` | `10` | Dark teal and gold outer window frame. |
| `LegionXmlPanel.png` | `7` | Raised section panel. |
| `LegionXmlInset.png` | `5` | Recessed portrait, row, or bar well. |
| `LegionXmlPortraitFrame.png` | n/a | Gold circular paperdoll portrait bezel. |
| `LegionXmlTitleGem.png` | n/a | Small title-divider ornament. |

Complete themed examples are available in `docs/XmlGumpExamples/LegionTheme`.

## Clean TrueType text

The `text` tag accepts either the existing numeric `hue` or a direct six-digit HTML `color`.
Set `stroke="true"` to use the player's configured text outline, matching scripted labels made by
`CreateGumpTTFLabel(..., applyStroke=True)`. When both `color` and `hue` are present, `color` takes
precedence; keeping `hue` provides a fallback for older clients.

```xml
<text x="12" y="8" width="120" size="9" hue="2414" color="#E5C58D" stroke="true">
    Clean outlined label
</text>
```

Set `fontsizeoffset` on the root `gump` to increase or decrease every TrueType label without
rewriting each `text` element. The final font size is clamped to at least one pixel.

```xml
<gump x="50" y="50" fontsizeoffset="2">
    <text x="12" y="8" size="9">Rendered at size 11</text>
</gump>
```

For compact dashboards, set `nativefont="true"` on the root to render `text` elements with the
same crisp native UO label used by Legion scripts. Native labels honor `hue`, `width`, `align`, and
`updates`; TrueType-only `size`, `color`, and `stroke` settings are ignored. A specific title can
remain TrueType with `native="false"`:

```xml
<gump x="50" y="50" nativefont="true">
    <text x="0" y="4" width="240" size="18" align="center" native="false">TITLE</text>
    <text x="12" y="34" hue="2414">Crisp compact label</text>
</gump>
```

## Connection statistics

Live connection values can be displayed in a `text` element with `updates="true"`:

```xml
<text x="390" y="5" updates="true">Ping: {ping} ms</text>
<text x="390" y="21" updates="true">In: {bytesreceived}  Out: {bytessent}</text>
```

`{ping}` is the current round-trip time in milliseconds. `{bytesreceived}` and `{bytessent}` are
the recent incoming and outgoing byte counts, formatted adaptively as B, KB, MB, or GB.

## Player paperdoll

Use `player_paperdoll` to render a scaled preview of the current player's body and visible
equipment:

```xml
<?xml version="1.0"?>
<gump x="50" y="50" saveposition="true">
    <player_paperdoll
        x="6"
        y="6"
        width="68"
        height="68"
        updates="true"
        background="false"
        alpha="1" />

    <!-- Elements declared later are drawn on top. Use a custom image as a circular frame. -->
    <image id="62090" x="0" y="0" />
</gump>
```

The preview preserves the paperdoll aspect ratio within the requested dimensions. When `updates`
is enabled, body, hue, and equipment changes are detected automatically. The element itself is
rectangular; a circular frame image with an opaque outside area can be layered over it to create a
round portrait.

| Attribute | Default | Description |
| --- | --- | --- |
| `x`, `y` | `0` | Position relative to the XML gump. |
| `width` | `190` | Maximum preview width in pixels. Must be greater than zero. |
| `height` | `250` | Maximum preview height in pixels. Must be greater than zero. |
| `updates` | `true` | Refresh when the player's appearance or visible equipment changes. |
| `background` | `false` | Draw the built-in rectangular preview background and border. |
| `alpha` | `1` | Opacity clamped between `0` and `1`. |
