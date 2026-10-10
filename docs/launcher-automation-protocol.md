# Launcher automation contract

TUO-Launcher remains the owner of account credentials. A launcher integration
must expose a local, single-client control channel for Legion automation.

The transport may be a localhost named pipe, Unix domain socket, or loopback
HTTP endpoint. Messages are JSON objects with a `command` field:

* `activate_profile { profile: string }` selects and launches the configured
  TazUO profile, returning `{ profile, state }`.
* `run_sequence { profiles: string[], script?: string }` starts a sequential profile run. After
  each client finishes, Legion sends `profile_done`; the launcher closes that
  client and starts the next profile.
* `profile_done { success?: boolean }` advances the active sequence and starts
  the next profile after the configured delay.
* `script_status { profile, step, message, progress }` updates the launcher's
  visual status panel with the current Legion step.
* `state` returns `{ profile, state, step, total, message }`, where `state` is
  one of `stopped`, `starting`, `running`, `waiting`, or `error`.
* `stop` stops the sequence, cancels pending delays, and closes the tracked
  TazUO client.

The channel must serialize commands, reject unknown profiles, and return an
error object containing a stable `code` and human-readable `message`. It must
not return passwords or decrypted account data. The TazUO Legion API provides
`ConnectSaved`, `SelectCharacterByName`, `Disconnect`, `LoginState`, and
`WaitForLoginState` for the in-client half of the handshake.

The launcher generates a local token automatically when one is not supplied;
setting `TAZUO_LAUNCHER_TOKEN` is still useful when using the helper
`TUO-Launcher/tools/launcher-sequence.py`, which sends a complete ten-profile
sequence using that token.
