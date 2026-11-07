# Configuration, summary, processes and memory of deploy-client.sh (sourced).

fail_config() {
    echo "$*" >&2
    exit 2
}

# --- Configuration (.claude/deploy-client.json) ----------------------------------------------------------------------

load_config() {
    jq -e . "$CONFIG" >/dev/null || fail_config "$CONFIG is not valid JSON"
    local key
    for key in tests deploy; do
        [[ "$(jq -r --arg k "$key" '.[$k] // empty | strings' "$CONFIG")" != "" ]] || fail_config "$CONFIG: \"$key\" is required"
    done
    mapfile -t CONFIG_TARGETS < <(jq -r '.targets[]?.name' "$CONFIG")
    ((${#CONFIG_TARGETS[@]} > 0)) || fail_config "$CONFIG: no targets"
    local target field
    for target in "${CONFIG_TARGETS[@]}"; do
        for field in profile output; do
            [[ -n "$(target_value "$target" "$field")" ]] || fail_config "$CONFIG: target $target has no \"$field\""
        done
        [[ "$(jq -r --arg n "$target" '.targets[] | select(.name == $n) | .deployArgs | type' "$CONFIG")" == array ]] \
            || fail_config "$CONFIG: target $target has no \"deployArgs\" array"
    done
}

config_has_target() {
    local target
    for target in "${CONFIG_TARGETS[@]}"; do
        [[ "$target" == "$1" ]] && return 0
    done
    return 1
}

config_value() {
    jq -r --arg k "$1" '.[$k]' "$CONFIG"
}

# One string field of a target: "<target>" "<field>".
target_value() {
    jq -r --arg n "$1" --arg f "$2" '.targets[] | select(.name == $n) | .[$f] // empty' "$CONFIG"
}

# One array field of a target, a line per item: "<target>" "<field>".
target_list() {
    jq -r --arg n "$1" --arg f "$2" '.targets[] | select(.name == $n) | .[$f] // [] | .[]' "$CONFIG"
}

# --- Summary ---------------------------------------------------------------------------------------------------------

# Rows: "<stage>|<PASS/FAIL/SKIP>|<seconds>|<details>"
SUMMARY=()

record() {
    SUMMARY+=("$1|$2|$3|$4")
}

# A hook script of the game (preflight, beforeUpload): "<stage>" "<script>", run in the root of the game, its output
# in <stage>.log and on the console; its last line is the detail of the summary. Uses stage_start of the caller.
run_hook() {
    local stage="$1" script="$2" log code=0 last
    log="$LOG_DIR/${stage// /-}.log"
    [[ -x "$ROOT/$script" ]] || fail_config "$CONFIG: $stage script $script is not an executable file"
    echo "$stage: $script (log: $log)"
    (cd "$ROOT" && exec "$ROOT/$script") 2>&1 | tee "$log" | sed "s/^/  [$stage] /" || code=$?
    last="$(grep -v '^[[:space:]]*$' "$log" | tail -1 || true)"
    if ((code != 0)); then
        record "$stage" FAIL $((SECONDS - stage_start)) "exit $code: $last; $log"
        return 1
    fi
    record "$stage" PASS $((SECONDS - stage_start)) "$last"
}

any_failed() {
    local row
    for row in "${SUMMARY[@]}"; do
        [[ "$(cut -d'|' -f2 <<<"$row")" == FAIL ]] && return 0
    done
    return 1
}

print_summary() {
    echo
    echo "==== deploy-client summary ===="
    {
        echo "stage|result|seconds|details"
        printf '%s\n' "${SUMMARY[@]}"
    } | column -t -s'|'
    echo "Total: $((SECONDS - RUN_START)) s; memory: $(memory_report)"
    echo "Logs: $LOG_DIR"
    if any_failed; then
        echo "DEPLOY-CLIENT FAILED"
    else
        echo "DEPLOY-CLIENT PASSED"
    fi
}

# --- Tests -----------------------------------------------------------------------------------------------------------

record_tests() {
    local code="$1" seconds="$2" result=PASS details
    [[ "$code" == 0 ]] || result=FAIL
    details="$(grep -E '^(ALL TESTS PASSED|TESTS FAILED)' "$LOG_DIR/tests.log" | tail -1 || true)"
    record tests "$result" "$seconds" "exit ${code:-killed}${details:+, $details}; $LOG_DIR/tests.log"
    if [[ "$result" == PASS ]]; then
        sed -n '/^==== Summary ====/,$p' "$LOG_DIR/tests.log"
    fi
}

# The end of a failed test run: the failed tests of every step, the summary table and the log folder.
print_tests_failure() {
    echo
    echo "==== Failed tests ===="
    awk '/^Failed tests:/ { show = 1 } show && /^$/ { show = 0 } show' "$LOG_DIR/tests.log"
    sed -n '/^==== Summary ====/,$p' "$LOG_DIR/tests.log"
}

stop_tests() {
    [[ -n "$TESTS_PID" ]] || return 0
    # run-tests.sh kills its own Unity on TERM; what outlives it carries the marker.
    kill -TERM "$TESTS_PID" 2>/dev/null || true
    local i
    for ((i = 0; i < 60; i++)); do
        kill -0 "$TESTS_PID" 2>/dev/null || break
        sleep 1
    done
    wait "$TESTS_PID" 2>/dev/null || true
    TESTS_PID=""
    kill_markers "$TESTS_MARKER" || true
}

# --- Processes -------------------------------------------------------------------------------------------------------

# The pids of the processes whose environment holds one of these "NAME=value" entries; Unity.Licensing.Client is left
# alone: it outlives the editor that started it and serves every editor of the user.
marked_pids() {
    local patterns=() marker pid
    for marker in "$@"; do
        patterns+=(-e "$marker")
    done
    for pid in /proc/[0-9]*; do
        pid="${pid#/proc/}"
        if grep -qzxF "${patterns[@]}" "/proc/$pid/environ" 2>/dev/null \
            && [[ "$(cat "/proc/$pid/comm" 2>/dev/null)" != Unity.Licensing* ]]; then
            echo "$pid"
        fi
    done
}

# Kills the marked processes; fails when one survives SIGKILL. Unity calls setsid, so a process group does not reach
# its workers.
kill_markers() {
    local attempt found
    for attempt in 1 2 3 4 5 6 7 8 9 10; do
        mapfile -t found < <(marked_pids "$@")
        ((${#found[@]} == 0)) && return 0
        kill -KILL "${found[@]}" 2>/dev/null || true
        sleep 1
    done
    echo "Processes survived SIGKILL: ${found[*]} ($*)" >&2
    return 1
}

find_unity() {
    if [[ -n "${UNITY_EDITOR:-}" ]]; then
        echo "$UNITY_EDITOR"
        return
    fi
    local version candidate
    version="$(sed -n 's/^m_EditorVersion: //p' "$1/ProjectSettings/ProjectVersion.txt")"
    for candidate in "/tools/unity/$version/Editor/Unity" "$HOME/Unity/Hub/Editor/$version/Editor/Unity"; do
        if [[ -x "$candidate" ]]; then
            echo "$candidate"
            return
        fi
    done
    fail_config "Unity $version not found (set UNITY_EDITOR)"
}

# --- Memory ----------------------------------------------------------------------------------------------------------

mem_available_kb() {
    sed -n 's/^MemAvailable: *\([0-9]*\) kB/\1/p' /proc/meminfo
}

# Every 5 s: the lowest MemAvailable of the run and the peak resident memory of each build (the sum over its marked
# processes), into $STATE/memory.*.
start_memory_sampler() {
    (
        local min_kb target kb peak
        min_kb="$(mem_available_kb)"
        while true; do
            kb="$(mem_available_kb)"
            ((kb < min_kb)) && min_kb=$kb
            echo "$min_kb" >"$STATE/memory.min_available"
            for target in "${TARGETS[@]}"; do
                kb="$(marked_pids "$BUILD_MARKER_PREFIX/$target" | while read -r pid; do
                    sed -n 's/^VmRSS:[[:space:]]*\([0-9]*\) kB/\1/p' "/proc/$pid/status" 2>/dev/null
                done | awk '{ s += $1 } END { print s + 0 }')"
                peak="$(cat "$STATE/memory.$target" 2>/dev/null || echo 0)"
                ((kb > peak)) && echo "$kb" >"$STATE/memory.$target"
            done
            sleep 5
        done
    ) 9>&- &
    SAMPLER_PID=$!
}

memory_report() {
    local total min parts=() target
    total="$(sed -n 's/^MemTotal: *\([0-9]*\) kB/\1/p' /proc/meminfo)"
    min="$(cat "$STATE/memory.min_available" 2>/dev/null || mem_available_kb)"
    parts+=("lowest MemAvailable $((min / 1024 / 1024)) GB of $((total / 1024 / 1024))")
    for target in "${TARGETS[@]}"; do
        if [[ -f "$STATE/memory.$target" ]]; then
            parts+=("peak RSS of build $target $(awk '{ printf "%.1f", $1 / 1048576 }' "$STATE/memory.$target") GB")
        fi
    done
    local IFS=';'
    echo "${parts[*]}" | sed 's/;/, /g'
}
