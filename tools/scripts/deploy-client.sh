#!/usr/bin/env bash
# Tests and WebGL builds of a game at once, then the uploads: the parallel path of the deploy-client skill.
#
# Run from the root of the game: tools/framework/tools/scripts/deploy-client.sh [target...] [--notest] [--no-deploy]
#
#   1. snapshot  a reflink copy (cp -a --reflink=always) of the working tree of the game, uncommitted edits included,
#                with the working tree of tools/framework (or of FRAMEWORK_SOURCE) and client/Library (minus what a
#                build must not inherit, see deploy-client/snapshot.sh); the "same" files are compared there
#   2. at once   the tests: the "tests" script of the game run FROM the snapshot (it tests exactly that tree);
#                one reflink copy of the snapshot per target, each built by its own batchmode Unity
#                (Internal.WebGlBuild.Build with -activeBuildProfile), at most UNITY_BUILD_SLOTS builds on the machine
#   3. checks    of every build right after it (deploy-client/verify.sh)
#   4. deploy    only when the tests passed and every target passed its checks: target after target, the "deploy"
#                script of the game from the root of the game with BUILD_DIR=<output in the copy>, then its --list
#                must name the deployed tag as current. A failed upload stops the remaining ones.
# Failed tests kill the builds of this run and nothing is uploaded. The copies are deleted on exit, the logs stay.
#
# The game describes itself in .claude/deploy-client.json (no values of a game in this script):
#   {
#     "tests": "tools/scripts/run-tests.sh",       // run in the snapshot with TEST_LOG_DIR, exit 0 = passed
#     "deploy": "tools/scripts/deploy-webgl.sh",   // run in the game root with BUILD_DIR=<abs>; prints "Deployed ... <tag>";
#                                                  // with --list its last line names the current release
#     "exclude": ["builds", "client/Build"],       // paths (globs, from the root) left out of the snapshot
#     "same": [["<file>", "<file>"]],              // files of the tree that must be byte-equal (cmp)
#     "targets": [{                                // the order is the order of the uploads
#       "name": "web",                             // the argument of this script
#       "profile": "Assets/Settings/Build Profiles/website.asset",   // relative to client/
#       "output": "builds/website",                // relative to the root; its name names the stripping backup folder
#       "deployArgs": ["--target", "web"],         // arguments of the deploy script for this target
#       "indexContains": ["..."],                  // strings index.html must hold
#       "indexExcludes": ["..."],                  // strings index.html must not hold
#       "files": ["TemplateData/..."]              // files the output must hold
#     }]
#   }
#
# Environment (all optional):
#   FRAMEWORK_SOURCE=<dir>        tools/framework of the snapshot from this folder (a change of the framework before
#                                 the pointer of the submodule moves)
#   DEPLOY_COPY_DIR=/var/tmp      parent of the copies, on the btrfs of the game (/tmp is tmpfs: no reflink)
#   DEPLOY_LOG_DIR=<dir>          logs; default /tmp/<game>-deploy-client/<time>
#   UNITY_EDITOR=<path>           Unity binary; default: the version of ProjectVersion.txt under /tools/unity or the Hub
#   UNITY_BUILD_SLOTS=2           batchmode builds at once on the machine (/tmp/claude-1000/unity-build.slot<i>.lock)
#   UNITY_BUILD_MIN_FREE_GB=10    MemAvailable a build needs to start (plus as much per build started in the last 60 s)
#   UNITY_BUILD_MEM_WAIT=900      seconds to wait for that memory with a free slot, then the build fails
#   UNITY_BUILD_TIMEOUT=1800      seconds of one build before SIGKILL
#   KEEP_COPY=1                   keep the copies (their path is printed)
#
# Exit code: 0 everything passed (and was uploaded), 1 a stage failed, 2 bad arguments or configuration.
set -euo pipefail

