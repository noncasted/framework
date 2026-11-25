# registry show / verify and the reading of the registry:
#   python3 -m unittest discover -s tools/scripts/deploy/tests
import json
import subprocess
from unittest import mock

from support import Base  # first: it puts the scripts on the path

import registry  # noqa: E402
from common import SERVICE, Failure  # noqa: E402
from fake_backend import FIXTURES, FakePanel  # noqa: E402

TOKENS = {"ovh": "ovh-Secret-Token-111", "firstvds": "kz-Secret-Token-222"}


class Show(Base):
    def test_a_project_in_full(self):
        result = self.run_cli("registry", "show", "pixel-swarm", "--registry", str(self.registry))

        self.assert_code(result, "ok")
        self.assertIn("pixel-swarm: server ovh (OVH, Warsaw, 51.68.143.178), mode services, namespace pixel-swarm", result.stdout)
        self.assertIn("ssh ovh (user ubuntu, docker: sudo -n docker), MCP coolify-ovh, panel https://coolify.ncstd.xyz", result.stdout)
        self.assertIn("coolify project zqfwglgmpyhdwoii2oszwoa5 'pixel-swarm' / production", result.stdout)
        self.assertIn("  backend: application c4b3aasq8hoikkbzgloa5ccd", result.stdout)
        self.assertIn("  postgres: database vjgu3p4ptf5bxxs7di7f8sw1 (postgres)", result.stdout)
        self.assertIn("client https://pixel-swarm.xyz", result.stdout)
        self.assertIn("stack application oukejx1fbca7wtmycipf9rjt", result.stdout)

    def test_a_list_of_client_domains(self):
        result = self.run_cli("registry", "show", "post-radio", "--registry", str(self.registry))

        self.assertIn("client https://post-radio.ru https://post-radio.com", result.stdout)
        self.assertIn("note: Not on the framework", result.stdout)

    def test_a_project_without_deploy(self):
        result = self.run_cli("registry", "show", "neon-dodge", "--registry", str(self.registry))

        self.assert_code(result, "ok")
        self.assertEqual("neon-dodge: not deployed. No Coolify project on either panel.", result.stdout.strip())

    def test_an_unknown_project_lists_the_known(self):
        result = self.run_cli("registry", "show", "nope", "--registry", str(self.registry))

        self.assert_code(result, "usage")
        self.assertIn("no project nope in the registry; known: mines-leader, pixel-swarm", result.stderr)

    def test_outside_a_game_every_project(self):
        result = self.run_cli("registry", "show", "--registry", str(self.registry))

        lines = result.stdout.splitlines()
        self.assertEqual(["project", "server", "mode", "namespace", "coolify", "project", "client"], lines[0].split())
        self.assertEqual(1 + len(self.registry_data["projects"]), len(lines))
        self.assertIn("not deployed", next(line for line in lines if line.startswith("neon-dodge")))

    def test_the_project_of_the_repository_by_its_origin(self):
        # A worktree of a game is named after the task, not the game: the origin names it.
        folder = self.git_repo("some_task_worktree", "git@github.com:noncasted/storytale-workshop.git")

        result = self.run_cli("registry", "show", "--registry", str(self.registry), cwd=folder)

        self.assertTrue(result.stdout.startswith("storytale: server firstvds"), result.stdout)

    def test_the_project_of_the_repository_by_its_name(self):
        folder = self.git_repo("mines-leader")

        result = self.run_cli("registry", "show", "--registry", str(self.registry), cwd=folder / "")

        self.assertTrue(result.stdout.startswith("mines-leader: server ovh"), result.stdout)


