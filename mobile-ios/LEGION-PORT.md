# Legion on iOS

Legion is a release gate for the IPA. The desktop implementation currently
hosts IronPython and C# scripting through the .NET runtime. Unity's iOS player
uses IL2CPP, so copying `LegionScripting.cs` and the desktop IronPython DLLs
into the Unity project would produce a client that compiles in the editor but
fails or is unsupported on a device.

The port is therefore split at the existing `LegionAPI` boundary:

1. Keep `LegionAPI`, packet actions, world wrappers, and callback scheduling in
   the shared ClassicUO code.
2. Add an iOS script host with an interpreter supported by IL2CPP (or compile
   the selected Python runtime as an iOS native plugin). Its only bridge to
   the game is `ICallbackChannel` and the shared `LegionAPI` instance.
3. Run script pumps on the Unity main-thread budget, matching the browser
   scheduler contract, and persist scripts under Unity's application data
   directory.
4. Add an on-device test script that walks, reads journal state, sends speech,
   opens a gump, and stops cleanly. The IPA is not release-ready until this
   test passes on a physical iPhone.

`mobile-ios/legion-smoke.py` is the first device smoke script. A Unity test
scene can load it and call `TazUOLegionHost.RunScript`; the bridge sends each
API call to the real `GameScene` and packet-producing actions. The remaining
API surface must be mapped before existing production Legion scripts are
claimed compatible.

The current overlay deliberately does not claim Python support. It provides
the real game renderer, direct TCP packets, and touch controls first; the
Legion host is the next native port required before an IPA can be called a
complete TazUO client.
