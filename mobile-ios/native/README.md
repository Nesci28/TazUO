# Native iOS build

This project is the maintained native host for the current TazUO client. It
references `src/ClassicUO.Client` directly, so updating the TazUO commit does
not copy the client into a separate Unity snapshot. Platform-specific code is
kept in this directory; the upstream FNA and SDL3 submodules remain unchanged.

The native host starts the real TazUO `Bootstrap` and `GameController` in
UIKit. The simulator gate verifies Legion's IronPython engine, loads the 2.6 GB
UO data set, creates the SDL3/FNA3D OpenGL ES renderer, connects to a real
server, selects a character, and reaches the in-game world. A transparent
UIKit joystick writes directly to `Player.Walk`, so movement uses the normal
TazUO packet path. Audio is disabled in this target until the FAudio ARM64
static build is adapted to the iOS linker.

Install the pinned SDK/workload once:

```sh
tools/mobile-ios/install-dotnet-ios.sh
```

Build the simulator app:

```sh
tools/mobile-ios/build-native-simulator.sh
```

Install on the booted simulator:

```sh
tools/mobile-ios/install-native-simulator.sh
```

The local test writes `mobile-ios/native-client.log` and a screenshot. The
success criteria are `Legion engine: PASS` and `Native GameController: READY`.

Build an unsigned iPhone IPA for signing with Sideloadly:

```sh
bash tools/mobile-ios/build-native-unsigned-ipa.sh
```

This uses the native libraries already built under `mobile-ios/build/native/ios-arm64`.
The archive is written to `mobile-ios/build/TazUO-<version>-unsigned.ipa` and
includes local UO data when available (`TAZUO_UO_PATH` overrides its location).
Device builds must keep the iOS linker enabled, even with `MtouchLink=None`:
it generates static runtime mappings and the assemblies used by AOT. Do not
pass `_MustTrim=false` or clear `PublishTrimmed` for an iPhone build.

On iOS, user fonts and their cache live in `Documents/TazUO/Fonts` and
`Documents/TazUO/Cache`, alongside the client settings. The signed application
bundle is read-only. A graphics startup failure restores the diagnostic window
above SDL; its full exception is saved in `Documents/native-runtime.log`.

To run the real Crossroads smoke test, pass credentials only through the
environment. They are written to the simulator sandbox for startup and then
deleted:

```sh
TAZUO_TEST_ACCOUNT='user:password' \
TAZUO_TEST_SERVER='login.example.net:2593' \
TAZUO_TEST_WAIT=30 tools/mobile-ios/test-native-simulator.sh
```

The test additionally requires `Native world: READY` when an account is set.

The same `TAZUO_TEST_ACCOUNT` variable also pre-fills the login screen when
using `install-native-simulator.sh` (it does not enable automatic login):

```sh
export TAZUO_TEST_ACCOUNT='user:password'
export TAZUO_TEST_SERVER='login.example.net:2593'
tools/mobile-ios/install-native-simulator.sh
```

When only the account name is needed, use `TAZUO_TEST_USERNAME`; an optional
`TAZUO_TEST_PASSWORD` can provide the password without using the `user:password`
form.

Keep these exports in a local shell profile or ignored environment file; never
put the credential in the repository.
For a host-side touch smoke test, compile `drag-simulator.swift` and drag from
the joystick centre toward a direction while the Simulator is foregrounded.

Use `RUNTIME_IDENTIFIER=ios-arm64` with an Apple signing identity for a device
build. The simulator build uses the local ad hoc signature and is suitable for
`simctl` only.
