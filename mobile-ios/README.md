# TazUO iOS client

This directory contains the native iOS port of the current TazUO client. The
simulator build links `src/ClassicUO.Client` (5.31.2), FNA/SDL3 and the real
Legion scripting engine. It connects directly to an Ultima Online shard; the
browser WebSocket gateway and the old Unity/MobileUO fixture are not used by
this target.

MobileUO is pinned to commit `1ae20dcc0ff6c01e7eb86844a6364b127d766571` and
its ClassicUO submodule is pinned by that project. The bootstrap script copies
the tracked TazUO mobile overlay into a generated Unity project, so the source
checkout stays reproducible and no Unity-generated files are committed.

## Prerequisites

- Xcode and the local .NET iOS SDK installed by
  `tools/mobile-ios/install-dotnet-ios.sh`.
- An Apple signing team for a device IPA (the simulator build does not need
  a team).
- A legal UO installation supplied by the player. The client never bundles
  Ultima Online data files.

The client uses IronPython's interpreter mode, which is compatible with iOS
without JIT. User data and Legion scripts live in the app's Documents folder,
so scripts and profiles can be updated without rebuilding the app.

## Build and run the native simulator client

```sh
tools/mobile-ios/install-dotnet-ios.sh   # first machine setup
tools/mobile-ios/build-native-simulator.sh
tools/mobile-ios/test-native-simulator.sh
```

The test script installs the real `.app`, logs into a configured shard, waits
for the world, and captures `mobile-ios/native-runtime.png`. Add
`TAZUO_TEST_LEGION=1` to run the live `API.Player`/`API.Pause` smoke script.

## Legacy Unity project

The following commands generate the older MobileUO/Unity experiment. They are
kept for comparison only and do not build the native TazUO client or its IPA.

```sh
tools/mobile-ios/bootstrap.sh
```

This now selects the current TazUO source tree by default. Set
`TAZUO_USE_CURRENT_SOURCE=0` only to recreate the old MobileUO transport
fixture while investigating the port.

The generated project is `mobile-ios/build/MobileUO`. Open that directory in
Unity `6000.3.24f1`, add the supplied UO data directory in the normal MobileUO
download screen, and build the iOS target. The generated Xcode project can
then be signed and archived as an IPA.

The bootstrap ends with `tools/mobile-ios/validate-unity-project.sh`; this
checks the Unity version, direct TCP `NetClient`, native touch overlay and iOS
build method before Unity is opened.

For CI or a repeatable local build, use the Unity batch method installed by
the overlay:

```sh
Unity -batchmode -quit \
  -projectPath mobile-ios/build/MobileUO \
  -buildTarget iOS \
  -executeMethod TazUO.Editor.BuildIOS.Perform \
  -logFile mobile-ios/build/unity-ios.log
```

To build and install the simulator app in one command:

```sh
tools/mobile-ios/build-simulator.sh
```

This produces an unsigned simulator `.app` and installs it with `simctl`. A
simulator app is not an iPhone IPA; device distribution still requires an
Xcode archive, signing certificate and provisioning profile.

With an Apple signing team configured, the native device archive command is:

```sh
DEVELOPMENT_TEAM=XXXXXXXXXX tools/mobile-ios/archive-ipa.sh
```

Optional `CODESIGN_KEY` and `CODESIGN_PROVISION` select a local signing
identity/profile. The command builds `ios-arm64` native libraries and uses
`dotnet publish` to create a signed device IPA.

The runtime uses TazUO's direct TCP `NetClient`, packet encryption, compression
and handlers. The native joystick calls the same `PlayerMobile.Walk` path as
keyboard input. The game scene fills the landscape client area and retains
TazUO's existing tabs, gumps and controls. A real simulator smoke test has
logged into Crossroads, rendered the server map and character, executed a live
Legion API script, and moved the player with an acknowledged server packet.

## TazUO parity gate

The next port stage must replace the MobileUO snapshot with the current
`src/ClassicUO.Client` tree and its TazUO dependencies (FNA, Myra, Legion,
packet handlers and current UI). The Unity adapter must then provide the
window, graphics, audio, storage and touch implementations expected by that
tree. A release build is blocked until the login screen identifies TazUO
5.31.2, the current TazUO UI is present, mounted mobiles render correctly,
and the real-server smoke test passes again.

## Acceptance checks

Run these in order on the simulator, then repeat the network checks on a real
iPhone:

1. The app opens in landscape and shows the real login/world renderer, not a
   browser surface.
2. Login reaches the shard directly; no TazUO process or local WebSocket is
   running on the Mac.
3. Holding each direction produces server-confirmed movement and releasing it
   stops movement.
4. Target enters target mode and a world tap sends the target packet; Chat
   opens the iOS keyboard and sends speech.
5. Inventory, paperdoll, journal, map, gumps, backpack and status bars open
   using the existing ClassicUO UI.
6. The Legion device test in `mobile-ios/LEGION-PORT.md` runs, yields while
   waiting, reads journal state, and stops without leaving a script thread.
