# The work of an agent in a copy must reach the repository of the project without anyone asking.
# Runs the real `control` on a temporary project with a submodule: python3 -m unittest discover -s .claude/skills/control/tests
# The temporary folder is on /var/tmp: a copy needs reflinks, /tmp is usually tmpfs.
import json
import os
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

CONTROL = Path(__file__).resolve().parent.parent / "control"
TEST_DIR = os.environ.get("CONTROL_TEST_DIR", "/var/tmp")


class CopySavesTests(unittest.TestCase):
    def setUp(self):
        self.temp = Path(tempfile.mkdtemp(prefix="control-test-", dir=TEST_DIR))
        self.addCleanup(shutil.rmtree, self.temp)
        self.env = {
            "PATH": os.environ["PATH"],
            "HOME": str(self.temp / "home"),
            "CONTROL_COPY_DIR": str(self.temp / "copies"),
            "GIT_AUTHOR_NAME": "test", "GIT_AUTHOR_EMAIL": "test@test", "GIT_COMMITTER_NAME": "test", "GIT_COMMITTER_EMAIL": "test@test",
            "GIT_CONFIG_NOSYSTEM": "1",
        }
        (self.temp / "home").mkdir()
        self.source = self.temp / "game"
        self._make_project()

    def _make_project(self):
        framework = self.temp / "framework-origin"
        framework.mkdir()
        self.git(framework, "init", "-q", "-b", "main")
        (framework / "code.cs").write_text("framework\n")
        self.git(framework, "add", "-A")
        self.git(framework, "commit", "-q", "-m", "framework")

        self.source.mkdir()
        self.git(self.source, "init", "-q", "-b", "main")
        (self.source / ".gitignore").write_text("Library/\n")
        (self.source / "client" / "Assets").mkdir(parents=True)
        (self.source / "client" / "Assets" / "game.cs").write_text("game\n")
        (self.source / "client" / "Assets" / "scene.unity").write_text("scene\n")
        (self.source / "client" / "Library").mkdir()
        (self.source / "client" / "Library" / "cache").write_text("cache\n")
        self.git(self.source, "-c", "protocol.file.allow=always", "submodule", "add", "-q", str(framework), "tools/framework")
        self.git(self.source, "add", "-A")
        self.git(self.source, "commit", "-q", "-m", "start")

    def git(self, repo, *args):
        return subprocess.run(["git", "-C", str(repo), *args], env=self.env, check=True, capture_output=True, text=True).stdout.strip()

    def ref(self, repo, name):
        result = subprocess.run(["git", "-C", str(repo), "rev-parse", "--verify", "-q", name], env=self.env, capture_output=True, text=True)
        return result.stdout.strip()

    def run_control(self, *args, cwd=None):
        return subprocess.run([str(CONTROL), *args], cwd=cwd or self.source, env=self.env, capture_output=True, text=True)

    def control(self, *args, cwd=None):
        result = self.run_control(*args, cwd=cwd)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        return json.loads(result.stdout)

    def create(self, name="work"):
        self.control("copy", "create", name, "--no-editor")
        return self.temp / "copies" / self.source.name / name

    def entry(self, name="work"):
        return next(item for item in self.control("copy", "list") if item["copy"] == name)

    def commit_in(self, repo, file, text, message):
        Path(repo, file).parent.mkdir(parents=True, exist_ok=True)
        Path(repo, file).write_text(text)
        self.git(repo, "add", "-A")
        self.git(repo, "commit", "-q", "-m", message)

    def test_commit_in_a_copy_reaches_the_project_at_once(self):
        copy = self.create()
        self.git(copy, "switch", "-q", "-c", "web/feature")
        self.commit_in(copy, "client/Assets/game.cs", "changed\n", "feature")
        self.assertEqual(self.ref(self.source, "refs/heads/copy/work/web/feature"), self.ref(copy, "HEAD"))
        self.assertEqual(self.git(self.source, "branch", "--show-current"), "main")

    def test_commit_on_main_of_a_copy_does_not_move_main_of_the_project(self):
        copy = self.create()
        before = self.ref(self.source, "main")
        self.commit_in(copy, "client/Assets/game.cs", "changed\n", "on main")
        self.assertEqual(self.ref(self.source, "main"), before)
        self.assertEqual(self.ref(self.source, "refs/heads/copy/work/main"), self.ref(copy, "HEAD"))

    def test_commit_in_the_submodule_of_a_copy_reaches_the_submodule_of_the_project(self):
        copy = self.create()
        framework = copy / "tools/framework"
        self.git(framework, "switch", "-q", "-c", "fix")
        self.commit_in(framework, "code.cs", "fixed\n", "fix")
        self.assertEqual(self.ref(self.source / "tools/framework", "refs/heads/copy/work/fix"), self.ref(framework, "HEAD"))

    def test_changes_the_project_had_at_create_are_not_work_of_the_agent(self):
        (self.source / "client/Assets/game.cs").write_text("someone else edits the main tree\n")
        (self.source / "client/Assets/untracked.cs").write_text("untracked\n")
        self.create()
        entry = self.entry()
        self.assertEqual(entry["pending"], {"commits": [], "changed": 0, "files": []})
        self.assertFalse(self.git(self.source, "for-each-ref", "refs/copies"))

    def test_uncommitted_work_is_saved_without_touching_the_copy(self):
        (self.source / "client/Assets/game.cs").write_text("inherited\n")
        copy = self.create()
        (copy / "client/Assets/scene.unity").write_text("edited scene\n")
        (copy / "client/Assets/new.cs").write_text("new file\n")
        (copy / "client/Library/cache").write_text("ignored\n")
        status = self.git(copy, "status", "--porcelain")

        entry = self.control("copy", "save", "work")
        self.assertEqual(entry["pending"]["changed"], 2)
        self.assertEqual(sorted(entry["pending"]["files"]), ["client/Assets/new.cs", "client/Assets/scene.unity"])
        self.assertEqual(self.git(copy, "status", "--porcelain"), status)
        self.assertEqual(self.git(copy, "diff", "--cached", "--name-only"), "")

        wip = "refs/copies/work/wip"
        self.assertEqual(self.git(self.source, "show", f"{wip}:client/Assets/scene.unity"), "edited scene")
        self.assertEqual(self.git(self.source, "show", f"{wip}:client/Assets/new.cs"), "new file")
        self.assertNotIn("client/Library/cache", self.git(self.source, "ls-tree", "-r", "--name-only", wip))
        changed = self.git(self.source, "diff", "--name-only", f"{wip}^2", wip).split()
        self.assertEqual(sorted(changed), ["client/Assets/new.cs", "client/Assets/scene.unity"])

    def test_uncommitted_work_in_the_submodule_is_saved_in_its_repository(self):
        copy = self.create()
        (copy / "tools/framework/code.cs").write_text("edited\n")
        entry = self.control("copy", "save", "work")
        self.assertEqual(entry["pending"]["files"], ["tools/framework/code.cs"])
        self.assertEqual(self.git(self.source / "tools/framework", "show", "refs/copies/work/wip:code.cs"), "edited")

    def test_a_second_save_without_changes_makes_no_new_snapshot(self):
        copy = self.create()
        (copy / "client/Assets/scene.unity").write_text("edited\n")
        self.control("copy", "save", "work")
        first = self.ref(self.source, "refs/copies/work/wip")
        self.control("copy", "save", "work")
        self.assertEqual(self.ref(self.source, "refs/copies/work/wip"), first)

    def test_a_snapshot_follows_the_work_and_goes_when_it_is_committed(self):
        copy = self.create()
        (copy / "client/Assets/scene.unity").write_text("edited\n")
        self.control("copy", "save", "work")
        self.git(copy, "add", "-A")
        self.git(copy, "commit", "-q", "-m", "scene")
        entry = self.control("copy", "save", "work")
        self.assertEqual(entry["pending"]["changed"], 0)
        self.assertEqual(self.ref(self.source, "refs/copies/work/wip"), "")
        self.assertEqual(len(entry["pending"]["commits"]), 1)

    def test_merged_work_is_not_pending_and_its_branch_goes(self):
        copy = self.create()
        self.git(copy, "switch", "-q", "-c", "feature")
        self.commit_in(copy, "client/Assets/game.cs", "changed\n", "feature")
        self.assertEqual(len(self.entry()["pending"]["commits"]), 1)
        self.git(self.source, "merge", "-q", "copy/work/feature")
        self.assertEqual(self.entry()["pending"]["commits"], [])
        self.assertEqual(self.ref(self.source, "refs/heads/copy/work/feature"), "")

    def test_rebased_work_is_not_pending(self):
        copy = self.create()
        self.git(copy, "switch", "-q", "-c", "feature")
        self.commit_in(copy, "client/Assets/game.cs", "changed\n", "feature")
        self.commit_in(self.source, "client/Assets/scene.unity", "moved on\n", "the project moved on")
        self.git(self.source, "cherry-pick", "copy/work/feature")
        self.assertEqual(self.entry()["pending"]["commits"], [])

    def test_uncommitted_file_the_project_already_committed_is_not_pending(self):
        copy = self.create()
        (copy / "client/Assets/scene.unity").write_text("carried over\n")
        self.assertEqual(self.entry()["pending"]["changed"], 1)
        self.commit_in(self.source, "client/Assets/scene.unity", "carried over\n", "carried by hand")
        self.assertEqual(self.entry()["pending"]["changed"], 0)

    def test_remove_refuses_a_copy_with_work_and_keeps_it(self):
        copy = self.create()
        (copy / "client/Assets/scene.unity").write_text("edited\n")
        result = self.run_control("copy", "remove", "work")
        self.assertEqual(result.returncode, 1, result.stdout)
        self.assertIn("--force", result.stdout)
        self.assertTrue(copy.exists())

    def test_remove_force_keeps_the_saved_work_in_the_project(self):
        copy = self.create()
        (copy / "client/Assets/scene.unity").write_text("edited\n")
        self.git(copy, "switch", "-q", "-c", "feature")
        self.commit_in(copy, "client/Assets/game.cs", "committed\n", "feature")
        (copy / "client/Assets/scene.unity").write_text("edited again\n")
        self.control("copy", "remove", "work", "--force")
        self.assertFalse(copy.exists())
        self.assertEqual(self.git(self.source, "show", "refs/copies/work/wip:client/Assets/scene.unity"), "edited again")
        self.assertEqual(self.git(self.source, "show", "copy/work/feature:client/Assets/game.cs"), "committed")
        removed = self.entry()
        self.assertEqual(removed["state"], "removed")

    def test_drop_deletes_what_a_removed_copy_left(self):
        copy = self.create()
        (copy / "tools/framework/code.cs").write_text("edited\n")
        self.commit_in(copy, "client/Assets/game.cs", "committed\n", "work")
        self.control("copy", "remove", "work", "--force")
        self.control("copy", "drop", "work")
        self.assertFalse(self.git(self.source, "for-each-ref", "refs/copies", "refs/heads/copy"))
        self.assertFalse(self.git(self.source / "tools/framework", "for-each-ref", "refs/copies", "refs/heads/copy"))
        self.assertEqual(self.control("copy", "list"), [])

    def test_create_refuses_the_name_of_a_removed_copy_whose_work_is_saved(self):
        copy = self.create()
        (copy / "client/Assets/scene.unity").write_text("edited\n")
        self.control("copy", "remove", "work", "--force")
        result = self.run_control("copy", "create", "work", "--no-editor")
        self.assertEqual(result.returncode, 1, result.stdout)
        self.assertIn("drop", result.stdout)
        self.assertEqual(self.git(self.source, "show", "refs/copies/work/wip:client/Assets/scene.unity"), "edited")

    def test_drop_refuses_a_copy_that_still_exists(self):
        self.create()
        self.assertEqual(self.run_control("copy", "drop", "work").returncode, 1)

    def test_remove_of_a_clean_copy_needs_no_force_and_leaves_nothing(self):
        copy = self.create()
        self.control("copy", "remove", "work")
        self.assertFalse(copy.exists())
        self.assertFalse(self.git(self.source, "for-each-ref", "refs/copies", "refs/heads/copy"))

    def test_sweep_runs_outside_a_repository_and_saves_every_project(self):
        copy = self.create()
        (copy / "client/Assets/scene.unity").write_text("edited\n")
        self.create("idle")
        result = self.control("copy", "sweep", cwd=self.temp)
        self.assertEqual(self.git(self.source, "show", "refs/copies/work/wip:client/Assets/scene.unity"), "edited")
        self.assertEqual(result["pending"], [f"{self.source.name}/work"])
        status = json.loads((self.temp / "home/.cache/claude-control/copies.json").read_text())
        self.assertEqual(status["pending"], [f"{self.source.name}/work"])
        self.assertEqual(status["errors"], {})

    def test_sweep_reports_a_folder_without_a_marker_and_a_broken_copy_and_goes_on(self):
        copy = self.create()
        (copy / "client/Assets/scene.unity").write_text("edited\n")
        broken = self.create("broken")
        marker = json.loads((broken / ".control-copy.json").read_text())
        marker["source"] = str(self.temp / "no-such-project")
        (broken / ".control-copy.json").write_text(json.dumps(marker))
        unknown = self.temp / "copies" / self.source.name / "by-hand"
        unknown.mkdir()

        result = self.control("copy", "sweep", cwd=self.temp)
        self.assertEqual(result["pending"], [f"{self.source.name}/work"])
        self.assertEqual(result["unknown"], [f"{self.source.name}/by-hand"])
        self.assertEqual(list(result["errors"]), [f"{self.source.name}/broken"])

    def test_sweep_gives_an_old_copy_its_hook_and_keeps_its_work(self):
        copy = self.create()
        hook = Path(self.git(copy, "rev-parse", "--absolute-git-dir")) / "hooks" / "post-commit"
        hook.unlink()
        self.git(copy, "update-ref", "-d", "refs/control/base")
        (copy / "client/Assets/scene.unity").write_text("edited\n")

        result = self.control("copy", "sweep", cwd=self.temp)
        self.assertEqual(result["pending"], [f"{self.source.name}/work"])
        self.commit_in(copy, "client/Assets/game.cs", "committed\n", "after the sweep")
        self.assertEqual(self.ref(self.source, "refs/heads/copy/work/main"), self.ref(copy, "HEAD"))

    def test_hook_of_the_project_runs_too(self):
        hook = self.source / ".git/hooks/post-commit"
        hook.write_text('#!/bin/sh\necho ran > "$(git rev-parse --absolute-git-dir)/own-hook-ran"\n')
        hook.chmod(0o755)
        copy = self.create()
        self.commit_in(copy, "client/Assets/game.cs", "committed\n", "work")
        self.assertTrue((copy / ".git/own-hook-ran").exists())
        self.assertEqual(self.ref(self.source, "refs/heads/copy/work/main"), self.ref(copy, "HEAD"))

    def test_hooks_path_of_the_project_is_not_written_through(self):
        self.git(self.source, "config", "core.hooksPath", str(self.source / ".git/hooks"))
        copy = self.create()
        self.assertFalse((self.source / ".git/hooks/post-commit").exists())
        self.commit_in(copy, "client/Assets/game.cs", "committed\n", "work")
        self.assertEqual(self.ref(self.source, "refs/heads/copy/work/main"), self.ref(copy, "HEAD"))

    def test_hooks_of_the_project_in_its_tree_run_and_stay_untouched(self):
        hooks = self.source / "tools/githooks"
        hooks.mkdir(parents=True)
        for name in ("pre-commit", "post-commit"):
            (hooks / name).write_text(f'#!/bin/sh\necho ran > "$(git rev-parse --absolute-git-dir)/{name}-ran"\n')
            (hooks / name).chmod(0o755)
        self.git(self.source, "add", "-A")
        self.git(self.source, "commit", "-q", "-m", "hooks")
        self.git(self.source, "config", "core.hooksPath", "tools/githooks")
        copy = self.create()
        self.commit_in(copy, "client/Assets/game.cs", "committed\n", "work")
        self.assertTrue((copy / ".git/pre-commit-ran").exists())
        self.assertTrue((copy / ".git/post-commit-ran").exists())
        self.assertEqual(self.ref(self.source, "refs/heads/copy/work/main"), self.ref(copy, "HEAD"))
        self.assertEqual(self.git(copy, "status", "--porcelain"), "")
        self.assertEqual(self.git(self.source, "config", "core.hooksPath"), "tools/githooks")
        self.assertNotIn("control copy", (hooks / "post-commit").read_text())
        self.assertEqual(self.entry()["pending"]["files"], [])
        self.control("copy", "remove", "work", "--force")
        self.assertFalse(copy.exists())

    def test_large_file_objects_reach_the_project_with_the_snapshot(self):
        copy = self.create()
        objects = copy / ".git/lfs/objects/aa/bb"
        objects.mkdir(parents=True)
        (objects / "aabb").write_text("large file\n")
        (copy / "client/Assets/scene.unity").write_text("edited\n")
        self.control("copy", "save", "work")
        self.assertEqual((self.source / ".git/lfs/objects/aa/bb/aabb").read_text(), "large file\n")


if __name__ == "__main__":
    unittest.main()
