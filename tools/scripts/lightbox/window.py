"""The window: a GTK4 application with a WebKit view of ui/, a Unix socket for the requests, the snapshots of the images."""
import json
import os
import select
import shutil
import socket
import threading
import time
from pathlib import Path

from session import hypr_dispatch, hyprctl_json

HERE = Path(__file__).resolve().parent
MIME = {
    ".png": "image/png", ".jpg": "image/jpeg", ".jpeg": "image/jpeg", ".gif": "image/gif", ".webp": "image/webp",
    ".bmp": "image/bmp", ".svg": "image/svg+xml", ".tif": "image/tiff", ".tiff": "image/tiff",
    ".html": "text/html", ".js": "text/javascript", ".css": "text/css",
}


class Store:
    """Snapshots of the shown images and the history of the viewer, kept in the cache folder of the terminal."""

    def __init__(self, folder):
        self.folder = folder
        self.images = folder / "images"
        self.images.mkdir(parents=True, exist_ok=True)
        self.index_file = folder / "history.json"
        self.history = json.loads(self.index_file.read_text()) if self.index_file.exists() else []
        self.next_id = max((int(name.split(".")[0]) for name in os.listdir(self.images)), default=0) + 1

    def snapshot(self, path):
        source = Path(path)
        name = f"{self.next_id}{source.suffix.lower()}"
        self.next_id += 1
        shutil.copyfile(source, self.images / name)
        return f"cv://img/{name}"

    def frames(self, path):
        source = Path(path)
        if source.is_dir():
            return [self.snapshot(p) for p in sorted(p for p in source.iterdir() if p.suffix.lower() == ".png")]
        return [self.snapshot(source)]

    def add_shown(self, items):
        added = []
        for item in items:
            entry = {"src": self.snapshot(item["path"]), "path": item["path"], "from": item["from"],
                     "time": time.strftime("%H:%M:%S")}
            self.history.append(entry)
            added.append(entry)
        self.index_file.write_text(json.dumps(self.history, ensure_ascii=False))
        return added

    def read(self, name):
        path = self.images / name
        if path.parent != self.images:
            raise PermissionError(name)
        return path.read_bytes(), MIME[path.suffix.lower()]


