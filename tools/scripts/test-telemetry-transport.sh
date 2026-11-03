#!/usr/bin/env bash
# The client telemetry end to end: the real client of client/modules/telemetry in Unity PlayMode, over the real connection of the
# module network and a real websocket (/test-host/meta), against a real backend on this machine (backend/ClientTelemetry.TestHost: Kestrel, a silo, its own PostgreSQL container, not a volume of a game).
#
#   build the host -> start it -> PlayMode TelemetryTransportEndToEndTests -> stop it
#
# Unity runs on client/project of this tree, so no other editor may have it open. The tests are ignored without the host, so here
# a run passes only when all of them ran and passed. One test takes the real 20 s timeout of a request.
#
# Environment: TELEMETRY_TRANSPORT_TIMEOUT - the limit of the Unity stage in seconds (default 600).
# Logs and the results: .build/telemetry-transport. Exit code: 0 passed, 1 failed, 2 could not start.
set -euo pipefail

if (( $# > 0 )); then
    sed -n '2,/^set -euo/p' "${BASH_SOURCE[0]}" | sed '$d' | sed 's/^# \{0,1\}//' >&2
    exit 2
fi

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WORK="$ROOT/.build/telemetry-transport"
PROJECT="$ROOT/client/project"
HOST_BIN="$ROOT/.build/bin/ClientTelemetry.TestHost/Debug/net10.0/Framework.ClientTelemetry.TestHost"
RESULTS="$WORK/results.xml"
HOST_LOG="$WORK/host.log"
UNITY_LOG="$WORK/unity.log"
ADDRESS_FILE="$WORK/address"
FILTER="Global.Telemetry.Tests.Transport.TelemetryTransportEndToEndTests"
TIMEOUT="${TELEMETRY_TRANSPORT_TIMEOUT:-600}"
HOST_START_SECONDS=120

die() {
    echo "test-telemetry-transport: $*" >&2
    exit 2
}

VERSION="$(sed -n 's/^m_EditorVersion: //p' "$PROJECT/ProjectSettings/ProjectVersion.txt")"
UNITY="/tools/unity/$VERSION/Editor/Unity"
[[ -x "$UNITY" ]] || die "no Unity editor at $UNITY"

mkdir -p "$WORK"
rm -f "$RESULTS" "$HOST_LOG" "$UNITY_LOG" "$ADDRESS_FILE"

dotnet build "$ROOT/backend/ClientTelemetry.TestHost/ClientTelemetry.TestHost.csproj" -v quiet --nologo >"$WORK/build.log" 2>&1 \
    || die "the build of the host failed, see $WORK/build.log"

TOKEN="$(head -c 16 /dev/urandom | od -An -tx1 | tr -d ' \n')"
"$HOST_BIN" --token "$TOKEN" --address-file "$ADDRESS_FILE" >"$HOST_LOG" 2>&1 &
HOST_PID=$!

stop_host() {
    kill -TERM "$HOST_PID" 2>/dev/null || return 0
    wait "$HOST_PID" 2>/dev/null || true
}
trap stop_host EXIT
trap 'exit 130' INT TERM HUP

for (( second = 0; second < HOST_START_SECONDS; second++ )); do
    [[ -s "$ADDRESS_FILE" ]] && break
    kill -0 "$HOST_PID" 2>/dev/null || die "the host stopped at the start, see $HOST_LOG"
    sleep 1
done
[[ -s "$ADDRESS_FILE" ]] || die "the host did not start in $HOST_START_SECONDS s, see $HOST_LOG"

UNITY_CODE=0
CLIENT_TELEMETRY_HOST="$(<"$ADDRESS_FILE")" CLIENT_TELEMETRY_TOKEN="$TOKEN" timeout -k 5 "$TIMEOUT" "$UNITY" \
    -batchmode -nographics -projectPath "$PROJECT" \
    -runTests -testPlatform PlayMode -testFilter "$FILTER" \
    -testResults "$RESULTS" -logFile "$UNITY_LOG" || UNITY_CODE=$?

stop_host
trap - EXIT
grep '^\[telemetry-host\]' "$HOST_LOG"

[[ -f "$RESULTS" ]] || { echo "No results, exit code of Unity $UNITY_CODE, see $UNITY_LOG" >&2; exit 1; }

# testcasecount, passed, failed, skipped, inconclusive of the run.
summary() {
    python3 -I - "$RESULTS" <<'PY'
import sys
import xml.etree.ElementTree as tree
run = tree.parse(sys.argv[1]).getroot()
print(*(run.get(name) for name in ("testcasecount", "passed", "failed", "skipped", "inconclusive")))
for case in run.iter("test-case"):
    print("  ", case.get("result"), case.get("name"), case.get("duration"), "s")
PY
}

summary
read -r TOTAL PASSED FAILED SKIPPED INCONCLUSIVE < <(summary | head -1)
if (( UNITY_CODE != 0 || TOTAL == 0 || PASSED != TOTAL || FAILED != 0 || SKIPPED != 0 || INCONCLUSIVE != 0 )); then
    echo "FAILED: exit code of Unity $UNITY_CODE, see $RESULTS and $UNITY_LOG" >&2
    exit 1
fi

echo "PASSED: $PASSED tests"
