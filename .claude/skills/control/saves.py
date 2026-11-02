# The work of an agent in a copy reaches the repository of the project by itself: a copy has its own .git, the project
# knows nothing of it, and removing the folder would lose the work.
#   refs/heads/copy/<name>/<branch> in the project: commits not delivered yet, pushed by a post-commit hook and by every save.
#   refs/copies/<name>/wip in the project: a snapshot of the uncommitted files, parents HEAD and the base when there is one.
#   refs/control/base in the copy: the state at create, so what the project already had uncommitted is not work of the agent.
# Each submodule goes to the same submodule of the project. Nothing here touches the files or the index of the copy.
import fcntl
import json
import os
import subprocess
import time
from pathlib import Path

from common import ControlError, STATE_DIR

MARKER = ".control-copy.json"
BASE_REF = "refs/control/base"
WIP_REF = "refs/control/wip"
HOOK_START = "# >>> control copy"
HOOK_END = "# <<< control copy"
HOOK_BLOCK = HOOK_START + """: every commit reaches the project at once
branch=$(git symbolic-ref -q --short HEAD || echo detached)
git push -q --no-verify {source} "+HEAD:refs/heads/copy/{name}/$branch" || echo "control: the commit did not reach the project, the next save retries" >&2
""" + HOOK_END + "\n"
# Without a name git refuses to make a commit where the user has no config: the timer has a bare environment.
IDENTITY = {"GIT_AUTHOR_NAME": "control", "GIT_AUTHOR_EMAIL": "control@localhost", "GIT_COMMITTER_NAME": "control", "GIT_COMMITTER_EMAIL": "control@localhost"}
FILES_SHOWN = 20


def git(repo, *args, env=None):
    result = subprocess.run(["git", "-C", str(repo), *args], capture_output=True, text=True, errors="replace", env={**os.environ, **(env or {})})
    if result.returncode != 0:
        raise ControlError(f"git {' '.join(args[:3])} in {repo}: {result.stderr.strip()[:300]}")
    return result.stdout.strip()


def ref_of(repo, name):
    result = subprocess.run(["git", "-C", str(repo), "rev-parse", "--verify", "-q", name], capture_output=True, text=True)
    return result.stdout.strip()


def has_commit(repo, sha):
    return subprocess.run(["git", "-C", str(repo), "cat-file", "-e", f"{sha}^{{commit}}"], capture_output=True).returncode == 0


def submodules(repo):
    paths = [line.split("\t", 1)[1] for line in git(repo, "ls-files", "-s").splitlines() if line.startswith("160000 ")]
    return [path for path in paths if (Path(repo) / path / ".git").exists()]


def repositories(root, source):
    yield root, source, ""
    for path in submodules(root):
        if (source / path / ".git").exists():
            yield root / path, source / path, path + "/"


def prepare(root, name, source):
    # Right after the copy: the base has to be taken before the agent changes anything.
    for repo, target, _ in repositories(root, source):
        _ensure(repo, target, name)
        git(repo, "update-ref", BASE_REF, _commit(repo, _snapshot_tree(repo), [git(repo, "rev-parse", "HEAD")]))


def save(root, name, source):
    if not Path(source).is_dir():
        raise ControlError(f"the project of the copy is gone: {source}")
    commits, files = [], []
    with open(Path(git(root, "rev-parse", "--absolute-git-dir")) / "control-save.lock", "w") as lock_file:
        fcntl.flock(lock_file, fcntl.LOCK_EX)
        for repo, target, prefix in repositories(root, Path(source)):
            _ensure(repo, target, name)
            commits += [prefix + commit for commit in _save_commits(repo, target, name)]
            files += [prefix + path for path in _save_changes(repo, target, name)]
    return {"commits": commits, "changed": len(files), "files": sorted(files)[:FILES_SHOWN]}


def is_pending(pending):
    return bool(pending["commits"] or pending["changed"])


def leftovers(source):
    # Copies whose folder is gone and whose work stays in the project, by name.
    names = set()
    for repo in [source, *(source / path for path in submodules(source))]:
        for ref in git(repo, "for-each-ref", "--format=%(refname)", "refs/copies", "refs/heads/copy").splitlines():
            names.add(ref.split("/")[2 if ref.startswith("refs/copies/") else 3])
    return names


