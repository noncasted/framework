# Modal dialogs of an editor with a window. They block its main thread, so MCP cannot reach them: the text is read from a capture of the
# window and a button is pressed by keys sent to that window. A dialog of Unity takes keys only with the focus: it gets it for a moment, then the
# focus returns to where it was. Requires Hyprland and XWayland.
import json
import os
import shutil
import subprocess
import time

from common import STATE_DIR, ControlError

ANSWER_WAIT = 3
SAFE_MODE_TITLE = "Enter Safe Mode?"


def _require_tools():
    for tool in ("hyprctl", "xprop", "import"):
        if not shutil.which(tool):
            raise ControlError(f"{tool} is not installed: dialogs of an editor cannot be read or answered here")
    if not os.environ.get("HYPRLAND_INSTANCE_SIGNATURE"):
        raise ControlError("not inside a Hyprland session: dialogs of an editor cannot be read or answered here")


def floating_windows(pid):
    if not os.environ.get("HYPRLAND_INSTANCE_SIGNATURE") or not shutil.which("hyprctl"):
        return []
    output = subprocess.run(["hyprctl", "clients", "-j"], capture_output=True, text=True).stdout
    return [client for client in json.loads(output) if client["pid"] == pid and client["floating"]]


def _x_windows():
    output = subprocess.run(["xprop", "-root", "_NET_CLIENT_LIST"], capture_output=True, text=True).stdout
    return [part.strip().rstrip(",") for part in output.split("#", 1)[-1].split(",") if "0x" in part]


def _x_window_of(pid, title):
    for window in _x_windows():
        output = subprocess.run(["xprop", "-id", window, "_NET_WM_PID", "_NET_WM_NAME"], capture_output=True, text=True).stdout
        if f"= {pid}" in output and f'"{title}"' in output:
            return window
    return None


def capture(pid):
    _require_tools()
    STATE_DIR.mkdir(parents=True, exist_ok=True)
    result = []
    for index, client in enumerate(floating_windows(pid)):
        window = _x_window_of(pid, client["title"])
        image = None
        if window:
            image = str(STATE_DIR / f"dialog-{pid}-{time.strftime('%H%M%S')}-{index}.png")
            subprocess.run(["import", "-window", window, image], check=True, capture_output=True)
        result.append({"address": client["address"], "title": client["title"], "image": image})
    return result


def ignore_safe_mode(pid):
    # A compile error at the start stops the editor on this dialog even with -ignoreCompilerErrors. Ignore loses nothing: the editor comes up on
    # the last good assemblies, the bridge works and the errors can be read and fixed. The buttons by Tab: Enter Safe Mode, Ignore, Quit.
    for client in floating_windows(pid):
        if client["title"] == SAFE_MODE_TITLE:
            return answer(pid, 2, client["address"])["answered"]
    return False


def _active_window():
    output = subprocess.run(["hyprctl", "activewindow", "-j"], capture_output=True, text=True).stdout
    return json.loads(output).get("address") if output.strip().startswith("{") else None


def _focus(address):
    subprocess.run(["hyprctl", "eval", f"hl.dispatch(hl.dsp.focus({{window='address:{address}'}}))"], check=True, capture_output=True)
    time.sleep(0.3)


def _press(address, key):
    code = f"hl.dispatch(hl.dsp.send_shortcut({{mods='', key='{key}', window='address:{address}'}}))"
    subprocess.run(["hyprctl", "eval", code], check=True, capture_output=True)
    time.sleep(0.4)


def answer(pid, button, address):
    # Button 1 is the one focused when the dialog opens, Tab walks to the next ones. Escape closes without a choice: button 0.
    _require_tools()
    windows = floating_windows(pid)
    if address is None:
        if len(windows) != 1:
            raise ControlError(f"{len(windows)} floating windows: read them with `editor dialog` and pass --address")
        address = windows[0]["address"]
    if address not in [client["address"] for client in windows]:
        raise ControlError(f"no floating window {address} of the editor {pid}")
    previous = _active_window()
    _focus(address)
    try:
        if button == 0:
            _press(address, "Escape")
        else:
            for _ in range(button - 1):
                _press(address, "Tab")
            _press(address, "Return")
    finally:
        if previous and previous != address:
            _focus(previous)
    deadline = time.time() + ANSWER_WAIT
    while time.time() < deadline:
        if address not in [client["address"] for client in floating_windows(pid)]:
            # The closing dialog gives the focus to the window of the editor, so it is returned once more.
            time.sleep(0.5)
            if previous and previous != address:
                _focus(previous)
            return {"answered": True, "address": address, "button": button}
        time.sleep(0.2)
    return {"answered": False, "address": address, "button": button, "error": "the window is still open: capture it again"}
