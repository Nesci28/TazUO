# iOS Legion runtime gate

The native iOS host links the current `ClassicUO.Client` assembly and
produces a signed ARM64 simulator application. The complete simulator gate now
passes: IronPython 3.4.2 starts inside the iOS process, evaluates a Python
expression, and reports the linked TazUO assembly version (`5.31.2.0`).
This is a real compatibility result, not a mocked Legion result. The build
exports the architecture-correct `Mono.Unix` symbols and maps its DllImport to
the iOS executable. The next gate is to run `LegionScripting.SetupPythonEngine`
with a real game world after FNA has created the client window.

The native build and install commands are:

```sh
tools/mobile-ios/build-native-simulator.sh
tools/mobile-ios/install-native-simulator.sh
```

To repeat the complete simulator test and save both the exception and a
screenshot, run:

```sh
tools/mobile-ios/test-native-simulator.sh
```

The command exits nonzero when the diagnostic app cannot write its runtime log,
which prevents a successful launch from being mistaken for a successful Legion
execution.