def drop(source, name):
    for repo in [source, *(source / path for path in submodules(source))]:
        for ref in git(repo, "for-each-ref", "--format=%(refname)", f"refs/copies/{name}/", f"refs/heads/copy/{name}/").splitlines():
            git(repo, "update-ref", "-d", ref)


def sweep(base):
    # The timer: every copy of every project, a failure of one does not stop the others. What it finds goes to a file the status line reads.
    result = {"updated": int(time.time()), "pending": [], "unknown": [], "errors": {}}
    for project in sorted(path for path in Path(base).iterdir() if path.is_dir()) if Path(base).exists() else []:
        for root in sorted(path for path in project.iterdir() if path.is_dir()):
            label = f"{project.name}/{root.name}"
            if not (root / MARKER).exists():
                result["unknown"].append(label)
                continue
            try:
                if is_pending(save(root, root.name, json.loads((root / MARKER).read_text())["source"])):
                    result["pending"].append(label)
            except (ControlError, OSError) as error:
                result["errors"][label] = str(error)
    STATE_DIR.mkdir(parents=True, exist_ok=True)
    path = STATE_DIR / "copies.json"
    temporary = path.with_suffix(".tmp")
    temporary.write_text(json.dumps(result, indent=2, ensure_ascii=False))
    temporary.replace(path)
    return result


def _ensure(repo, target, name):
    # Copies made by an older version get all of this on the first save, so it runs every time and changes only what differs.
    git_dir = Path(git(repo, "rev-parse", "--absolute-git-dir"))
    # A reflink copy has new inodes and ctimes: without this git would hash the whole project once.
    git(repo, "config", "core.checkStat", "minimal")
    _ensure_exclude(git_dir)
    _ensure_hook(repo, git_dir, target, name)


def _ensure_exclude(git_dir):
    exclude = git_dir / "info" / "exclude"
    exclude.parent.mkdir(exist_ok=True)
    text = exclude.read_text() if exclude.exists() else ""
    if "__pycache__/" not in text:
        exclude.write_text(text + "\n__pycache__/\n")


def _hooks_dir(repo, git_dir, target):
    configured = subprocess.run(["git", "-C", str(repo), "config", "--local", "core.hooksPath"], capture_output=True, text=True).stdout.strip()
    if not configured:
        return git_dir / "hooks"
    target_dir = Path(git(target, "rev-parse", "--absolute-git-dir"))
    resolved = (Path(repo) / configured).resolve()
    if resolved == (target_dir / "hooks").resolve():
        # An absolute path copied with .git: the hooks of the project would run here, and ours would be written into the project.
        git(repo, "config", "--local", "--unset", "core.hooksPath")
        return git_dir / "hooks"
    if resolved.is_relative_to(git_dir):
        return resolved
    raise ControlError(f"core.hooksPath of {repo} is {configured}: the post-commit hook of the copy has no place to go")


def _ensure_hook(repo, git_dir, target, name):
    hooks = _hooks_dir(repo, git_dir, target)
    hooks.mkdir(exist_ok=True)
    hook = hooks / "post-commit"
    block = HOOK_BLOCK.format(source=_shell_quote(str(target)), name=name)
    text = hook.read_text() if hook.exists() else "#!/bin/sh\n"
    if HOOK_START in text:
        if block in text:
            return
        text = text[:text.index(HOOK_START)]
    # After the hook of the project, git-lfs for one: that one stays.
    hook.write_text(text.rstrip("\n") + "\n" + block)
    hook.chmod(0o755)


def _shell_quote(text):
    return "'" + text.replace("'", "'\\''") + "'"


def _snapshot_tree(repo):
    # A temporary index starts from the real one, so only changed files are hashed and the index of the agent stays as it is.
    git_dir = Path(git(repo, "rev-parse", "--absolute-git-dir"))
    index = git_dir / f"control-index-{os.getpid()}"
    try:
        real = git_dir / "index"
        if real.exists():
            index.write_bytes(real.read_bytes())
        env = {"GIT_INDEX_FILE": str(index)}
        git(repo, "add", "-A", env=env)
        return git(repo, "write-tree", env=env)
    finally:
        index.unlink(missing_ok=True)


def _commit(repo, tree, parents):
    arguments = [item for parent in parents for item in ("-p", parent)]
    return git(repo, "commit-tree", tree, *arguments, "-m", "control: snapshot of a copy", env=IDENTITY)


