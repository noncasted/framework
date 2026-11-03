# The script against a local fake of the reading API, as a real process:
#   python3 -m unittest discover -s .claude/skills/client-telemetry/tests
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SKILL = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(SKILL))
sys.path.insert(0, str(Path(__file__).resolve().parent))

from fake_backend import FakeBackend, sample, session, startup_info, startup_span  # noqa: E402

SCRIPT = SKILL / "telemetry"
TOKEN = "s3cr3t-Token-value-123"
OLD = "aaaaaaaa-0000-4000-8000-000000000001"
NEW = "bbbbbbbb-0000-4000-8000-000000000002"
NEW2 = "cccccccc-0000-4000-8000-000000000003"
ABSENT = "dddddddd-0000-4000-8000-000000000004"

CONFIG, API, READY, BAD_AUTH, MISSING, TIMED_OUT = 2, 1, 0, 3, 4, 5


class Base(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.temp = Path(temp.name)
        self.backend = FakeBackend(TOKEN)
        self.addCleanup(self.backend.close)
        self.env_file = self.temp / "deploy.env"
        self.env_file.write_text(f"OTHER_SECRET=other-value\nCONSOLE_TOKEN={TOKEN}\n")

    def run_bare(self, *arguments, env=None):
        base = {"PATH": os.environ["PATH"], "HOME": str(self.temp)}
        result = subprocess.run([str(SCRIPT), *arguments], capture_output=True, text=True, timeout=60, env={**base, **(env or {})})
        self.assertNotIn(TOKEN, result.stdout + result.stderr)
        self.assertNotIn("other-value", result.stdout + result.stderr)
        self.assertNotIn("Traceback", result.stderr)

        return result

    def run_cli(self, *arguments):
        return self.run_bare(*arguments, "--url", self.backend.url, "--env-file", str(self.env_file))

    def monitor(self, *arguments):
        return self.run_cli("monitor", "--timeout", "5", "--poll", "0.05", "--seconds", "1", *arguments)

    def appear(self, *sessions_to_add, at=2):
        def on_list(calls):
            if calls == at:
                self.backend.sessions[:0] = sessions_to_add

        self.backend.on_list = on_list


class ConfigTests(Base):
    def write_section(self, text):
        folder = self.temp / "project" / ".claude"
        folder.mkdir(parents=True)
        (folder / "project-skills.md").write_text(text)

        return {"CLAUDE_PROJECT_DIR": str(self.temp / "project")}

    def section(self, url=None, env_file=None):
        return ("# Skills of the game\n\n## deploy-client\n\n- url: `https://wrong.invalid`\n\n"
                "## client-telemetry\n\nProse of the game.\n\n"
                f"- url: `{url or self.backend.url}`\n- env-file: `{env_file or self.env_file}`\n\n"
                "## visualize\n\n- env-file: `/wrong`\n")

    def test_section_gives_the_address_and_the_file(self):
        result = self.run_bare("sessions", env=self.write_section(self.section()))

        self.assertEqual(READY, result.returncode, result.stderr)
        self.assertEqual(f"Bearer {TOKEN}", self.backend.authorizations[0])

    def test_section_expands_the_home_of_the_file(self):
        result = self.run_bare("sessions", env=self.write_section(self.section(env_file="~/deploy.env")))

        self.assertEqual(READY, result.returncode, result.stderr)

    def test_arguments_win_over_the_section(self):
        env = self.write_section(self.section(url="http://127.0.0.1:1", env_file="/nonexistent"))
        result = self.run_bare("sessions", "--url", self.backend.url, "--env-file", str(self.env_file), env=env)

        self.assertEqual(READY, result.returncode, result.stderr)

    def test_no_project_and_no_arguments(self):
        result = self.run_bare("sessions")

        self.assertEqual(CONFIG, result.returncode)
        self.assertIn("CLAUDE_PROJECT_DIR", result.stderr)
        self.assertIn("--url", result.stderr)

    def test_no_section(self):
        result = self.run_bare("sessions", env=self.write_section("## deploy-client\n\n- url: `https://x.invalid`\n"))

        self.assertEqual(CONFIG, result.returncode)
        self.assertIn("## client-telemetry", result.stderr)

    def test_section_without_a_key(self):
        result = self.run_bare("sessions", env=self.write_section(f"## client-telemetry\n\n- url: `{self.backend.url}`\n"))

        self.assertEqual(CONFIG, result.returncode)
        self.assertIn("env-file", result.stderr)

    def test_no_env_file(self):
        result = self.run_bare("sessions", "--url", self.backend.url, "--env-file", str(self.temp / "absent.env"))

        self.assertEqual(CONFIG, result.returncode)
        self.assertIn("absent.env", result.stderr)

    def test_no_token_in_the_file(self):
        self.env_file.write_text("OTHER_SECRET=other-value\n# CONSOLE_TOKEN=commented\n")
        result = self.run_cli("sessions")

        self.assertEqual(CONFIG, result.returncode)
        self.assertIn("CONSOLE_TOKEN", result.stderr)
        self.assertNotIn("commented", result.stderr)
        self.assertEqual([], self.backend.requests)

    def test_token_forms(self):
        for line in (f"CONSOLE_TOKEN={TOKEN}", f"export CONSOLE_TOKEN={TOKEN}", f"  CONSOLE_TOKEN=\"{TOKEN}\"",
                     f"CONSOLE_TOKEN='{TOKEN}'", f"CONSOLE_TOKEN={TOKEN}  # a comment", f"CONSOLE_TOKEN={TOKEN}\r"):
            self.env_file.write_text(f"A=1\n{line}\nB=2\n")
            result = self.run_cli("sessions")

            self.assertEqual(READY, result.returncode, f"{line}: {result.stderr}")
            self.assertEqual(f"Bearer {TOKEN}", self.backend.authorizations[-1])

    def test_file_is_data_never_run(self):
        mark = self.temp / "executed"
        self.env_file.write_text(f"touch {mark}\nCONSOLE_TOKEN=\"$(touch {mark})\"\n")
        result = self.run_cli("sessions")

        self.assertFalse(mark.exists())
        self.assertEqual(CONFIG, result.returncode)
        self.assertNotIn("touch", result.stderr)

    def test_expansion_is_a_part_of_the_value(self):
        self.backend.token = "$HOME"
        self.env_file.write_text("CONSOLE_TOKEN=$HOME\n")

        self.assertEqual(READY, self.run_cli("sessions").returncode)

    def test_token_of_wrong_characters_is_not_printed(self):
        self.env_file.write_text("CONSOLE_TOKEN=токен-значение\n")
        result = self.run_cli("sessions")

        self.assertEqual(CONFIG, result.returncode)
        self.assertNotIn("значение", result.stderr)
        self.assertEqual([], self.backend.requests)

    def test_binary_file_is_not_printed(self):
        self.env_file.write_bytes(b"CONSOLE_TOKEN=\xff\xfeabc\n")
        result = self.run_cli("sessions")

        self.assertEqual(CONFIG, result.returncode)
        self.assertNotIn("abc", result.stderr)

    def test_token_is_no_argument(self):
        result = self.run_cli("sessions", "--token", "x")

        self.assertEqual(CONFIG, result.returncode)
        self.assertEqual([], self.backend.requests)

    def test_wrong_id(self):
        self.assertEqual(CONFIG, self.run_cli("summary", "not-a-guid").returncode)
        self.assertEqual([], self.backend.requests)

    def test_plain_http_to_another_host_is_refused(self):
        result = self.run_bare("sessions", "--url", "http://meta.example.invalid", "--env-file", str(self.env_file))

        self.assertEqual(CONFIG, result.returncode)
        self.assertIn("https", result.stderr)


class TokenTests(Base):
    def test_only_in_the_header(self):
        self.backend.sessions = [session(OLD, status="ended")]
        self.backend.samples[OLD] = [sample(1, 1000, {"frame.fps": 60})]

        for arguments in (("sessions",), ("summary", OLD), ("errors",), ("errors", OLD), ("watch", OLD, "--seconds", "1")):
            self.assertEqual(READY, self.run_cli(*arguments).returncode, arguments)

        self.assertTrue(self.backend.requests)
        self.assertFalse([path for path in self.backend.requests if TOKEN in path])
        self.assertEqual({f"Bearer {TOKEN}"}, set(self.backend.authorizations))

    def test_echoed_by_the_backend_is_cut_from_the_output(self):
        self.backend.forced = (400, f"bad header Bearer {TOKEN}", {})
        self.assertEqual(API, self.run_cli("sessions").returncode)

        self.backend.forced = None
        self.backend.sessions = [session(OLD, status="ended", platform=TOKEN)]
        self.assertEqual(READY, self.run_cli("sessions").returncode)

    def test_short_token_leaves_the_words_that_hold_it(self):
        self.backend.token = "dev"
        self.env_file.write_text("CONSOLE_TOKEN=dev\n")
        self.backend.sessions = [session(OLD, status="ended")]
        result = self.run_cli("summary", OLD)

        self.assertIn("device platform", result.stdout)

        self.backend.forced = (400, "bad header Bearer dev.", {})
        result = self.run_cli("sessions")

        self.assertNotIn("Bearer dev", result.stderr)

    def test_redirect_is_not_followed(self):
        other = FakeBackend(TOKEN)
        self.addCleanup(other.close)
        self.backend.forced = (302, "", {"Location": f"{other.url}/api/client-telemetry/sessions"})
        result = self.run_cli("sessions")

        self.assertEqual(API, result.returncode)
        self.assertIn("redirect", result.stderr)
        self.assertEqual([], other.requests)


class ExitCodeTests(Base):
    def test_codes(self):
        for status, code, word in ((401, BAD_AUTH, "401"), (403, BAD_AUTH, "403"), (503, API, "503"), (500, API, "500"), (400, API, "400")):
            self.backend.forced = (status, "", {})
            result = self.run_cli("sessions")

            self.assertEqual(code, result.returncode, status)
            self.assertIn(word, result.stderr)

    def test_wrong_token(self):
        self.backend.token = "another"
        result = self.run_cli("sessions")

        self.assertEqual(BAD_AUTH, result.returncode)
        self.assertIn("CONSOLE_TOKEN", result.stderr)

    def test_unknown_session(self):
        for arguments in (("summary", ABSENT), ("errors", ABSENT), ("watch", ABSENT, "--seconds", "1")):
            result = self.run_cli(*arguments)

            self.assertEqual(MISSING, result.returncode, arguments)
            self.assertIn(ABSENT, result.stderr)

    def test_host_is_down(self):
        result = self.run_bare("sessions", "--url", "http://127.0.0.1:1", "--env-file", str(self.env_file))

        self.assertEqual(API, result.returncode)
        self.assertIn("127.0.0.1:1", result.stderr)

    def test_not_json(self):
        self.backend.forced = (200, "<html>a page of a proxy</html>", {})
        result = self.run_cli("sessions")

        self.assertEqual(API, result.returncode)
        self.assertIn("JSON", result.stderr)


class MonitorTests(Base):
    def test_finds_a_new_one(self):
        self.backend.sessions = [session(OLD)]
        self.appear(session(NEW, status="ended"))
        result = self.monitor()

        self.assertEqual(READY, result.returncode, result.stderr)
        self.assertIn(f"new {NEW}", result.stdout)
        self.assertNotIn(f"new {OLD}", result.stdout)

    def test_new_is_by_id_not_by_the_clock(self):
        self.backend.sessions = [session(OLD, status="ended", started="2026-11-03T12:00:00Z")]
        self.appear(session(NEW, status="ended", started="2020-01-01T00:00:00Z"))
        result = self.monitor()

        self.assertIn(f"new {NEW}", result.stdout)

    def test_live_of_the_baseline_and_a_reopened_one_are_not_new(self):
        self.backend.sessions = [session(OLD), session(NEW2, status="lost")]

        def on_list(calls):
            if calls == 2:
                self.backend.find(NEW2)["status"] = "live"

        self.backend.on_list = on_list
        result = self.run_cli("monitor", "--timeout", "0.3", "--poll", "0.05")

        self.assertEqual(TIMED_OUT, result.returncode)

    def test_baseline_reads_every_page(self):
        many = [f"eeeeeeee-0000-4000-8000-{index:012d}" for index in range(2500)]
        self.backend.sessions = [session(item, status="ended") for item in many]
        result = self.run_cli("monitor", "--timeout", "0.3", "--poll", "0.05")

        self.assertEqual(TIMED_OUT, result.returncode)
        self.assertIn("2500", result.stderr)

    def test_timeout_points_to_the_flag(self):
        result = self.run_cli("monitor", "--timeout", "0.2", "--poll", "0.05")

        self.assertEqual(TIMED_OUT, result.returncode)
        self.assertIn("Features", result.stderr)

    def test_several_new_the_live_one_is_watched_the_others_are_listed(self):
        self.backend.sessions = [session(OLD, status="ended")]
        self.appear(session(NEW2, status="ended"), session(NEW))
        self.backend.on_samples = lambda found: found.update(status="ended")
        result = self.monitor()

        self.assertEqual(READY, result.returncode, result.stderr)
        self.assertIn(f"new {NEW}", result.stdout)
        self.assertIn(f"other_new {NEW2}", result.stdout)

    def test_platform_goes_to_the_backend(self):
        self.backend.sessions = [session(OLD, status="ended")]
        self.appear(session(NEW2, status="ended", platform="desktop"), session(NEW, status="ended"))
        result = self.monitor("--platform", "webgl")

        self.assertIn(f"new {NEW}", result.stdout)
        self.assertNotIn(NEW2, result.stdout)
        self.assertTrue(all("platform=webgl" in path for path in self.backend.requests if path.split("?")[0].endswith("/sessions")))

    def test_os_is_filtered_here_and_counted_in_the_timeout(self):
        self.appear(session(NEW2, status="ended", os_name="Windows"))
        result = self.run_cli("monitor", "--timeout", "0.3", "--poll", "0.05", "--os", "Android")

        self.assertEqual(TIMED_OUT, result.returncode)
        self.assertIn("1 new", result.stderr)

    def test_api_failure_while_waiting(self):
        def on_list(calls):
            if calls == 2:
                self.backend.forced = (503, "", {})

        self.backend.on_list = on_list
        self.assertEqual(API, self.monitor().returncode)


class TagTests(Base):
    def list_requests(self):
        return [path for path in self.backend.requests if path.split("?")[0].endswith("/sessions")]

    def test_tag_is_asked_of_the_backend_on_every_list(self):
        self.backend.sessions = [session(OLD, status="ended", tags=["run"])]
        self.appear(session(NEW, status="ended", tags=["run"]))
        result = self.monitor("--tag", "run")

        self.assertEqual(READY, result.returncode, result.stderr)
        self.assertIn(f"new {NEW}", result.stdout)
        self.assertGreaterEqual(len(self.list_requests()), 2)
        self.assertTrue(all("tag=run" in path for path in self.list_requests()), self.list_requests())

    def test_baseline_holds_only_the_given_tag(self):
        self.backend.sessions = [session(OLD, tags=["menu"]), session(NEW2, status="ended", tags=["run"])]
        self.appear(session(NEW, status="ended", tags=["run"]))
        result = self.monitor("--tag", "run")

        self.assertIn("baseline=1", result.stdout)
        self.assertIn(f"new {NEW}", result.stdout)
        self.assertNotIn(OLD, result.stdout)

    def test_new_session_of_another_tag_is_not_taken_and_the_timeout_names_the_tag(self):
        self.backend.sessions = [session(OLD, status="ended", tags=["menu"])]
        self.appear(session(NEW, tags=["menu"]))
        result = self.run_cli("monitor", "--timeout", "0.3", "--poll", "0.05", "--tag", "run")

        self.assertEqual(TIMED_OUT, result.returncode)
        self.assertIn("--tag run", result.stderr)
        self.assertNotIn(NEW, result.stdout)

    def test_several_tags_mean_all_of_them(self):
        self.appear(session(NEW2, status="ended", tags=["run"]), session(NEW, status="ended", tags=["run", "ranked"]))
        result = self.monitor("--tag", "run", "--tag", "ranked")

        self.assertIn(f"new {NEW}", result.stdout)
        self.assertNotIn(NEW2, result.stdout)
        self.assertTrue(all("tag=run&tag=ranked" in path for path in self.list_requests()), self.list_requests())

    def test_any_value_the_game_gives_is_taken_exactly(self):
        self.appear(session(NEW2, status="ended", tags=["boss"]), session(NEW, status="ended", tags=["boss fight"]))
        result = self.monitor("--tag", "boss fight")

        self.assertEqual(READY, result.returncode, result.stderr)
        self.assertIn(f"new {NEW}", result.stdout)
        self.assertNotIn(NEW2, result.stdout)

    def test_without_a_tag_nothing_is_asked(self):
        self.appear(session(NEW, status="ended", tags=["menu"]))
        self.monitor()

        self.assertFalse([path for path in self.list_requests() if "tag=" in path])

    def test_new_lines_show_the_tags(self):
        self.backend.sessions = [session(OLD, status="ended")]
        self.appear(session(NEW2, status="ended", tags=["menu"]), session(NEW, tags=["run", "ranked"]))
        self.backend.on_samples = lambda found: found.update(status="ended")
        result = self.monitor()

        self.assertIn("tags=run,ranked", next(line for line in result.stdout.splitlines() if line.startswith("new ")))
        self.assertIn("tags=menu", next(line for line in result.stdout.splitlines() if line.startswith("other_new ")))

    def test_blank_tag_is_refused(self):
        for command in ("sessions", "monitor"):
            result = self.run_cli(command, "--tag", " ")

            self.assertEqual(CONFIG, result.returncode)

        self.assertEqual([], self.backend.requests)

    def test_list_filters_and_shows(self):
        self.backend.sessions = [session(OLD, status="ended", tags=["menu"]), session(NEW, status="ended", tags=["run"]), session(NEW2, status="ended")]
        result = self.run_cli("sessions", "--tag", "run")

        self.assertEqual(READY, result.returncode, result.stderr)
        self.assertIn("sessions=1", result.stdout)
        self.assertIn(NEW, result.stdout)
        self.assertIn("tags=run", result.stdout)
        self.assertNotIn(OLD, result.stdout)
        self.assertTrue(all("tag=run" in path for path in self.list_requests()))

    def test_list_marks_a_session_without_tags(self):
        self.backend.sessions = [session(OLD, status="ended")]

        self.assertIn("tags=-", self.run_cli("sessions").stdout)

    def test_report_shows_the_tags(self):
        self.backend.sessions = [session(OLD, status="ended", tags=["menu", "ranked"]), session(NEW, status="ended")]

        self.assertIn("tags=menu,ranked", self.run_cli("summary", OLD).stdout.splitlines()[0])
        self.assertIn("tags=-", self.run_cli("summary", NEW).stdout.splitlines()[0])

    def test_watch_shows_the_tags_also_when_nothing_came(self):
        self.backend.sessions = [session(OLD, tags=["run"])]
        result = self.run_cli("watch", OLD, "--seconds", "1")

        self.assertIn("no samples", result.stdout)
        self.assertIn("tags=run", result.stdout)


class WatchTests(Base):
    def setUp(self):
        super().setUp()
        self.backend.sessions = [session(OLD)]

    def test_ends_by_seconds(self):
        result = self.run_cli("watch", OLD, "--seconds", "1")

        self.assertEqual(READY, result.returncode, result.stderr)
        self.assertIn("end reason=seconds", result.stdout)

    def test_ends_by_close(self):
        for status in ("ended", "lost"):
            self.backend.on_samples = lambda found, value=status: found.update(status=value)
            self.backend.find(OLD)["status"] = "live"
            result = self.run_cli("watch", OLD, "--seconds", "30")

            self.assertEqual(READY, result.returncode, result.stderr)
            self.assertIn(f"end reason={status}", result.stdout)

    def test_lost_names_the_socket_and_the_gateway_and_no_return_is_promised(self):
        self.backend.on_samples = lambda found: found.update(status="lost")
        result = self.run_cli("watch", OLD, "--seconds", "30")

        self.assertIn("socket", result.stdout)
        self.assertIn("gateway", result.stdout)
        self.assertNotIn("comes back", result.stdout)

    def test_report_covers_only_the_watched_rows(self):
        self.backend.find(OLD)["ackSeq"] = 1
        rows = self.backend.samples[OLD] = [sample(1, 1000, {"frame.fps": 11})]

        def on_samples(found):
            if len(rows) == 1:
                rows.append(sample(2, 2000, {"frame.fps": 22}))
            found["status"] = "ended"

        self.backend.on_samples = on_samples
        result = self.run_cli("watch", OLD, "--seconds", "30")

        self.assertIn("22", result.stdout)
        self.assertNotIn("11", result.stdout.split("frame.fps", 1)[1])

    def test_nothing_came(self):
        result = self.run_cli("watch", OLD, "--seconds", "1")

        self.assertIn("no samples", result.stdout)


class ReportTests(Base):
    def setUp(self):
        super().setUp()
        self.backend.sessions = [session(OLD, status="ended")]

    def trend(self, stdout, name):
        return [line for line in stdout.splitlines() if line.startswith(f"{name} ")][-1].split()[1:]

    def test_window_rules(self):
        self.backend.samples[OLD] = [
            sample(1, 0, {"frame.fps": 60, "frame.dt.max": 20, "frame.over33": 1, "memory.allocated": 1048576}),
            sample(2, 1000, {"frame.fps": 40, "frame.dt.max": 90, "frame.over33": 2, "memory.allocated": 2097152}),
            sample(3, 2000, {"frame.fps": 30, "frame.dt.max": 30, "frame.over33": 4, "memory.allocated": 3145728}),
            sample(4, 3000, {"frame.fps": 10, "frame.dt.max": 40, "frame.over33": 8, "memory.allocated": 4194304}),
        ]
        result = self.run_cli("summary", OLD, "--windows", "2")

        self.assertEqual(READY, result.returncode, result.stderr)
        self.assertEqual(["50", "20"], self.trend(result.stdout, "frame.fps"))
        self.assertEqual(["90", "40"], self.trend(result.stdout, "frame.dt.max"))
        self.assertEqual(["3", "12"], self.trend(result.stdout, "frame.over33"))
        self.assertEqual(["2.0MB", "4.0MB"], self.trend(result.stdout, "memory.allocated"))

    def test_hidden_samples_stay_out(self):
        self.backend.samples[OLD] = [
            sample(1, 0, {"frame.fps": 60}),
            sample(2, 1000, {"frame.fps": 1}, hidden=True),
            sample(3, 2000, {"frame.fps": 60}),
        ]
        result = self.run_cli("summary", OLD, "--windows", "1")

        self.assertEqual(["60"], self.trend(result.stdout, "frame.fps"))
        self.assertIn("hidden=1", result.stdout)

    def test_missing_metrics_are_named(self):
        self.backend.samples[OLD] = [sample(1, 0, {"frame.fps": 60})]
        result = self.run_cli("summary", OLD)

        line = next(line for line in result.stdout.splitlines() if line.startswith("not_reported"))
        self.assertIn("frame.gpu", line)
        self.assertNotIn("frame.fps", line)

    def test_errors_of_a_live_session_are_grouped(self):
        row = {"seq": 1, "time": 500, "level": "exception", "message": "Boom", "stack": "Game.A:B ()", "repeat": 3,
               "receivedAt": "2026-11-03T10:00:00Z", "fingerprint": "00112233aabbccdd"}
        self.backend.find(OLD)["status"] = "live"
        self.backend.errors[OLD] = [row, {**row, "seq": 2, "time": 900, "repeat": 2}]
        result = self.run_cli("errors", OLD)

        self.assertEqual(READY, result.returncode, result.stderr)
        self.assertIn("00112233aabbccdd", result.stdout)
        self.assertIn("count=5", result.stdout)
        self.assertIn("Game.A:B", result.stdout)

    def test_error_groups_of_a_digest_are_timed_from_the_start(self):
        digest = {"durationMs": 30000, "errorRows": 1, "errorCount": 2, "marks": 0, "droppedSamples": 0, "droppedErrors": 0, "droppedMarks": 0,
                  "rejectedRequests": 0, "summary": {"samples": 0, "hiddenSamples": 0, "metrics": []},
                  "errorGroups": {"omittedGroups": 0, "omittedCount": 0, "groups": [
                      {"fingerprint": "ff", "level": "error", "message": "Failed", "count": 2, "rows": 1,
                       "firstSeenAt": "2026-11-03T10:00:20.5000000Z", "lastSeenAt": "2026-11-03T10:00:25Z"}]}}
        self.backend.find(OLD)["digest"] = digest
        result = self.run_cli("errors", OLD)

        self.assertIn("first=20.5s last=25.0s", result.stdout)

    def test_errors_of_all_sessions(self):
        self.backend.error_groups["groups"] = [{"fingerprint": "ff", "level": "error", "message": "Failed", "count": 7, "sessions": 2,
                                                "firstSeenAt": "2026-11-03T10:00:00Z", "lastSeenAt": "2026-11-03T10:00:00Z", "sessionIds": [OLD]}]
        result = self.run_cli("errors")

        self.assertIn("count=7", result.stdout)
        self.assertIn("sessions=2", result.stdout)


class StartupTests(Base):
    def page(self):
        spans = [startup_span(0, "Boot", duration=2000, own=200, frames=4),
                 startup_span(1, "Scope: Root", parent=0, depth=1, duration=1800, own=1800, frames=2),
                 startup_span(2, "Waiting", parent=0, depth=1, duration=31000, own=31000, frames=1835, unfinished=True)]

        return {"startup": startup_info(NEW), "spans": spans, "sessions": [session(OLD, status="ended", tags=["menu"], startup_id=NEW)]}

    def test_startups_lists_the_records(self):
        self.backend.startups = [self.page(), {**self.page(), "startup": startup_info(NEW2, final=False)}]

        result = self.run_cli("startups")

        self.assertEqual(result.returncode, READY)
        self.assertIn("startups=2", result.stdout)
        self.assertIn(f"{NEW} final", result.stdout)
        self.assertIn(f"{NEW2} snapshot", result.stdout)
        self.assertIn("duration=54.34s frames=3033 spans=3 depth=2", result.stdout)

    def test_startup_gives_the_own_time_first_with_the_frames_of_a_wait(self):
        self.backend.startups = [self.page()]

        result = self.run_cli("startup", NEW, "--top", "2")

        self.assertEqual(result.returncode, READY)
        lines = result.stdout.splitlines()
        waiting = next(index for index, line in enumerate(lines) if line.endswith("Waiting") and "16.9" in line)
        root = next(index for index, line in enumerate(lines) if line.endswith("Scope: Root"))
        self.assertLess(waiting, root)
        self.assertNotIn("200ms", result.stdout.split("unfinished 1")[0].split("own_time")[1], "cut by --top")
        self.assertIn("unfinished 1", result.stdout)
        self.assertIn("31.00s depth=1 Waiting", result.stdout)
        self.assertIn("dropped_spans=2 rejected=1", result.stdout)
        self.assertIn("sessions 1", result.stdout)
        self.assertIn(OLD, result.stdout)

    def test_a_snapshot_is_named(self):
        self.backend.startups = [{**self.page(), "startup": startup_info(NEW, final=False)}]

        self.assertIn("snapshot: the trace was open", self.run_cli("startup", NEW).stdout)

    def test_no_such_startup(self):
        result = self.run_cli("startup", ABSENT)

        self.assertEqual(result.returncode, MISSING)
        self.assertIn("no such session or startup", result.stderr)

    def test_an_unknown_device_prints_dashes(self):
        bare = {**startup_info(NEW), "device": {"platform": "web"}}
        self.backend.startups = [{**self.page(), "startup": bare, "sessions": [{**session(OLD, status="ended"), "device": {"platform": "web"}}]}]

        for result in (self.run_cli("startups"), self.run_cli("startup", NEW), self.run_cli("sessions")):
            self.assertEqual(result.returncode, READY)
            self.assertNotIn("None", result.stdout)

        self.assertIn("platform=web build=- duration=", self.run_cli("startups").stdout)
        self.assertIn("device platform=web build=- browser=- os=- gpu=- cores=- memoryGB=- screen=-", self.run_cli("startup", NEW).stdout)

    def test_a_screen_without_a_pixel_ratio_prints_a_dash(self):
        device = {"platform": "web", "screenWidth": 1920, "screenHeight": 1080}
        self.backend.startups = [{**self.page(), "startup": {**startup_info(NEW), "device": device}}]

        self.assertIn("screen=1920x1080@-", self.run_cli("startup", NEW).stdout)

    def test_summary_shows_the_startup_of_the_session(self):
        self.backend.sessions = [session(OLD, status="ended", startup_id=NEW), session(NEW2, status="ended")]

        self.assertIn(f"startup={NEW}", self.run_cli("summary", OLD).stdout)
        self.assertIn("startup=None", self.run_cli("summary", NEW2).stdout)


if __name__ == "__main__":
    unittest.main()
