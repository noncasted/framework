#!/bin/bash
# Link lightbox-show and lightbox-pick into ~/.local/bin and check what the window needs.
# The window: Hyprland (hyprctl), Python with GTK 4 and WebKitGTK 6 (Arch: python-gobject, gtk4, webkitgtk-6.0).
set -euo pipefail

here=$(dirname "$(readlink -f "$0")")
bin="$HOME/.local/bin"

command -v hyprctl >/dev/null || { echo "install.sh: hyprctl not found: the window is placed by Hyprland" >&2; exit 1; }
python3 -c "import gi; gi.require_version('Gtk', '4.0'); gi.require_version('WebKit', '6.0')" \
  || { echo "install.sh: Python needs GTK 4 and WebKitGTK 6 (python-gobject, gtk4, webkitgtk-6.0)" >&2; exit 1; }

mkdir -p "$bin"
for name in lightbox-show lightbox-pick; do
  ln -sfn "$here/$name" "$bin/$name"
  echo "$bin/$name -> $here/$name"
done

settings="$HOME/.claude/settings.json"
if ! grep -q 'lightbox-show --hook' "$settings" 2>/dev/null; then
  cat <<'HOOK'
The images Claude reads are not shown yet: add to ~/.claude/settings.json, under "hooks":
  "PostToolUse": [{ "matcher": "Read", "hooks": [{ "type": "command", "command": "lightbox-show --hook 2>/dev/null || true", "async": true }] }]
HOOK
fi