# bash reads a script while it runs: run from a temporary copy, so an edit of these files breaks no run in progress.
if [[ -z "${DEPLOY_CLIENT_SELF:-}" ]]; then
    source_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    DEPLOY_CLIENT_SELF="$(mktemp -d "${TMPDIR:-/tmp}/deploy-client.XXXXXX")"
    export DEPLOY_CLIENT_SELF
    cp "$source_dir/deploy-client.sh" "$DEPLOY_CLIENT_SELF/"
    cp -r "$source_dir/deploy-client" "$DEPLOY_CLIENT_SELF/"
    exec bash "$DEPLOY_CLIENT_SELF/deploy-client.sh" "$@"
fi

LIB="$DEPLOY_CLIENT_SELF/deploy-client"
# shellcheck source=deploy-client/common.sh
source "$LIB/common.sh"
# shellcheck source=deploy-client/snapshot.sh
source "$LIB/snapshot.sh"
# shellcheck source=deploy-client/build.sh
source "$LIB/build.sh"
# shellcheck source=deploy-client/verify.sh
source "$LIB/verify.sh"
# shellcheck source=deploy-client/deploy.sh
source "$LIB/deploy.sh"

usage() {
    sed -n '2,/^set -euo/p' "$DEPLOY_CLIENT_SELF/deploy-client.sh" | sed '$d' | sed 's/^# \{0,1\}//' >&2
    exit 2
}

ROOT="$(pwd)"
CONFIG="$ROOT/.claude/deploy-client.json"
[[ -f "$CONFIG" ]] || fail_config "No $CONFIG: run from the root of a game that describes its targets there"
load_config

NO_TEST=0
NO_DEPLOY=0
TARGETS=()
while (($# > 0)); do
    case "$1" in
        --notest) NO_TEST=1 ;;
        --no-deploy) NO_DEPLOY=1 ;;
        -h | --help) usage ;;
        -*) echo "Unknown option: $1" >&2; usage ;;
        *)
            config_has_target "$1" || fail_config "Unknown target: $1 (targets: ${CONFIG_TARGETS[*]})"
            TARGETS+=("$1")
            ;;
    esac
    shift
