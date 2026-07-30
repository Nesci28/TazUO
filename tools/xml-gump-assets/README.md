# XML gump theme assets

The SVG sources in this directory reproduce the Legion gump palette and framing used by
`LegionScripts/_Utils/Gump.py`:

- near-black outer frame: `#070401`
- bronze frame: `#b67d28`
- gold highlight: `#f2c45e`
- dark teal interiors: `#061014`, `#0a1519`, and `#0a171b`
- teal panel highlight: `#102a32`

Run `./generate.sh` after changing an SVG. The generator requires Inkscape or `rsvg-convert` and
writes 8-bit RGBA PNGs to `src/ClassicUO.Assets/gumpartassets`, where the project embeds them in
`ClassicUO.Assets.dll`.

`LegionXmlWindow.png`, `LegionXmlPanel.png`, and `LegionXmlInset.png` are nine-slice textures. Keep
their documented border sizes in sync with `docs/XmlGumps.md` and the example XML files.
