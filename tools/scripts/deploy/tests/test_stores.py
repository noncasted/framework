# The store commands (errors, logs, trace, around, tail) against fake stores answering recorded rows, directly and through
# the ssh tunnel (fake_ssh):
#   python3 -m unittest discover -s tools/scripts/deploy/tests
import socket
import sys
from unittest import mock

from support import TESTS, Base  # first: it puts the scripts on the path

import commands  # noqa: E402
import trace_text  # noqa: E402
from fake_backend import FIXTURES, FakeStore, rows  # noqa: E402

TRACE = "f144c9dd095de444d421ad87d9aa621a"
LOGS_SCOPE = '{service.namespace="pixel-swarm"}'
SPANS_SCOPE = '"resource_attr:service.namespace":="pixel-swarm"'


class Stores(Base):
    def setUp(self):
        super().setUp()
        self.logs = FakeStore()
        self.traces = FakeStore()
        self.addCleanup(self.logs.close)
        self.addCleanup(self.traces.close)

    def run_store(self, *arguments, project="pixel-swarm"):
        return self.run_cli(*arguments, "--registry", str(self.registry), "--project", project,
                            "--logs-url", self.logs.url, "--traces-url", self.traces.url)


class Errors(Stores):
    def test_groups_by_template_with_an_example(self):
        self.logs.answer = lambda form: rows("errors.jsonl")

        result = self.run_store("errors")

        self.assert_code(result, "ok")
        self.assertEqual([
            "[pixel-swarm on ovh, namespace pixel-swarm]",
            "4 warnings and errors of pixel-swarm in the last 1h, 3 templates (count, last, service, level):",
            "    2  11-25 05:25:17.564  silo  Warning  Lobby <N> is full: <N> of <N> players",
            "       e.g. Lobby 17 is full: 8 of 8 players",
            "    1  11-25 05:25:17.565  silo  Warning  Slow grain call LobbyGrain.Join took <N> ms",
            "       e.g. Slow grain call LobbyGrain.Join took 1200 ms",
            "    1  11-25 05:25:17.582  meta  Error  Request POST /api/match/start failed in <N> ms",
            "       e.g. Request POST /api/match/start failed in 48 ms",
            "       ! System.InvalidOperationException: Lobby 42 is full",
            f"       trace={TRACE}",
        ], result.stdout.splitlines())
        form = self.logs.requests[0]
        self.assertEqual(LOGS_SCOPE, form["extra_stream_filters"])
        self.assertEqual("1h", form["start"])
        self.assertTrue(form["query"].startswith("severity_number:>=13 | "), form["query"])
        self.assertIn("collapse_nums at template", form["query"])
        self.assertIn("limit 30", form["query"])
        self.assertEqual([], self.traces.requests)

    def test_one_service_over_a_day(self):
        result = self.run_store("errors", "--service", "meta", "--since", "1d", "--limit", "5")

        self.assertIn("no warnings or errors of pixel-swarm in the last 1d", result.stdout)
        form = self.logs.requests[0]
        self.assertEqual('{service.namespace="pixel-swarm",service.name="meta"}', form["extra_stream_filters"])
        self.assertEqual("1d", form["start"])
        self.assertIn("limit 5", form["query"])

    def test_another_namespace_on_the_server_of_the_project(self):
        result = self.run_cli("errors", "--registry", str(self.registry), "--project", "peace-data-reborn", "--namespace",
                              "smoke-test", "--logs-url", self.logs.url, "--traces-url", self.traces.url)

        self.assertIn("[peace-data-reborn on firstvds, namespace smoke-test]", result.stdout)
        self.assertEqual('{service.namespace="smoke-test"}', self.logs.requests[0]["extra_stream_filters"])


