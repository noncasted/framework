#!/usr/bin/env bash
# The full test suite of the framework, on the current state of the tree (uncommitted edits included).
#
#   copy, then in parallel:
#     build -> backend, common, verify (dotnet, after the build)
#     control, telemetry (python, the tests of the skills control and client-telemetry)
#     editmode (Unity, client/project)
#     playmode (Unity, the second copy of the project)
#
# Unity needs no dotnet build, so its stages start with the build and not after it.
# The second copy: Unity does not open one project twice. .build/test-all/playmode mirrors the layout of the framework, so
# the relative links of Assets and the file: packages of the manifest stay valid; it gets its own copies of the sources,
# because the EditMode tests write assets the other editor would pick up on a refresh. Library is seeded once and
# survives the runs; the sources are synced with their times kept, so Unity recompiles only what changed.
#
# Verify runs the Debug build of the solution: only a Release build overwrites client/core/Analyzers/ContainerGenerator.dll.
#
# A stage passes when its process exits with 0 and its report exists, is readable and holds tests, none of them failed.
#
# Only the PlayMode copy is a snapshot: build, backend, common, verify and editmode read the tree itself. A source edited
# during the run (another session working in the tree) is named in the summary and in the reason of a failed stage: a
# refresh of the EditMode editor that sees a changed script requests a compilation, and while it is pending UniTask does
# not pump the editor loop, so the next test awaiting a frame hangs until the stage limit.
#
# Environment: TEST_ALL_TIMEOUT - the limit of one stage in seconds (default 300).
# Logs: .build/test-all/logs/<time>, the last 10 runs. Exit code: 0 all passed, 1 a stage failed, 2 could not start.
set -euo pipefail

