# The deploy registry (deploy/registry.json, deploy/CLAUDE.md): read from the latest main of the framework this script lives in,
# and the project of a command: --project, else the repository of the current directory.
import json
import re
import subprocess
import time
from pathlib import Path

from common import SERVICE, USAGE, Failure

# tools/scripts/deploy/registry.py: the framework itself, tools/framework of a game or /projects/framework alike.
FRAMEWORK = Path(__file__).resolve().parents[3]
_PATH = "deploy/registry.json"
# A fetch costs a round trip to GitHub (10 s where git waits for IPv6 first); origin/main of the last few minutes is fresh enough.
FETCH_SECONDS = 300
# Written in the git folder of the framework after a fetch of main by this script: another fetch (of a branch, of the IDE)
# may not move origin/main, so FETCH_HEAD says nothing about its age.
_STAMP = "deploy-registry.fetched"
_NAMESPACE = re.compile(r"^[a-z0-9][a-z0-9-]*$")
_REMOTE = re.compile(r"[:/]([^/:]+/[^/]+?)(?:\.git)?/?$")


class Target:
    # One project on one server: what every command of the stores needs.
    def __init__(self, name, project, server_key, server, namespace):
        self.name = name
        self.project = project
        self.server_key = server_key
        self.server = server
        self.namespace = namespace


def load(path=None, refresh=False):
    if path:
        try:
            text = Path(path).read_text()
        except OSError as error:
            raise Failure(USAGE, f"cannot read the registry {path}: {error.strerror}") from None

        return _parse(text, str(path))

    stamp = Path(_git("rev-parse", "--path-format=absolute", "--git-path", _STAMP).strip())

    if refresh or not stamp.exists() or time.time() - stamp.stat().st_mtime > FETCH_SECONDS:
        _git("fetch", "-q", "origin", "main")
        stamp.touch()

    return _parse(_git("show", f"origin/main:{_PATH}"), f"origin/main:{_PATH} of {FRAMEWORK}")


def find(registry, name):
    projects = registry["projects"]

    if name not in projects:
        raise Failure(USAGE, f"no project {name} in the registry; known: {', '.join(projects)}")

    return projects[name]


def current(registry, directory):
    # The project of a directory: its origin matched against projects.*.repository, else the name of its root.
    root = _run(["git", "-C", str(directory), "rev-parse", "--show-toplevel"], check=False)

    if root is None:
        return None

    root = root.strip()
    remote = _run(["git", "-C", root, "remote", "get-url", "origin"], check=False)
    match = _REMOTE.search(remote.strip()) if remote else None

    for name, project in registry["projects"].items():
        if match and project.get("repository", "").lower() == match.group(1).lower():
            return name

    name = Path(root).name

    return name if name in registry["projects"] else None


def target(registry, name, directory, namespace=None):
    name = name or current(registry, directory)

    if not name:
        raise Failure(USAGE, f"{directory} is not a project of the registry: pass --project (known: {', '.join(registry['projects'])})")

    project = find(registry, name)

    if project.get("deployed", True) is False:
        raise Failure(USAGE, f"{name} is not deployed ({project.get('note', 'deployed: false')})")

    namespace = _namespace(registry, name, namespace)

    if not _NAMESPACE.match(namespace):
        raise Failure(USAGE, f"a namespace is lowercase letters, digits and dashes, not {namespace!r}")

    server_key = project["server"]

    return Target(name, project, server_key, registry["servers"][server_key], namespace)


def _namespace(registry, name, override):
    # An override reads a namespace nobody owns (smoke-test), never the logs of another project under the name of this one.
    own = registry["projects"][name]["namespace"]

    if not override or override == own:
        return own

    owners = [other for other, project in registry["projects"].items() if project.get("namespace") == override]

    if owners:
        raise Failure(USAGE, f"the namespace {override} belongs to {owners[0]}: pass --project {owners[0]}")

    return override


def _parse(text, origin):
    try:
        registry = json.loads(text)
    except json.JSONDecodeError as error:
        raise Failure(SERVICE, f"{origin} is not JSON: {error}") from None

    for key in ("servers", "projects"):
        if key not in registry:
            raise Failure(SERVICE, f"{origin} has no {key}")

    return registry


def _git(*arguments):
    return _run(["git", "-C", str(FRAMEWORK), *arguments], check=True)


def _run(command, check):
    try:
        result = subprocess.run(command, capture_output=True, text=True, timeout=60)
    except subprocess.TimeoutExpired:
        raise Failure(SERVICE, f"{' '.join(command)}: no answer in 60 s") from None

    if result.returncode == 0:
        return result.stdout

    if check:
        raise Failure(SERVICE, f"{' '.join(command)} failed: {result.stderr.strip() or result.returncode}")

    return None