class Logs(Stores):
    def test_the_query_goes_as_it_is(self):
        self.logs.answer = lambda form: rows("stats.jsonl")

        result = self.run_store("logs", "--query", "* | stats by (service.name) count() n", "--limit", "7")

        self.assert_code(result, "ok")
        self.assertEqual(["service.name=meta  n=2", "service.name=silo  n=3"], result.stdout.splitlines()[1:])
        form = self.logs.requests[0]
        self.assertEqual("* | stats by (service.name) count() n", form["query"])
        self.assertEqual("7", form["limit"])
        self.assertEqual(LOGS_SCOPE, form["extra_stream_filters"])

    def test_log_rows_are_lines_in_time_order_with_the_exception(self):
        self.logs.answer = lambda form: list(reversed(rows("logs_of_trace.jsonl")))

        result = self.run_store("logs", "--query", f'trace_id:="{TRACE}"')

        self.assertEqual([
            f"11-25 05:25:17.534 meta INFO Match start requested by user 1001  trace={TRACE}",
            f"11-25 05:25:17.563 silo WARN Lobby 42 is full: 8 of 8 players  trace={TRACE}",
            f"11-25 05:25:17.582 meta ERROR Request POST /api/match/start failed in 48 ms  trace={TRACE}",
            "    ! System.InvalidOperationException: Lobby 42 is full",
            "      at Game.Lobby.Join()",
            "      at Game.Match.Start()",
        ], result.stdout.splitlines()[1:])

    def test_the_newest_rows_only_says_so(self):
        self.logs.answer = lambda form: rows("tail.jsonl")

        result = self.run_store("logs", "--query", "*", "--limit", "5")

        self.assertEqual("(only the newest 5 rows: narrow the query or the time)", result.stdout.splitlines()[-1])

    def test_a_query_the_store_refuses(self):
        self.logs.answer = lambda form: (400, (FIXTURES / "bad_query.txt").read_text())

        result = self.run_store("logs", "--query", "bad ((")

        self.assert_code(result, "service")
        self.assertIn("error: VictoriaLogs 400: cannot parse `query` arg", result.stderr)

    def test_an_unreachable_store(self):
        self.logs.close()

        result = self.run_store("logs", "--query", "*")

        self.assert_code(result, "service")
        self.assertIn("VictoriaLogs at", result.stderr)
        self.assertIn("unreachable", result.stderr)

    def test_both_urls_or_none(self):
        result = self.run_cli("logs", "--query", "*", "--registry", str(self.registry), "--project", "pixel-swarm",
                              "--logs-url", self.logs.url)

        self.assert_code(result, "usage")

    def test_a_project_without_deploy(self):
        result = self.run_store("logs", "--query", "*", project="neon-dodge")

        self.assert_code(result, "usage")
        self.assertIn("neon-dodge is not deployed", result.stderr)

    def test_no_project_outside_a_game(self):
        result = self.run_cli("logs", "--query", "*", "--registry", str(self.registry), "--logs-url", self.logs.url,
                              "--traces-url", self.traces.url)

        self.assert_code(result, "usage")
        self.assertIn("is not a project of the registry: pass --project", result.stderr)


class Trace(Stores):
    def test_the_span_tree(self):
        self.traces.answer = lambda form: rows("spans_of_trace.jsonl")

        result = self.run_store("trace", TRACE.upper())

        self.assert_code(result, "ok")
        self.assertEqual([
            f"trace {TRACE}: 3 spans, 50.0ms, 2 errors, services meta, silo, started 11-25 05:25:17.533 UTC (offset, duration, service, span)",
            "+    0.0ms    50.0ms  meta  POST /api/match/start  ERROR",
            "                      http.request.method=POST  http.response.status_code=500",
            "+   10.0ms    35.0ms  silo    MatchGrain.Start",
            "+   20.0ms    20.0ms  silo      LobbyGrain.Join  ERROR Lobby 42 is full",
            "                          lobby.id=42",
            "                          ! System.InvalidOperationException: Lobby 42 is full",
        ], result.stdout.splitlines()[1:])
        form = self.traces.requests[0]
        self.assertEqual(f'trace_id:="{TRACE}" name:*', form["query"])
        self.assertEqual(SPANS_SCOPE, form["extra_filters"])
        self.assertEqual("1d", form["start"])

    def test_a_span_whose_parent_is_not_there(self):
        spans = [span for span in rows("spans_of_trace.jsonl") if span["name"] != "MatchGrain.Start"]

        lines = trace_text.tree(TRACE, spans)

        self.assertIn("silo  LobbyGrain.Join  ERROR Lobby 42 is full  (parent 017f2ebe290f3aff not found)", lines[-3])

    def test_an_absent_trace(self):
        result = self.run_store("trace", "0" * 32, "--since", "3d")

        self.assert_code(result, "not_found")
        self.assertIn(f"no spans of trace {'0' * 32} in namespace pixel-swarm on ovh over the last 3d", result.stderr)

    def test_not_a_trace_id(self):
        self.assert_code(self.run_store("trace", "abc"), "usage")