if (( $# > 0 )); then
    sed -n '2,/^set -euo/p' "${BASH_SOURCE[0]}" | sed '$d' | sed 's/^# \{0,1\}//' >&2
    exit 2
fi

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WORK="$ROOT/.build/test-all"
COPY="$WORK/playmode"
PROJECT="$ROOT/client/project"
LOGS="$WORK/logs/$(date +%Y%m%d-%H%M%S)"
TIMEOUT="${TEST_ALL_TIMEOUT:-300}"
BIN="$ROOT/.build/bin"

die() {
    echo "test-all: $*" >&2
    exit 2
}

VERSION="$(sed -n 's/^m_EditorVersion: //p' "$PROJECT/ProjectSettings/ProjectVersion.txt")"
UNITY="/tools/unity/$VERSION/Editor/Unity"
[[ -x "$UNITY" ]] || die "no Unity editor at $UNITY"
[[ -d "$PROJECT/Library" ]] || die "no $PROJECT/Library: copy it from another tree with cp -a --reflink=auto, a full import does not fit the stage limit"

# Other trees have their own .build, so this lock stops only a second run of the same tree. Stages close its descriptor:
# build servers outlive the run and would keep the lock.
mkdir -p "$WORK"
exec 9>"$WORK/lock"
flock -n 9 || die "another run of this tree holds $WORK/lock"

mkdir -p "$LOGS"
ls -1dt "$WORK"/logs/* | tail -n +11 | xargs -r rm -rf

# Kills what is still running when the script stops early: timeout passes the signal to the group of its command.
stop_stages() {
    local file
    for file in "$LOGS"/*.pid; do
        [[ -e "$file" ]] && kill -TERM "$(<"$file")" 2>/dev/null
    done
    return 0
}
trap stop_stages EXIT
trap 'exit 130' INT TERM HUP

# Runs a command of a stage under the limit of a stage. Returns its exit code (124 or 137: the limit ran out).
limited() {
    local stage="$1"
    shift
    local code=0
    timeout -k 5 "$TIMEOUT" "$@" &
    local pid=$!
    echo "$pid" >"$LOGS/$stage.pid"
    wait "$pid" || code=$?
    rm "$LOGS/$stage.pid"
    return "$code"
}

# Result of a stage: "<PASS|FAIL>|<tests>|<reason>" in <stage>.result, failed tests in <stage>.failed.
record() {
    echo "$2|$3|$4" >"$LOGS/$1.result"
}

# A stage without a result died or never ran: it counts as failed.
passed() {
    [[ -e "$LOGS/$1.result" ]] && [[ "$(cut -d'|' -f1 "$LOGS/$1.result")" == PASS ]]
}

seconds_since() {
    awk -v a="$1" -v b="$EPOCHREALTIME" 'BEGIN { printf "%.1f", b - a }'
}

write_time() {
    seconds_since "$2" >"$LOGS/$1.time"
}

# Why a process failed, empty when it exited with 0.
exit_reason() {
    case "$1" in
        0) ;;
        124 | 137) echo "timeout ${TIMEOUT} s" ;;
        *) echo "exit $1" ;;
    esac
}

# Prints "<passed> <failed> <skipped> <errors>", then the failed tests of an xunit or an NUnit 3 report.
read_report() {
    python3 -I - "$1" <<'PY'
import sys
import xml.etree.ElementTree as ET

root = ET.parse(sys.argv[1]).getroot()
if root.tag == "assemblies":
    counts = [sum(int(a.get(key, 0)) for a in root.iter("assembly")) for key in ("passed", "failed", "skipped", "errors")]
    failed = [t.get("name") for t in root.iter("test") if t.get("result") == "Fail"]
elif root.tag == "test-run":
    # Inconclusive tests are neither passed, failed nor skipped: they count as failed, nothing else would show them.
    a = root.attrib
    counts = [int(a["passed"]), int(a["failed"]) + int(a["inconclusive"]), int(a["skipped"]), 0]
    failed = [t.get("fullname") for t in root.iter("test-case") if t.get("result") in ("Failed", "Inconclusive")]
else:
    raise SystemExit("unknown report: " + root.tag)
print(*counts)
print(*failed, sep="\n")
PY
}

# Records a stage with a report: both the exit code and the report decide.
record_report() {
    local stage="$1" code="$2" report="$3"
    local reason tests="-" passed=0 failed=0 skipped=0 errors=0 parsed=""
    reason="$(exit_reason "$code")"
    if parsed="$(read_report "$report" 2>/dev/null)"; then
        read -r passed failed skipped errors <<<"${parsed%%$'\n'*}"
        tail -n +2 <<<"$parsed" | sed '/^$/d' >"$LOGS/$stage.failed"
        tests="$passed / $((passed + failed + skipped))"
        (( skipped == 0 )) || tests+=" ($skipped skipped)"
        (( failed == 0 )) || reason="$failed failed${reason:+, $reason}"
        (( errors == 0 )) || reason="$errors errors${reason:+, $reason}"
        (( passed + failed + skipped > 0 )) || reason="no tests${reason:+, $reason}"
    else
        reason="no report${reason:+, $reason}"
    fi
    record "$stage" "$([[ -z "$reason" ]] && echo PASS || echo FAIL)" "$tests" "$reason"
}

stage_build() {
    local code=0
    limited build dotnet build "$ROOT/framework.slnx" --nologo -v q >"$LOGS/build.log" 2>&1 || code=$?
    if (( code == 0 )); then
        record build PASS - ""
        return
    fi
    grep -E ": error " "$LOGS/build.log" | sort -u | head -30 >"$LOGS/build.failed" || true
    record build FAIL - "$(exit_reason "$code")"
}

stage_xunit() {
    local stage="$1" exe="$2" code=0
    limited "$stage" "$exe" -noColor -xml "$LOGS/$stage.xml" >"$LOGS/$stage.log" 2>&1 || code=$?
    record_report "$stage" "$code" "$LOGS/$stage.xml"
}

stage_verify() {
    local code=0 reason passed
    limited verify "$BIN/ContainerGenerator.Verify/Debug/net10.0/ContainerGenerator.Verify" >"$LOGS/verify.log" 2>&1 || code=$?
    passed="$(grep -c '^PASS  ' "$LOGS/verify.log" || true)"
    grep '^FAIL  ' "$LOGS/verify.log" >"$LOGS/verify.failed" || true
    reason="$(exit_reason "$code")"
    if [[ "$(tail -n 1 "$LOGS/verify.log")" != "ALL PASSED" ]]; then
        reason="no ALL PASSED${reason:+, $reason}"
    fi
    record verify "$([[ -z "$reason" ]] && echo PASS || echo FAIL)" "$passed" "$reason"
}

# The import workers and the Burst client of an editor leave its process group and outlive a killed editor: they are
# found by the variable they inherit.
kill_stage_processes() {
    grep -lzxF "TEST_ALL_STAGE=$LOGS/$1" /proc/[0-9]*/environ 2>/dev/null | cut -d/ -f3 | xargs -r kill -KILL 2>/dev/null || true
}

stage_unity() {
    local stage="$1" project="$2" platform="$3" code=0
    TEST_ALL_STAGE="$LOGS/$stage" limited "$stage" "$UNITY" -batchmode -projectPath "$project" -runTests -testPlatform "$platform" \
        -testResults "$LOGS/$stage.xml" -logFile "$LOGS/$stage.log" >"$LOGS/$stage.out" 2>&1 || code=$?
    kill_stage_processes "$stage"
    record_report "$stage" "$code" "$LOGS/$stage.xml"
    # No failed test in the report and still a failure: the cause is in the log (compile errors, a crash, a second editor).
    if [[ ! -s "$LOGS/$stage.failed" ]] && ! passed "$stage"; then
        grep -m 10 -E "error CS|Caught fatal signal|another Unity instance|Multiple Unity instances" "$LOGS/$stage.log" \
            >"$LOGS/$stage.failed" || true
    fi
}

# Seeds Library once, through a temporary name: an interrupted seed does not pass for a finished one.
stage_copy() {
    mkdir -p "$COPY/client/project"
    if [[ ! -d "$COPY/client/project/Library" ]]; then
        rm -rf "$COPY/client/project/Library.seed"
        cp -a --reflink=auto "$PROJECT/Library" "$COPY/client/project/Library.seed"
        mv "$COPY/client/project/Library.seed" "$COPY/client/project/Library"
    fi
    local folder
    for folder in Assets Packages ProjectSettings; do
        rsync -a --delete "$PROJECT/$folder/" "$COPY/client/project/$folder/"
    done
    for folder in client/core client/flow client/modules client/platforms client/packages common shared; do
        mkdir -p "$COPY/$folder"
        rsync -a --delete "$ROOT/$folder/" "$COPY/$folder/"
    done
    record copy PASS - ""
}

stage_python() {
    local stage="$1" folder="$2" code=0 tests
    PYTHONDONTWRITEBYTECODE=1 limited "$stage" python3 -m unittest discover -s "$folder" >"$LOGS/$stage.log" 2>&1 || code=$?
    tests="$(sed -n 's/^Ran \([0-9]*\) tests\? .*/\1/p' "$LOGS/$stage.log")"
    if (( code == 0 )) && [[ -n "$tests" ]]; then
        record "$stage" PASS "$tests" ""
    else
        record "$stage" FAIL "${tests:--}" "exit code $code"
        grep -E '^(FAIL|ERROR):' "$LOGS/$stage.log" >"$LOGS/$stage.failed" || true
    fi
}

# The sources the stages compile, changed since the start of the run: Unity scripts and the dotnet projects.
changed_sources() {
    find "$ROOT/client/core" "$ROOT/client/flow" "$ROOT/client/modules" "$ROOT/client/platforms" "$ROOT/common" \
        "$ROOT/shared" "$ROOT/backend" \( -name bin -o -name obj \) -prune -o -type f \
        \( -name '*.cs' -o -name '*.asmdef' -o -name '*.asmref' -o -name '*.rsp' -o -name '*.csproj' -o -name '*.props' \
        -o -name '*.targets' -o -name '*.razor' \) -newer "$LOGS/sources.stamp" -print | sed "s|^$ROOT/||" | sort
}

# Runs a stage as a job of its own and writes its time.
start_stage() {
    local stage="$1"
    shift
    {
        local begin=$EPOCHREALTIME
        "$@"
        write_time "$stage" "$begin"
    } 9>&- &
}

# Everything after the build runs on its output.
stage_dotnet() {
    local begin=$EPOCHREALTIME
    stage_build
    write_time build "$begin"
    passed build || return 0
    start_stage backend stage_xunit backend "$BIN/Tests/Debug/net10.0/Framework.Tests"
    start_stage common stage_xunit common "$BIN/Framework.Common.Tests/Debug/net10.0/Framework.Common.Tests"
    start_stage verify stage_verify
    wait
}

BEGIN_ALL=$EPOCHREALTIME
echo "load: $(uptime | sed 's/.*load average: //'), logs: $LOGS"

touch "$LOGS/sources.stamp"
begin=$EPOCHREALTIME
stage_copy 9>&-
write_time copy "$begin"

stage_dotnet 9>&- &
start_stage control stage_python control "$ROOT/.claude/skills/control/tests"
start_stage telemetry stage_python telemetry "$ROOT/.claude/skills/client-telemetry/tests"
start_stage editmode stage_unity editmode "$PROJECT" EditMode
start_stage playmode stage_unity playmode "$COPY/client/project" PlayMode
wait

changed_sources >"$LOGS/sources.changed"
if [[ -s "$LOGS/sources.changed" ]]; then
    for stage in build backend common verify editmode; do
        [[ -e "$LOGS/$stage.result" ]] && ! passed "$stage" && sed -i 's/$/, sources changed during the run/' "$LOGS/$stage.result"
    done
fi

failed=0
printf '\n%-10s %8s  %-24s %s\n' stage "time, s" tests result
for stage in copy build backend common verify control telemetry editmode playmode; do
    status=FAIL tests=- reason="did not finish"
    [[ -e "$LOGS/$stage.result" ]] && IFS='|' read -r status tests reason <"$LOGS/$stage.result"
    passed "$stage" || failed=1
    printf '%-10s %8s  %-24s %s\n' "$stage" "$(cat "$LOGS/$stage.time" 2>/dev/null || echo -)" "$tests" "$status${reason:+: $reason}"
done
echo "wall $(seconds_since "$BEGIN_ALL") s, load: $(uptime | sed 's/.*load average: //')"

if [[ -s "$LOGS/sources.changed" ]]; then
    echo
    echo "sources changed during the run, the stages on the tree did not test one state: $LOGS/sources.changed"
    head -n 10 "$LOGS/sources.changed" | sed 's/^/  /'
fi

for stage in copy build backend common verify control telemetry editmode playmode; do
    passed "$stage" && continue
    echo
    echo "$stage failed, log: $LOGS/$stage.log"
    [[ -s "$LOGS/$stage.failed" ]] || continue
    head -n 30 "$LOGS/$stage.failed" | sed 's/^/  /'
    (( $(wc -l <"$LOGS/$stage.failed") <= 30 )) || echo "  ... all of them: $LOGS/$stage.failed"
done

(( failed == 0 ))