class Load(Base):
    def framework(self, main_registry):
        # A framework whose origin/main holds one registry and whose working tree another.
        self.origin = self.temp / "origin.git"
        work = self.temp / "framework"
        subprocess.run(["git", "init", "-q", "--bare", "-b", "main", str(self.origin)], check=True)
        subprocess.run(["git", "clone", "-q", str(self.origin), str(work)], check=True, capture_output=True)
        self.push_registry(work, main_registry)
        (work / "deploy" / "registry.json").write_text("{}")

        return work

    def push_registry(self, clone, data):
        (clone / "deploy").mkdir(exist_ok=True)
        (clone / "deploy" / "registry.json").write_text(json.dumps(data))
        git = ["git", "-C", str(clone), "-c", "user.name=t", "-c", "user.email=t@t"]
        subprocess.run([*git, "add", "."], check=True)
        subprocess.run([*git, "commit", "-q", "-m", "registry"], check=True)
        subprocess.run([*git, "push", "-q", "origin", "HEAD:main"], check=True, capture_output=True)

    def another_clone(self, name="another"):
        clone = self.temp / name
        subprocess.run(["git", "clone", "-q", str(self.origin), str(clone)], check=True, capture_output=True)

        return clone

    def test_the_registry_of_origin_main_not_of_the_tree(self):
        work = self.framework({"servers": {}, "projects": {"only-on-main": {}}})

        with mock.patch.object(registry, "FRAMEWORK", work):
            loaded = registry.load()

        self.assertEqual(["only-on-main"], list(loaded["projects"]))

    def test_a_recent_fetch_is_not_repeated(self):
        work = self.framework({"servers": {}, "projects": {"first": {}}})

        with mock.patch.object(registry, "FRAMEWORK", work):
            registry.load()
            self.push_registry(self.another_clone(), {"servers": {}, "projects": {"second": {}}})
            cached = registry.load()

            with mock.patch.object(registry, "FETCH_SECONDS", 0):
                stale = registry.load()

            self.push_registry(self.another_clone("third"), {"servers": {}, "projects": {"third": {}}})
            forced = registry.load(refresh=True)

        self.assertEqual(["first"], list(cached["projects"]))
        self.assertEqual(["second"], list(stale["projects"]))
        self.assertEqual(["third"], list(forced["projects"]))

    def test_a_fetch_of_someone_else_does_not_count(self):
        # FETCH_HEAD is fresh after any fetch, of another branch too; only the stamp of this script says main was fetched.
        work = self.framework({"servers": {}, "projects": {"first": {}}})
        subprocess.run(["git", "-C", str(work), "fetch", "-q", "origin"], check=True)
        self.push_registry(self.another_clone(), {"servers": {}, "projects": {"second": {}}})

        with mock.patch.object(registry, "FRAMEWORK", work):
            loaded = registry.load()

        self.assertEqual(["second"], list(loaded["projects"]))

    def test_a_missing_registry_file(self):
        result = self.run_cli("registry", "show", "--registry", str(self.temp / "absent.json"))

        self.assert_code(result, "usage")
        self.assertIn(f"cannot read the registry {self.temp / 'absent.json'}: No such file or directory", result.stderr)

    def test_a_failed_fetch_is_an_error(self):
        work = self.framework({"servers": {}, "projects": {}})
        subprocess.run(["git", "-C", str(work), "remote", "set-url", "origin", str(self.temp / "gone.git")], check=True)

        with mock.patch.object(registry, "FRAMEWORK", work), self.assertRaises(Failure) as raised:
            registry.load()

        self.assertEqual(SERVICE, raised.exception.code)
        self.assertIn("fetch -q origin main failed", raised.exception.message)