def _push(repo, target, sha, ref):
    git(repo, "push", "-q", "--no-verify", str(target), f"+{sha}:{ref}")
    # A pointer in a snapshot is worth nothing without its file, and --no-verify skips the pre-push of git-lfs.
    objects = Path(git(repo, "rev-parse", "--absolute-git-dir")) / "lfs" / "objects"
    if objects.is_dir():
        destination = Path(git(target, "rev-parse", "--absolute-git-dir")) / "lfs" / "objects"
        destination.mkdir(parents=True, exist_ok=True)
        copied = subprocess.run(["rsync", "-a", "--ignore-existing", f"{objects}/", f"{destination}/"], capture_output=True, text=True)
        if copied.returncode != 0:
            raise ControlError(f"large files of {repo} did not reach the project: {copied.stderr.strip()[:300]}")


def _branches(repo):
    heads = {}
    for line in git(repo, "for-each-ref", "--format=%(refname:short) %(objectname)", "refs/heads").splitlines():
        branch, sha = line.rsplit(" ", 1)
        if not branch.startswith("copy/"):
            heads[branch] = sha
    if not git(repo, "branch", "--show-current"):
        heads["detached"] = git(repo, "rev-parse", "HEAD")
    return heads


def _pending_commits(target, tip):
    # Not reachable from what the project has of its own, and no commit there with the same patch (a rebase, a cherry-pick).
    found = git(target, "rev-list", "--no-merges", tip, "--not", "--exclude=copy/*", "--branches", "--remotes", "HEAD").split()
    if not found:
        return []
    delivered = {line[2:] for line in git(target, "cherry", "HEAD", tip).splitlines() if line.startswith("- ")}
    return [sha for sha in found if sha not in delivered]


def _save_commits(repo, target, name):
    prefix = f"refs/heads/copy/{name}/"
    kept = {ref: sha for ref, sha in (line.split() for line in git(target, "for-each-ref", "--format=%(refname) %(objectname)", prefix).splitlines())}
    pending = []
    for branch, tip in _branches(repo).items():
        ref = prefix + branch
        if not has_commit(target, tip):
            _push(repo, target, tip, ref)
            kept[ref] = tip
        found = _pending_commits(target, tip)
        pending += [sha for sha in found if sha not in pending]
        if found:
            if kept.get(ref) != tip:
                git(target, "update-ref", ref, tip)
            kept.pop(ref, None)
    # What is left is delivered, or a branch the agent deleted.
    for ref in kept:
        git(target, "update-ref", "-d", ref)
    if not pending:
        return []
    return git(target, "log", "--no-walk", "--format=%h %s", *pending).splitlines()


def _names(repo, first, second):
    return set(git(repo, "diff", "--name-only", "--no-renames", "-z", first, second).split("\0")) - {""}


def _entries(repo, tree):
    # Path -> blob. The two repositories lack objects of each other but have the same ids for the same content.
    entries = {}
    for item in git(repo, "ls-tree", "-r", "-z", tree).split("\0"):
        if item:
            meta, path = item.split("\t", 1)
            entries[path] = meta.split()[2]
    return entries


def _agent_files(repo, target, tree):
    # Differs from HEAD (not committed), from the base (not inherited), from the project (not carried over by hand).
    changed = _names(repo, "HEAD", tree)
    base = ref_of(repo, BASE_REF)
    if base:
        changed &= _names(repo, base, tree)
    if not changed:
        return []
    ours, theirs = _entries(repo, tree), _entries(target, "HEAD")
    return [path for path in changed if ours.get(path) != theirs.get(path)]


def _save_changes(repo, target, name):
    ref = f"refs/copies/{name}/wip"
    tree = _snapshot_tree(repo)
    files = _agent_files(repo, target, tree)
    previous = ref_of(repo, WIP_REF)
    if not files:
        if previous:
            git(repo, "update-ref", "-d", WIP_REF)
        if ref_of(target, ref):
            git(target, "update-ref", "-d", ref)
        return []
    parents = [git(repo, "rev-parse", "HEAD")] + ([ref_of(repo, BASE_REF)] if ref_of(repo, BASE_REF) else [])
    same = previous and git(repo, "rev-parse", f"{previous}^{{tree}}") == tree and git(repo, "rev-list", "--parents", "-n1", previous).split()[1:] == parents
    commit = previous if same else _commit(repo, tree, parents)
    if ref_of(target, ref) != commit:
        _push(repo, target, commit, ref)
    git(repo, "update-ref", WIP_REF, commit)
    return files