class Around(Stores):
    def test_logs_and_spans_on_one_time_line(self):
        self.logs.answer = lambda form: rows("logs_of_trace.jsonl")
        self.traces.answer = lambda form: rows("spans_of_trace.jsonl")

        result = self.run_store("around", TRACE)

        self.assert_code(result, "ok")
        self.assertEqual([
            f"trace {TRACE}: 3 spans, 3 logs (UTC)",
            "11-25 05:25:17.533 meta SPAN POST /api/match/start 50.0ms  ERROR",
            "    http.request.method=POST  http.response.status_code=500",
            "11-25 05:25:17.534 meta INFO Match start requested by user 1001",
            "11-25 05:25:17.543 silo SPAN MatchGrain.Start 35.0ms",
            "11-25 05:25:17.553 silo SPAN LobbyGrain.Join 20.0ms  ERROR Lobby 42 is full",
            "    lobby.id=42",
            "    ! System.InvalidOperationException: Lobby 42 is full",
            "11-25 05:25:17.563 silo WARN Lobby 42 is full: 8 of 8 players",
            "11-25 05:25:17.582 meta ERROR Request POST /api/match/start failed in 48 ms",
            "    ! System.InvalidOperationException: Lobby 42 is full",
            "      at Game.Lobby.Join()",
            "      at Game.Match.Start()",
        ], result.stdout.splitlines()[1:])
        self.assertEqual(f'trace_id:="{TRACE}"', self.logs.requests[0]["query"])
        self.assertEqual(LOGS_SCOPE, self.logs.requests[0]["extra_stream_filters"])
        self.assertEqual(SPANS_SCOPE, self.traces.requests[0]["extra_filters"])

    def test_nothing_of_the_trace(self):
        result = self.run_store("around", TRACE)

        self.assert_code(result, "not_found")
        self.assertIn("no logs or spans of trace", result.stderr)


class Tail(Stores):
    def test_the_last_lines_oldest_first(self):
        self.logs.answer = lambda form: list(reversed(rows("tail.jsonl")))

        result = self.run_store("tail", "--service", "silo")

        lines = result.stdout.splitlines()
        self.assertEqual(1 + 5 + 3, len(lines))  # the place, five rows, the exception of the error
        self.assertTrue(lines[1].startswith("11-25 05:25:17.534 meta INFO"), lines[1])
        form = self.logs.requests[0]
        self.assertEqual(("*", "5m", "50"), (form["query"], form["start"], form["limit"]))
        self.assertEqual('{service.namespace="pixel-swarm",service.name="silo"}', form["extra_stream_filters"])

    def test_follow_prints_only_new_rows(self):
        recorded = sorted(rows("tail.jsonl"), key=lambda row: row["_time"])
        # The store answers from the given start on, that instant included.
        answers = [recorded[1:3], recorded[2:4], recorded[3:5]]
        store = mock.Mock()
        store.query.side_effect = [*answers, KeyboardInterrupt()]
        printed = []

        with mock.patch.object(commands.time, "sleep"), mock.patch.object(commands, "_print", printed.extend):
            commands._follow(store, recorded[:2])

        self.assertEqual(["11-25 05:25:17.564", "11-25 05:25:17.565", "11-25 05:25:17.582"],
                         [line[:18] for line in printed if not line.startswith(" ")])
        self.assertEqual([row["_time"] for row in recorded[1:4]], [call.args[1] for call in store.query.call_args_list[:3]])


class Tunnel(Stores):
    def setUp(self):
        super().setUp()
        self.ssh_log = self.temp / "ssh.log"
        self.ssh_env = {"DEPLOY_SSH": str(TESTS / "fake_ssh"), "FAKE_SSH_LOG": str(self.ssh_log),
                        "FAKE_SSH_9428": str(self.logs.port), "FAKE_SSH_10428": str(self.traces.port)}

    def run_tunnel(self, *arguments, **env):
        return self.run_cli(*arguments, "--registry", str(self.registry), "--project", "pixel-swarm", env={**self.ssh_env, **env})

    def test_the_stores_of_the_server_through_its_alias(self):
        self.logs.answer = lambda form: rows("logs_of_trace.jsonl")
        self.traces.answer = lambda form: rows("spans_of_trace.jsonl")

        result = self.run_tunnel("around", TRACE)

        self.assert_code(result, "ok")
        self.assertIn("11-25 05:25:17.534 meta INFO Match start requested by user 1001", result.stdout)
        call, closed = self.ssh_log.read_text().splitlines()
        arguments = call.split()
        self.assertEqual("ovh", arguments[-1])
        self.assertIn("BatchMode=yes", arguments)
        self.assertIn("ExitOnForwardFailure=yes", arguments)
        forwards = [arguments[index + 1] for index, argument in enumerate(arguments) if argument == "-L"]
        self.assertEqual([":127.0.0.1:9428", ":127.0.0.1:10428"], [forward[forward.index(":"):] for forward in forwards])
        self.assertEqual("closed", closed)

        for forward in forwards:
            with socket.socket() as probe:
                self.assertNotEqual(0, probe.connect_ex(("127.0.0.1", int(forward.split(":")[0]))))

    def test_an_ssh_that_fails(self):
        result = self.run_tunnel("errors", FAKE_SSH_FAIL="ssh: Could not resolve hostname ovh: Name or service not known")

        self.assert_code(result, "service")
        self.assertIn("error: ssh ovh failed (exit 255): ssh: Could not resolve hostname ovh", result.stderr)
        self.assertEqual([], self.logs.requests)


if __name__ == "__main__":
    sys.exit("run with: python3 -m unittest discover -s tools/scripts/deploy/tests")
