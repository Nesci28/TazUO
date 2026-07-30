# Legion XML gump theme

These examples reproduce the dark teal, near-black, and gold visual language used by
`LegionScripts/_Utils/Gump.py` with reusable assets embedded in TazUO.

After building this branch, copy the two XML files into the client's `Data/XmlGumps` directory.
Open them from the **Xml Gumps** top-bar menu and use the context-menu checkbox to auto-open either
gump with the current profile.

- `Player HUD.xml` is a compact paperdoll portrait with live HP, stamina, and mana bars.
- `Character Status.xml` is a native live status dashboard with attributes, load, resistances,
  damage, HCI, DCI and its reported cap, SSI, and casting modifiers.

The XML files do not require loose PNGs. `LegionXmlWindow.png`, `LegionXmlPanel.png`,
`LegionXmlInset.png`, `LegionXmlPortraitFrame.png`, and `LegionXmlTitleGem.png` are embedded in
`ClassicUO.Assets.dll` and referenced by name.