class Verify(Base):
    def setUp(self):
        super().setUp()
        recorded = json.loads((FIXTURES / "coolify.json").read_text())
        self.panels = {}

        for key in ("ovh", "firstvds"):
            panel = FakePanel(TOKENS[key], recorded[f"coolify-{key}"])
            self.addCleanup(panel.close)
            self.panels[key] = panel
            self.registry_data["servers"][key]["panel"] = panel.url

        self.write_registry()
        self.config = self.temp / "coolify.json"
        self.write_config()

    def write_config(self, **overrides):
        servers = {f"coolify-{key}": {"type": "http", "url": f"{panel.url}/mcp", "headers": {"Authorization": f"Bearer {TOKENS[key]}"}}
                   for key, panel in self.panels.items()}
        servers.update(overrides)
        self.config.write_text(json.dumps({"mcpServers": servers}))

    def verify(self):
        result = self.run_cli("registry", "verify", "--registry", str(self.registry), "--coolify-config", str(self.config))

        for token in TOKENS.values():
            self.assertNotIn(token, result.stdout + result.stderr)

        return result

    def test_the_recorded_panels_match(self):
        result = self.verify()

        self.assert_code(result, "ok")
        self.assertEqual("registry matches Coolify: ovh 13 uuids, firstvds 15 uuids", result.stdout.strip())
        self.assertEqual([f"Bearer {TOKENS['ovh']}"], list({request["authorization"] for request in self.panels["ovh"].requests}))

    def test_every_difference(self):
        ovh = self.panels["ovh"].lists
        ovh["services"] = [item for item in ovh["services"] if item["uuid"] != "kxbnp6f7sld3gppbj6ik7as0"]
        ovh["projects"].append({"uuid": "newprojectuuid000000000a", "name": "fresh-game"})
        next(item for item in ovh["projects"] if item["name"] == "minesleader")["name"] = "mines"
        moved = next(item for item in ovh["databases"] if item["uuid"] == "vp7mmjzon4ko5gxhx0ntzt2m")
        ovh["databases"].remove(moved)
        self.panels["firstvds"].lists["databases"].append(moved)
        ovh["applications"].append({"uuid": "rp57atznu171qtglsuupajin", "name": "client"})
        ovh["services"] = [item for item in ovh["services"] if item["uuid"] != "rp57atznu171qtglsuupajin"]

        result = self.verify()

        self.assert_code(result, "findings")
        self.assertEqual([
            "6 differences (ovh 13 uuids, firstvds 15 uuids checked):",
            "  ovh: project xyrabeo4mc5g8y97v9oyo30p of mines-leader is 'mines' in Coolify, 'minesleader' in the registry",
            "  ovh: rp57atznu171qtglsuupajin of mines-leader (client) is application in Coolify, service in the registry",
            "  ovh: service kxbnp6f7sld3gppbj6ik7as0 of pixel-swarm (client) is not in Coolify",
            "  ovh: database vp7mmjzon4ko5gxhx0ntzt2m of gamejam-autobattler (postgres) is not in Coolify, it is on firstvds",
            "  ovh: project newprojectuuid000000000a 'fresh-game' is in Coolify, not in the registry",
            "  firstvds: database vp7mmjzon4ko5gxhx0ntzt2m 'postgres' is in Coolify, not in the registry",
        ], result.stdout.splitlines())

    def test_a_panel_that_moved(self):
        self.registry_data["servers"]["ovh"]["panel"] = "https://coolify.example"
        self.write_registry()

        result = self.verify()

        self.assert_code(result, "findings")
        self.assertIn(f"ovh: the panel is https://coolify.example in the registry, {self.panels['ovh'].url} in the MCP config of coolify-ovh",
                      result.stdout)

    def test_a_refused_token(self):
        self.write_config(**{"coolify-ovh": {"url": f"{self.panels['ovh'].url}/mcp", "headers": {"Authorization": "Bearer wrong-Token-xyz"}}})

        result = self.verify()

        self.assert_code(result, "auth")
        self.assertIn(f"coolify-ovh: GET {self.panels['ovh'].url}/api/v1/projects -> 401", result.stderr)
        self.assertNotIn("wrong-Token-xyz", result.stdout + result.stderr)

    def test_a_server_error(self):
        self.panels["firstvds"].status["databases"] = 500

        result = self.verify()

        self.assert_code(result, "service")
        self.assertIn("coolify-firstvds: GET", result.stderr)
        self.assertIn("databases -> 500", result.stderr)

    def test_a_redirect_is_not_followed(self):
        elsewhere = FakePanel("other", {})
        self.addCleanup(elsewhere.close)
        self.panels["ovh"].redirect = f"{elsewhere.url}/api/v1/projects"

        result = self.verify()

        self.assert_code(result, "service")
        self.assertIn("-> 302", result.stderr)
        self.assertEqual([], elsewhere.requests)

    def test_a_panel_missing_in_the_config(self):
        self.config.write_text(json.dumps({"mcpServers": {"coolify-ovh": json.loads(self.config.read_text())["mcpServers"]["coolify-ovh"]}}))

        result = self.verify()

        self.assert_code(result, "usage")
        self.assertIn("no MCP server coolify-firstvds of firstvds", result.stderr)

    def test_an_unreachable_panel(self):
        self.panels["ovh"].close()

        result = self.verify()

        self.assert_code(result, "service")
        self.assertIn("coolify-ovh:", result.stderr)
        self.assertIn("unreachable", result.stderr)
