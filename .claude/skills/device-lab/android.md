# Android: Chrome over adb

`$WB/android/adbctl.py`: adb, the DevTools socket of Chrome (`localabstract:chrome_devtools_remote`) forwarded to a local port,
CDP over a stdlib websocket.

```
A() { python3 "$WB/android/adbctl.py" --device android-mali "$@"; }   # or --serial <adb serial>; --port overrides cdpPort
A check                        # adb present, devices, the chosen one, Chrome installed, its DevTools socket open
A forward                      # adb forward tcp:<cdpPort> localabstract:chrome_devtools_remote (list / eval do it themselves)
A list                         # pages: index, id, url, title
A open '<url>'                # am start -a android.intent.action.VIEW
A eval '<js>' [--page <part of url / title / id>]   # promise awaited, value as JSON; a page exception -> exit 1
A screenshot <out.png>        # adb exec-out screencap -p, whatever is on top
```

- The local port is `--port`, else `cdpPort` of the device, else 9222 - the port of the iPhone proxy too: give the Android device its
  own `cdpPort` in `devices.json`.
- Machine: adb (Arch `sudo pacman -S android-tools`; `android-udev` when the device shows "no permissions"). Not installed on this
  machine as of 2026-11-04 and not yet tried on a device: `check` without adb exits 1 with the install line - tell the user, the
  install needs sudo.
- Phone, once: developer options (tap Build number 7 times), USB debugging on, "Always allow from this computer"; Chrome started (its
  socket exists only while it runs), the screen unlocked while measuring. `ADBCTL_PACKAGE` for Chrome Beta / Dev.
- A production page on the phone (`open https://<host>/<autorunQuery>`) reaches the telemetry: read it with `client-telemetry`.
  A LAN build sends none ([lan.md](lan.md)): measure it with the remote log or `eval`.
