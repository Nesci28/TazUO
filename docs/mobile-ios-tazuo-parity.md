# iOS TazUO parity

The iOS simulator has a working direct-TCP session through the compatibility
client, but it is not yet evidence that the current TazUO renderer runs on
iOS. The Unity import now selects the current TazUO source tree by default.

## Evidence

- `src/ClassicUO.Client/ClassicUO.Client.csproj` declares assembly/file
  version `5.31.2`.
- The compatibility fixture remains available only with
  `TAZUO_USE_CURRENT_SOURCE=0`.
- Current source import excludes desktop `bin/` and `obj/` output so Unity
  does not load stale FNA/TazUO assemblies.
- Unity's editor packages now compile without the former `SettingsScope`
  collision, and the active Personal license connects successfully.

## Required port

1. Build a clean generated Unity project from the current TazUO source tree,
   retaining only the Unity host/adapters needed for iOS.
2. Resolve API and runtime differences between the current TazUO/FNA code and
   Unity IL2CPP, including graphics, audio, storage, threading and native
   iOS lifecycle.
3. Port the current TazUO UI and asset pipeline before adding mobile layout
   changes. This is where the transparent mounted-mobile rendering must be
   fixed against the current animation and asset code.
4. Reconnect the current Legion implementation through a main-thread iOS
   scheduler and test its movement, journal, targeting and speech APIs.
5. Repeat the Crossroads test with the current client and only then create a
   signed device archive/IPA.

The existing MobileUO build remains a transport/device-shell test fixture. It
must not be used as evidence that the TazUO 5.31.2 UI has been ported.

The source switch is the default in `bootstrap.sh`. On 2026-09-12 the first
successful current-source import reached Unity C# compilation. Unity 6's
compiler is limited to C# 9, while current TazUO uses C# 10+ syntax (for
example file-scoped namespaces, `record struct`, and collection expressions),
so the current tree cannot yet produce an iOS binary. The simulator still
contains the earlier compatibility build; it must not be used as evidence of
current TazUO parity.
