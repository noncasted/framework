# The script as a real process against the fakes, from a directory the test chooses.
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

TESTS = Path(__file__).resolve().parent
SCRIPTS = TESTS.parent
SCRIPT = SCRIPTS / "prod"
sys.path.insert(0, str(SCRIPTS))
sys.path.insert(0, str(TESTS))

from fake_backend import FIXTURES  # noqa: E402

CODES = {"ok": 0, "service": 1, "usage": 2, "auth": 3, "not_found": 4, "findings": 5}


class Base(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.temp = Path(temp.name)
        self.registry_data = json.loads((FIXTURES / "registry.json").read_text())
        self.registry = self.temp / "registry.json"
        self.write_registry()
        # Outside any repository: no project of the current directory.
        self.cwd = self.temp / "outside"
        self.cwd.mkdir()

    def write_registry(self):
        self.registry.write_text(json.dumps(self.registry_data))

    def run_env(self):
        return {"PATH": os.environ["PATH"], "HOME": str(self.temp), "GIT_CEILING_DIRECTORIES": str(self.temp)}

    def run_cli(self, *arguments, cwd=None, env=None, secret=None):
        result = subprocess.run([sys.executable, str(SCRIPT), *arguments], capture_output=True, text=True, timeout=60,
                                cwd=cwd or self.cwd, env={**self.run_env(), **(env or {})})
        self.assertNotIn("Traceback", result.stderr)

        if secret:
            self.assertNotIn(secret, result.stdout + result.stderr)

        return result

    def assert_code(self, result, name):
        self.assertEqual(CODES[name], result.returncode, result.stdout + result.stderr)

    def git_repo(self, name, remote=None):
        folder = self.temp / name
        folder.mkdir()
        subprocess.run(["git", "init", "-q", str(folder)], check=True)

        if remote:
            subprocess.run(["git", "-C", str(folder), "remote", "add", "origin", remote], check=True)

        return folder
