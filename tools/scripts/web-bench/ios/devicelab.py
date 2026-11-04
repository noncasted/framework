#!/usr/bin/env python3
"""Shared configuration of the iOS tools of web-bench: the devices of this machine and the values of the game.

Machine values: ~/.config/device-lab/devices.json (override: DEVICE_LAB_DEVICES), data and tools in
~/.local/share/device-lab (override: DEVICE_LAB_HOME): pmd/ (venv of pymobiledevice3), ddi/<ver>/, run/ (pid files, logs).
Game values: <project>/.claude/device-lab.json (project: --project <dir>, default the current directory).

An iOS device entry:
  {"name": "iphone11", "kind": "ios", "udid": "...", "proxyPort": 9222, "ddi": "~/.local/share/device-lab/ddi/16.6"}
  {"name": "ipad7", "kind": "ios", "udid": "...", "proxyPort": 9223, "ddiAutoMount": true, "tunnel": "userspace"}
  ddi          - folder with DeveloperDiskImage.dmg(.signature), mounted with ideviceimagemounter (iOS <= 16)
  ddiAutoMount - mount with `pymobiledevice3 mounter auto-mount` instead (iOS 17+); exactly one of ddi / ddiAutoMount
  tunnel       - optional, "userspace": pass --userspace to the developer commands of pymobiledevice3 (iOS 17+,
                 no root); without it pymobiledevice3 falls back to the userspace tunnel itself with a warning

Command line (for the shell scripts next to it):
  devicelab.py shell <device>            prints DL_* shell assignments of the device (eval "$(...)")
  devicelab.py game <key> [--project D]  prints one value of .claude/device-lab.json of the game
  devicelab.py path pmd|python|run|data  prints a path of the machine
Example:
  vars=$(python3 devicelab.py shell ipad7) || exit 1; eval "$vars"; echo "$DL_UDID $DL_PORT"
"""
import json
import os
import shlex
import sys
from pathlib import Path


class ConfigError(Exception):
    pass


def data_dir():
    return Path(os.environ.get("DEVICE_LAB_HOME", "~/.local/share/device-lab")).expanduser()


def devices_file():
    return Path(os.environ.get("DEVICE_LAB_DEVICES", "~/.config/device-lab/devices.json")).expanduser()


def run_dir():
    path = data_dir() / "run"
    path.mkdir(parents=True, exist_ok=True)
    return path


def pmd_bin():
    return data_dir() / "pmd" / "bin" / "pymobiledevice3"


def pmd_python():
    return data_dir() / "pmd" / "bin" / "python"


def require_pmd():
    if not pmd_bin().exists():
        raise ConfigError(f"pymobiledevice3 is not installed at {pmd_bin()}: run web-bench/ios/setup.sh")
    return pmd_bin()


def load_devices():
    path = devices_file()
    if not path.exists():
        raise ConfigError(f"{path} is missing: run web-bench/ios/setup.sh, then fill in the devices")
    with open(path) as f:
        data = json.load(f)
    if not isinstance(data.get("devices"), list):
        raise ConfigError(f"{path}: no \"devices\" list")
    return data["devices"]


def ios_device(name):
    devices = load_devices()
    found = [d for d in devices if d.get("name") == name]
    if not found:
        names = ", ".join(str(d.get("name")) for d in devices)
        raise ConfigError(f"no device '{name}' in {devices_file()} (known: {names})")
    device = found[0]
    if device.get("kind") != "ios":
        raise ConfigError(f"device '{name}' is of kind '{device.get('kind')}', not ios")
    udid = device.get("udid")
    if not isinstance(udid, str) or not udid or "<" in udid:
        raise ConfigError(f"device '{name}': set its udid (idevice_id -l) in {devices_file()}")
    if not isinstance(device.get("proxyPort"), int):
        raise ConfigError(f"device '{name}': proxyPort (int) is required in {devices_file()}")
    has_ddi = bool(device.get("ddi"))
    auto = device.get("ddiAutoMount") is True
    if has_ddi == auto:
        raise ConfigError(f"device '{name}': set exactly one of \"ddi\" (folder) or \"ddiAutoMount\": true")
    if has_ddi:
        ddi = Path(device["ddi"]).expanduser()
        for file in ("DeveloperDiskImage.dmg", "DeveloperDiskImage.dmg.signature"):
            if not (ddi / file).exists():
                raise ConfigError(f"device '{name}': {ddi / file} is missing (setup.sh --ddi <src>:<version>)")
        device = dict(device, ddi=str(ddi))
    tunnel = device.get("tunnel")
    if tunnel not in (None, "userspace"):
        raise ConfigError(f"device '{name}': tunnel must be \"userspace\" or absent, got {tunnel!r}")
    return device


def pmd_device_args(device):
    """Arguments of a pymobiledevice3 developer command that select the device."""
    args = ["--udid", device["udid"]]
    if device.get("tunnel") == "userspace":
        args.append("--userspace")
    return args


def game_config(project=None):
    root = Path(project or os.getcwd()).expanduser().resolve()
    path = root / ".claude" / "device-lab.json"
    if not path.exists():
        raise ConfigError(f"{path} is missing: run from the root of the game or pass --project <dir>")
    with open(path) as f:
        return json.load(f), path


def game_value(key, project=None):
    config, path = game_config(project)
    if key not in config:
        raise ConfigError(f"{path}: key \"{key}\" is missing")
    return config[key]


def fail(error):
    print(f"error: {error}", file=sys.stderr)
    sys.exit(2)


def main(argv):
    if not argv or argv[0] in ("-h", "--help"):
        print(__doc__)
        return
    command = argv[0]
    if command == "shell" and len(argv) == 2:
        device = ios_device(argv[1])
        values = {
            "DL_NAME": device["name"],
            "DL_UDID": device["udid"],
            "DL_PORT": str(device["proxyPort"]),
            "DL_DDI": device.get("ddi", ""),
            "DL_DDI_AUTO": "1" if device.get("ddiAutoMount") else "",
            "DL_PMD_ARGS": " ".join(pmd_device_args(device)),
            "DL_PMD": str(require_pmd()),
            "DL_PY": str(pmd_python()),
            "DL_RUN": str(run_dir()),
        }
        for key, value in values.items():
            print(f"{key}={shlex.quote(value)}")
    elif command == "game" and len(argv) in (2, 4):
        project = None
        if len(argv) == 4:
            if argv[2] != "--project":
                raise ConfigError("usage: devicelab.py game <key> [--project <dir>]")
            project = argv[3]
        value = game_value(argv[1], project)
        print(value if isinstance(value, str) else json.dumps(value))
    elif command == "path" and len(argv) == 2:
        paths = {"pmd": pmd_bin(), "python": pmd_python(), "run": run_dir(), "data": data_dir()}
        if argv[1] not in paths:
            raise ConfigError(f"unknown path '{argv[1]}' (pmd, python, run, data)")
        print(paths[argv[1]])
    else:
        raise ConfigError("usage: devicelab.py shell <device> | game <key> [--project <dir>] | path <name> (--help)")


if __name__ == "__main__":
    try:
        main(sys.argv[1:])
    except (ConfigError, json.JSONDecodeError) as e:
        fail(e)