class App:
    def __init__(self, args):
        # On the NVIDIA EGL driver (610.57) the web process segfaults while exiting when Skia paints on the GPU: the
        # driver's exit-time teardown races WebKit's GL cleanup, and each closed window leaves a core dump and a
        # "Process crashed" notification. The web process inherits this environment; CPU painting avoids the crash.
        os.environ.setdefault("WEBKIT_SKIA_ENABLE_CPU_RENDERING", "1")
        import gi
        gi.require_version("Gtk", "4.0")
        gi.require_version("WebKit", "6.0")
        from gi.repository import Gio, GLib, Gtk, WebKit
        self.Gio, self.GLib, self.Gtk, self.WebKit = Gio, GLib, Gtk, WebKit
        self.args = args
        self.store = Store(Path(args.cache))
        self.ready = False
        self.outbox = []
        self.picks = {}
        self.pick_counter = 0
        self.monitor_size = tuple(int(v) for v in args.monitor_size.split("x"))
        self.application = Gtk.Application(application_id=args.app_id, flags=Gio.ApplicationFlags.NON_UNIQUE)
        self.application.connect("activate", self.on_activate)

    def run(self):
        self.application.run([])
        self.cancel_all("window closed")
        Path(self.args.socket).unlink(missing_ok=True)

    def on_activate(self, application):
        Gtk, WebKit = self.Gtk, self.WebKit
        self.window = Gtk.ApplicationWindow(application=application, title="Лайтбокс")
        self.window.set_default_size(900, 700)
        content = WebKit.UserContentManager()
        content.register_script_message_handler("cv", None)
        content.connect("script-message-received::cv", self.on_script_message)
        self.view = WebKit.WebView(user_content_manager=content)
        self.view.get_context().register_uri_scheme("cv", self.on_scheme)
        settings = self.view.get_settings()
        settings.set_enable_developer_extras(True)
        self.window.set_child(self.view)
        self.view.load_uri("cv://ui/index.html")
        self.window.present()
        threading.Thread(target=self.serve, daemon=True).start()
        if self.args.terminal_pid:
            self.GLib.timeout_add_seconds(2, self.watch_terminal)

    def watch_terminal(self):
        if Path(f"/proc/{self.args.terminal_pid}").exists():
            return True
        shutil.rmtree(self.args.cache, ignore_errors=True)
        Path(self.args.socket).with_suffix(".lock").unlink(missing_ok=True)
        self.application.quit()
        return False

    # ------------------------------------------------------------------------------------------------ web view side

    def on_scheme(self, scheme_request):
        Gio, GLib = self.Gio, self.GLib
        uri = scheme_request.get_uri()
        kind, _, name = uri.removeprefix("cv://").partition("/")
        try:
            if kind == "ui":
                path = HERE / "ui" / name
                if path.resolve().parent != (HERE / "ui").resolve():
                    raise PermissionError(name)
                data, mime = path.read_bytes(), MIME[path.suffix.lower()]
            elif kind == "img":
                data, mime = self.store.read(name)
            else:
                raise FileNotFoundError(uri)
        except (OSError, KeyError) as error:
            scheme_request.finish_error(GLib.Error.new_literal(Gio.io_error_quark(), str(error), 1))
            return
        stream = Gio.MemoryInputStream.new_from_bytes(GLib.Bytes.new(data))
        scheme_request.finish(stream, len(data), mime)

    def on_script_message(self, _content, value):
        message = json.loads(value.to_json(0))
        kind = message["type"]
        if kind == "ready":
            self.ready = True
            self.push({"type": "history", "items": self.store.history})
            for pick in self.picks.values():
                self.push({"type": "pick", "pick": pick["view"]})
            for queued in self.outbox:
                self.push(queued)
            self.outbox.clear()
        elif kind == "pick-result":
            self.finish_pick(message["id"], {"selected": message["selected"], "note": message["note"]})
        elif kind == "pick-cancel":
            self.finish_pick(message["id"], {"cancelled": "the user cancelled the pick"})
        else:
            raise ValueError(f"unknown message from the page: {kind}")

    def push(self, message):
        if not self.ready:
            self.outbox.append(message)
            return
        script = f"window.cv.receive({json.dumps(message, ensure_ascii=False)})"
        self.view.evaluate_javascript(script, -1, None, None, None, None, None)

    # --------------------------------------------------------------------------------------------- socket side

    def serve(self):
        server = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
        Path(self.args.socket).unlink(missing_ok=True)
        server.bind(self.args.socket)
        os.chmod(self.args.socket, 0o600)
        server.listen()
        while True:
            connection, _ = server.accept()
            threading.Thread(target=self.handle, args=(connection,), daemon=True).start()

    def handle(self, connection):
        with connection:
            line = connection.makefile("r", encoding="utf-8").readline()
            if not line:
                return
            message = json.loads(line)
            done = threading.Event()
            box = {}

            def reply(answer):
                box["answer"] = answer
                done.set()

            self.GLib.idle_add(self.dispatch, message, reply)
            while not done.wait(0.3):
                # A pick whose caller went away (its shell was killed) is withdrawn from the window.
                readable, _, _ = select.select([connection], [], [], 0)
                if readable and not connection.recv(1, socket.MSG_PEEK):
                    self.GLib.idle_add(lambda: self.abandon(message.get("_pick_id")))
                    return
            connection.sendall((json.dumps(box["answer"], ensure_ascii=False) + "\n").encode())

    def dispatch(self, message, reply):
        try:
            if message["cmd"] == "show":
                added = self.store.add_shown(message["items"])
                self.push({"type": "shown", "items": added})
                reply({"ok": True})
            elif message["cmd"] == "pick":
                self.start_pick(message, reply)
            elif message["cmd"] == "eval":
                self.view.evaluate_javascript(message["script"], -1, None, None, None, None, None)
                reply({"ok": True})
            elif message["cmd"] == "snapshot":
                self.snapshot(message["path"], reply)
            else:
                reply({"error": f"unknown command {message['cmd']}"})
        except OSError as error:
            reply({"error": str(error)})
        return False

    def snapshot(self, path, reply):
        """A PNG of the page as drawn, for checks of the window by a script."""
        WebKit = self.WebKit

        def done(view, result):
            view.get_snapshot_finish(result).save_to_png(path)
            reply({"ok": True})

        self.view.get_snapshot(WebKit.SnapshotRegion.VISIBLE, WebKit.SnapshotOptions.NONE, None, done)

    def start_pick(self, message, reply):
        for pick_id in list(self.picks):
            self.finish_pick(pick_id, {"cancelled": "replaced by a newer pick"})
        self.pick_counter += 1
        pick_id = self.pick_counter
        message["_pick_id"] = pick_id
        items = [{"label": item["label"], "caption": item["caption"], "path": item["path"],
                  "frames": self.store.frames(item["path"])} for item in message["items"]]
        view = {"id": pick_id, "title": message["title"], "from": message["from"], "delay": message["delay"],
                "time": time.strftime("%H:%M:%S"), "items": items}
        self.picks[pick_id] = {"reply": reply, "view": view}
        self.push({"type": "pick", "pick": view})
        self.enlarge()

    def finish_pick(self, pick_id, answer):
        pick = self.picks.pop(pick_id, None)
        if pick is None:
            return
        pick["reply"](answer)
        self.push({"type": "pick-closed", "id": pick_id, "answer": answer})

    def abandon(self, pick_id):
        pick = self.picks.pop(pick_id, None)
        if pick is not None:
            self.push({"type": "pick-closed", "id": pick_id, "answer": {"cancelled": "the caller went away"}})
        return False

    def cancel_all(self, reason):
        for pick_id in list(self.picks):
            self.finish_pick(pick_id, {"cancelled": reason})

    def enlarge(self):
        """A grid needs room: grow the window to 70% x 80% of the monitor, keeping its top right corner."""
        clients = [c for c in hyprctl_json("clients") if c["class"] == self.args.app_id and c["pid"] == os.getpid()]
        if not clients:
            return
        client = clients[0]
        width, height = self.monitor_size
        w, h = max(client["size"][0], width * 70 // 100), max(client["size"][1], height * 80 // 100)
        if (w, h) == tuple(client["size"]) or not client["floating"]:
            return
        address = client["address"]
        right = client["at"][0] + client["size"][0]
        hypr_dispatch(f"hl.dsp.window.resize({{ window = 'address:{address}', x = {w}, y = {h} }})")
        hypr_dispatch(f"hl.dsp.window.move({{ window = 'address:{address}', x = {right - w}, y = {client['at'][1]} }})")
