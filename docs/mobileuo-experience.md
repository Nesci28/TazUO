# MobileUO experience reference

MobileUO is the reference experience for the TazUO mobile client. Its public project is a Unity
application derived in part from ClassicUO and runs on iOS and Android. The important product
choices are visible in its `ClientRunner`, `GameScene`, and `MobileJoystick` classes:

* the game scene remains the real ClassicUO renderer and UI gump system;
* a virtual analog joystick drives the same movement input as desktop controls;
* joystick size, opacity, dead zone, run threshold, and position are user preferences;
* touch input coexists with target, context, chat, modifier, and escape actions;
* inventory, status, map, journal, and assistant windows remain native game panels rather than
  separate web pages.

The browser POC is only a contract test. The production iOS path is now tracked under
`mobile-ios/`: it bootstraps the MobileUO Unity project and installs native touch controls that
call the real `GameScene`, target manager, and speech packet actions. The final IPA therefore
renders the real game and connects directly to the shard; it does not stream a desktop client.

MobileUO is licensed AGPLv3 and does not redistribute Ultima Online assets. TazUO must preserve the
licenses of its own code and require the player to provide legally obtained UO data files.

Reference: <https://github.com/MobileUO/MobileUO/>
