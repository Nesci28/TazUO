# XML gumps

XML gumps are loaded from `Data/XmlGumps`. They can be opened from the **Xml Gumps** top-bar
menu and optionally reopened automatically with the current profile.

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
