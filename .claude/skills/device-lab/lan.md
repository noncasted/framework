# LAN: a local build on a phone

`$WB/lan/`: `inject.sh` (puts `remote-log.js` first into the `<head>` of a copy of a build), `serve.py` (serves the copy like the
production nginx and appends `POST /log` to a file), `remote-log.js` (console, errors, page events and a frame window every 2 s),
`windows.py` (cuts the log into time windows of a run).

```
rm -rf /tmp/claude-1000/lan/<name> && cp -a <build> /tmp/claude-1000/lan/<name>/site
$WB/lan/inject.sh /tmp/claude-1000/lan/<name>/site
python3 $WB/lan/serve.py /tmp/claude-1000/lan/<name>/site --port 8770 --log /tmp/claude-1000/lan/<name>/log.txt   # background
# the phone opens the printed http://<lan ip>:8770/<autorunQuery>
python3 $WB/lan/windows.py /tmp/claude-1000/lan/<name>/log.txt --start HH:MM:SS --secs 330 --n 6 --client <phone ip> [--mark '<text>']
```

- Always a copy: `inject.sh` edits the `index.html` it is given; never the build of the game.
- Only tcp 8770-8771 reach this machine from the LAN (ufw; `serve.py` warns on any other port and the phone just fails to load).
  The phone sees the machine only with its VPN off. Stop the server by its pid (printed; or `ss -ltnp 'sport = :8770'`).
- A plain-http page is not a secure context: a browser does not decode `Content-Encoding: br` there, so a compressed build may not
  start over the LAN (WEBGL_TROUBLESHOOTING P3: an uncompressed build with `insecureHttpOption`). Check the first lines of the log.
- No telemetry: the production meta answers 403 to the LAN origin, so the measure is the remote log (`frames` windows: mean and
  worst frame, fps, frames over 50 ms; `gap` well above 2000 ms is a frozen or hidden page). `windows.py` also reads the console
  lines of a game's frame overlay and a draw counter when present.
- Safari serves a cached script: load an extra probe with a cache buster (`?t=<Date.now()>`).
- To drive the page on the phone: [ios.md](ios.md) (`iosctl.py open`) or [android.md](android.md) (`adbctl.py open`).