done
((${#TARGETS[@]} > 0)) || TARGETS=("${CONFIG_TARGETS[@]}")

GAME="$(basename "$ROOT")"
RUN_START=$SECONDS
LOG_DIR="${DEPLOY_LOG_DIR:-/tmp/$GAME-deploy-client/$(date +%Y%m%d-%H%M%S)}"
mkdir -p "$LOG_DIR" /tmp/claude-1000
WORK="$(mktemp -d "${DEPLOY_COPY_DIR:-/var/tmp}/$GAME-deploy-client.XXXXXX")"
SNAP="$WORK/snapshot"
STATE="$WORK/state"
mkdir -p "$STATE"
# Every process of a build of this run carries "<TESTS_MARKER>/<target>"; the tests carry TESTS_MARKER.
BUILD_MARKER_PREFIX="DEPLOY_CLIENT_BUILD=$WORK"
TESTS_MARKER="DEPLOY_CLIENT_TESTS=$WORK"

TESTS_PID=""
BUILD_PIDS=()
SAMPLER_PID=""

cleanup() {
    local code=$?
    trap - EXIT INT TERM HUP
    stop_builds
    stop_tests
    if [[ -n "$SAMPLER_PID" ]]; then
        kill "$SAMPLER_PID" 2>/dev/null || true
        wait "$SAMPLER_PID" 2>/dev/null || true
    fi
    if [[ "${KEEP_COPY:-0}" == 1 ]]; then
        echo "Copies kept: $WORK"
    else
        rm -rf "$WORK"
        echo "Copies removed: $WORK"
    fi
    rm -rf "$DEPLOY_CLIENT_SELF"
    exit "$code"
}
trap cleanup EXIT
trap 'exit 129' HUP
trap 'exit 130' INT
trap 'exit 143' TERM

echo "deploy-client: ${TARGETS[*]} of $ROOT (tests: $([[ $NO_TEST == 1 ]] && echo skipped || echo yes), deploy: $([[ $NO_DEPLOY == 1 ]] && echo no || echo yes))"
echo "Logs: $LOG_DIR, copies: $WORK"

start_memory_sampler

# --- 1. Snapshot -----------------------------------------------------------------------------------------------------

stage_start=$SECONDS
take_snapshot >"$LOG_DIR/snapshot.log" 2>&1 || { tail -20 "$LOG_DIR/snapshot.log" >&2; record snapshot FAIL $((SECONDS - stage_start)) "see $LOG_DIR/snapshot.log"; print_summary; exit 1; }
if ! same_files_message="$(check_same_files)"; then
    record snapshot FAIL $((SECONDS - stage_start)) "$same_files_message"
    print_summary
    exit 1
fi
UNITY="$(find_unity "$SNAP/client")"
for target in "${TARGETS[@]}"; do
    profile="$(target_value "$target" profile)"
    [[ -f "$SNAP/client/$profile" ]] || fail_config "Build profile of $target not in the snapshot: client/$profile"
done
record snapshot PASS $((SECONDS - stage_start)) "$(du -sh --apparent-size "$SNAP" 2>/dev/null | cut -f1) apparent, Unity $(basename "$(dirname "$(dirname "$UNITY")")")"
echo "Snapshot ready in $((SECONDS - stage_start)) s"

# --- 2. Tests and builds at once -------------------------------------------------------------------------------------

stage_start=$SECONDS
for target in "${TARGETS[@]}"; do
    make_target_copy "$target" >>"$LOG_DIR/snapshot.log" 2>&1
done
record copies PASS $((SECONDS - stage_start)) "${#TARGETS[@]} copies of the snapshot"
echo "Copies ready in $((SECONDS - stage_start)) s"

TESTS_START=$SECONDS
if [[ "$NO_TEST" == 0 ]]; then
    tests_script="$(config_value tests)"
    (cd "$SNAP" && exec env "$TESTS_MARKER" TEST_LOG_DIR="$LOG_DIR/tests" "$SNAP/$tests_script") >"$LOG_DIR/tests.log" 2>&1 &
    TESTS_PID=$!
    echo "Tests started: $tests_script (log: $LOG_DIR/tests.log)"
fi

for target in "${TARGETS[@]}"; do
    build_target "$target" >"$LOG_DIR/build-$target.out" 2>&1 &
    BUILD_PIDS+=($!)
done

TESTS_CODE=""
pending=("${BUILD_PIDS[@]}")
[[ -n "$TESTS_PID" ]] && pending+=("$TESTS_PID")
while ((${#pending[@]} > 0)); do
    done_pid=""
    code=0
    wait -n -p done_pid "${pending[@]}" || code=$?
    remaining=()
    for pid in "${pending[@]}"; do
        [[ "$pid" == "$done_pid" ]] || remaining+=("$pid")
    done
    pending=("${remaining[@]+"${remaining[@]}"}")
    if [[ "$done_pid" == "$TESTS_PID" ]]; then
        TESTS_PID=""
        TESTS_CODE=$code
        echo "Tests finished in $((SECONDS - TESTS_START)) s, exit $code"
        if ((code != 0)); then
            echo "Tests failed: the builds of this run are stopped, nothing is uploaded" >&2
            stop_builds
            break
        fi
    else
        report_build_line "$done_pid"
    fi
done
((${#pending[@]} > 0)) || BUILD_PIDS=()

if [[ "$NO_TEST" == 1 ]]; then
    record tests SKIP 0 "--notest"
else
    record_tests "$TESTS_CODE" $((SECONDS - TESTS_START))
fi
for target in "${TARGETS[@]}"; do
    record_build "$target"
done

if [[ "$NO_TEST" == 0 && "$TESTS_CODE" != 0 ]]; then
    print_tests_failure
    print_summary
    exit 1
fi
if any_failed; then
    print_summary
    exit 1
fi

# --- 4. Deploy -------------------------------------------------------------------------------------------------------

if [[ "$NO_DEPLOY" == 1 ]]; then
    record deploy SKIP 0 "--no-deploy"
else
    for target in "${TARGETS[@]}"; do
        if ! deploy_target "$target"; then
            print_summary
            exit 1
        fi
    done
fi

print_summary
any_failed && exit 1
exit 0
