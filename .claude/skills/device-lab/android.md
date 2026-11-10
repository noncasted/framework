# Android: Chrome over adb

`$WB/android/adbctl.py`: adb, the DevTools socket of Chrome (`localabstract:chrome_devtools_remote`) forwarded to a local port,
CDP over a stdlib websocket.

```
A() { python3 "$WB/android/adbctl.py" --device android-mali "$@"; }   # or --serial <adb serial>; --port overrides cdpPort
A check                        # adb present, devices, the chosen one, Chrome installed, its DevTools socket open
A forward                      # adb forward tcp:<cdpPort> localabstract:chrome_devtools_remote (list / eval do it themselves)
A list                         # pages: index, id, url, title
A open '<url>'                # am start -a android.intent.action.VIEW
A eval '<js>' [--page <part of url / title / id>] [--timeout 30]   # promise awaited, value as JSON; a page exception -> exit 1
A navigate '<url>' [--page ...]  # Page.navigate (about:blank stops a game); never location.href= in eval: its answer never comes
A close [--page ...]             # closes the tab: the game in it ends
A screenshot <out.png>        # adb exec-out screencap -p, whatever is on top
```

- The local port is `--port`, else `cdpPort` of the device, else 9222 - the port of the iPhone proxy too: give the Android device its
  own `cdpPort` in `devices.json`.
- Machine: adb (Arch `sudo pacman -S android-tools`; `android-udev` when the device shows "no permissions"). Installed on this
  machine 2026-11-09 without `android-udev` (its mirror download failed; systemd `uaccess` was enough, no "no permissions").
  `check` without adb exits 1 with the install line - tell the user, the install needs sudo.
- Device of this machine (2026-11-09): Redmi 12 (`23053RN02Y`, MT6768, Mali-G52 MC2, Android 13, Chrome 156, DPR 2.75), serial
  `74fb031e7d78`. Chrome reports the frozen UA `Android 10; K` - judge the device by `WEBGL_debug_renderer_info`, not by the UA.
- Xiaomi: USB debugging off shows the phone as `2717:ff40` (MTP) in `lsusb`, on as `2717:ff08` (ADB Interface); a fresh machine
  is `unauthorized` until the phone accepts "Allow USB debugging?". "USB debugging (Security settings)" is needed for input.
- Phone, once: developer options (tap Build number 7 times), USB debugging on, "Always allow from this computer"; Chrome started (its
  socket exists only while it runs), the screen unlocked while measuring. `ADBCTL_PACKAGE` for Chrome Beta / Dev.
- A page busy in a slow mode (`?glcheck`, a probe on every draw) answers CDP late or never: `--timeout`, and close the tab when
  it does not answer. A probe of the GL calls wraps `WebGL2RenderingContext.prototype` before the context is made
  (`Page.addScriptToEvaluateOnNewDocument` on a websocket kept open over the navigation); under `?glcheck` the methods of the
  context itself are wrapped, so wrap the instance (`canvas.getContext('webgl2')`) instead. Keep a probe to one `getError` per
  draw: 16 `getVertexAttrib` per draw froze a Redmi 12 page.
- A local build on the phone without the LAN problems of plain http (brotli, insecure http, [lan.md](lan.md)): serve it with
  `lan/serve.py` (inject `remote-log.js` first) and `adb reverse tcp:8770 tcp:8770`, then `open http://localhost:8770/...`:
  `localhost` is a secure context, so the compressed build of `deploy-client --no-deploy` starts as it is. The meta refuses it
  (offline mode: the game server of the session runs in the page itself, so its frame times are not those of a production
  run) and it sends no telemetry: the measure is the remote log (`lan/windows.py --client 127.0.0.1`). After: close the tab,
  `adb reverse --remove tcp:8770`, stop the server by its pid.
- A production page on the phone (`open https://<host>/<autorunQuery>`) reaches the telemetry: read it with `client-telemetry`.
  A LAN build sends none ([lan.md](lan.md)): measure it with the remote log or `eval`.
- Plugging an iPhone / iPad on the same hub re-enumerates USB (`usbmuxd`): adb loses the phone (`no devices`, `lsusb` still
  shows `ADB Interface`) and the `adb reverse` with it, while the page goes on. `adb kill-server; adb start-server`, wait up to
  40 s for `device` (twice if needed), then `adb reverse` again.
