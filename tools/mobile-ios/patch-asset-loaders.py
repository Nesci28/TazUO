"""Compatibility changes for the pinned MobileUO reader (safe to run twice)."""
from pathlib import Path
import sys
import subprocess

root = Path(sys.argv[1]) / "Assets/Scripts/ClassicUO/src"


def patch(name, before, after):
    path = root / name
    text = path.read_text(encoding="utf-8-sig")
    if before not in text and after not in text:
        raise SystemExit(f"Pinned MobileUO pattern missing: {name}")
    path.write_text(text.replace(before, after))


# These generated files belong to this patch. Start from the pinned submodule
# so repeat builds cannot stack the same change or retain an abandoned fix.
for name in ("IO/UOFileIndex.cs", "IO/UOFileUop.cs", "IO/Resources/AnimationsLoader.cs", "Utility/Crypter.cs", "Network/PacketHandlers.cs"):
    original = subprocess.check_output(["git", "show", "HEAD:./src/" + name], cwd=root.parent)
    (root / name).write_bytes(original)

patch("IO/UOFileIndex.cs", "            AnimOffset = 0;",
      "            AnimOffset = 0;\n            CompressionFlag = 0;")
patch("IO/UOFileIndex.cs", "        public sbyte AnimOffset;",
      "        public sbyte AnimOffset;\n        public short CompressionFlag;")
patch("IO/UOFileUop.cs", "if (_hasExtra)", "if (_hasExtra && flag != 3)")
patch("IO/UOFileUop.cs",
      "decompressedLength, extra1, extra2));",
      "decompressedLength, extra1, extra2) { CompressionFlag = flag });")
patch("IO/UOFileUop.cs",
      "offset, compressedLength, decompressedLength));",
      "offset, compressedLength, decompressedLength) { CompressionFlag = flag });")

# AnimationSequence can remap a body without corresponding UOP frame groups.
patch("IO/Resources/AnimationsLoader.cs",
      "            _uopReplaceGroupIndex[old] = newG;",
      "            InitializeUOP();\n"
      "            if (old < _uopReplaceGroupIndex.Length && newG < _uopReplaceGroupIndex.Length)\n"
      "                _uopReplaceGroupIndex[old] = newG;")

# Development-only evidence from the real inbound movement handlers. Do not
# log packet contents, login credentials, or chat text.
patch("Network/PacketHandlers.cs",
      "            World.Player.Walker.ConfirmWalk(seq);",
      """            World.Player.Walker.ConfirmWalk(seq);
#if UNITY_IOS && DEVELOPMENT_BUILD
            UnityEngine.Debug.Log($"TazUO walk ack seq={seq} confirmed={World.RangeSize.X},{World.RangeSize.Y}");
#endif""")
patch("Network/PacketHandlers.cs",
      "            World.Player.Walker.DenyWalk(seq, x, y, z);",
      """            World.Player.Walker.DenyWalk(seq, x, y, z);
#if UNITY_IOS && DEVELOPMENT_BUILD
            UnityEngine.Debug.Log($"TazUO walk denied seq={seq} corrected={x},{y},{z}");
#endif""")

# The existing joystick writes its value every frame, including zero when
# idle. Merge the held buttons there instead of letting it erase their state.
project = Path(sys.argv[1])
runner = project / "Assets/Scripts/ClientRunner.cs"
text = subprocess.check_output(["git", "show", "HEAD:Assets/Scripts/ClientRunner.cs"], cwd=project).decode("utf-8-sig")
text = text.replace("if (movementJoystick.isActiveAndEnabled && Client.Game.Scene is GameScene gameScene)",
                    "if (Client.Game.Scene is GameScene gameScene)")
before = "gameScene.JoystickInput = new Microsoft.Xna.Framework.Vector2(movementJoystick.Input.x, -1 * movementJoystick.Input.y);"
after = """gameScene.JoystickInput = gameScene.MobileButtonInput != Microsoft.Xna.Framework.Vector2.Zero
                ? gameScene.MobileButtonInput
                : movementJoystick.isActiveAndEnabled
                    ? new Microsoft.Xna.Framework.Vector2(movementJoystick.Input.x, -movementJoystick.Input.y)
                    : Microsoft.Xna.Framework.Vector2.Zero;"""
if before not in text:
    raise SystemExit("Pinned ClientRunner joystick pattern missing")
runner.write_text(text.replace(before, after))

# The upstream preference can disable joystick follow-cancellation. A visible
# directional button is an explicit movement command and must still reach the
# normal PlayerMobile.Walk path.
scene = project / "Assets/Scripts/ClassicUO/src/Game/Scenes/GameScene.cs"
scene_text = subprocess.check_output(["git", "show", "HEAD:./src/Game/Scenes/GameScene.cs"], cwd=root.parent).decode("utf-8-sig")
scene_text = scene_text.replace(
    "if (JoystickInput != Vector2.Zero && UserPreferences.JoystickCancelsFollow.CurrentValue == (int) PreferenceEnums.JoystickCancelsFollow.On)",
    "if (JoystickInput != Vector2.Zero && (MobileButtonInput != Vector2.Zero || UserPreferences.JoystickCancelsFollow.CurrentValue == (int) PreferenceEnums.JoystickCancelsFollow.On))")
scene.write_text(scene_text)
