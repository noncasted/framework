# The batchmode builds of deploy-client.sh (sourced): slots, one Unity per target, its state files.
#
# Slots: at most UNITY_BUILD_SLOTS builds of all runs on the machine, slot <i> is /tmp/claude-1000/unity-build.slot<i>.lock
# held with flock -n by the build for its Unity run. The test runs have slots of their own (run-tests.sh of the game),
# the old shared lock unity-batchmode.lock is not taken. A slot is taken only with UNITY_BUILD_MIN_FREE_GB available,
# plus as much for every build that took a slot in the last 60 s (it has not grown yet; the stamp is
# unity-build.slot<i>.started). Unity does not inherit the descriptor of the slot.
#
# State of a target in $STATE: build.<target> "<PASS/FAIL>|<seconds>|<start epoch>|<end epoch>|<details>",
# verify.<target> "<PASS/FAIL>|<seconds>|<details>"; no file - the build was stopped.

BUILD_SLOT_DIR=/tmp/claude-1000

# Takes a slot on descriptor 9; fails after UNITY_BUILD_MEM_WAIT seconds of low memory with a free slot.
acquire_build_slot() {
    local slots="${UNITY_BUILD_SLOTS:-2}" min_gb="${UNITY_BUILD_MIN_FREE_GB:-10}" mem_wait="${UNITY_BUILD_MEM_WAIT:-900}"
    local low_seconds=0 said=$((SECONDS - 60)) avail_gb=0 need=0 i low stamp recent now
    while true; do
        low=0
        # One choice at a time across the runs, so two builds do not count the same free memory.
        exec 7>"$BUILD_SLOT_DIR/unity-build.acquire.lock"
        flock 7
        for ((i = slots - 1; i >= 0; i--)); do
            exec 9>"$BUILD_SLOT_DIR/unity-build.slot$i.lock"
            if ! flock -n 9; then
                exec 9>&-
                continue
            fi
            now="$(date +%s)"
            recent=0
            for stamp in "$BUILD_SLOT_DIR"/unity-build.slot*.started; do
                [[ -f "$stamp" && "$stamp" != *".slot$i.started" ]] || continue
                (($(stat -c %Y "$stamp") > now - 60)) && recent=$((recent + 1))
            done
            need=$((min_gb * (1 + recent)))
            avail_gb=$(($(mem_available_kb) / 1024 / 1024))
            if ((avail_gb >= need)); then
                touch "$BUILD_SLOT_DIR/unity-build.slot$i.started"
                exec 7>&-
                echo "Slot $i of $slots taken, $avail_gb GB available, $need needed"
                return 0
            fi
            exec 9>&-
            low=1
            break
        done
        exec 7>&-
        if ((low == 1)); then
            if ((low_seconds >= mem_wait)); then
                echo "Only $avail_gb GB of memory available for $mem_wait s, a build needs $need (UNITY_BUILD_MIN_FREE_GB)." >&2
                return 1
            fi
            low_seconds=$((low_seconds + 5))
        fi
        if ((SECONDS - said >= 60)); then
            said=$SECONDS
            if ((low == 1)); then
                echo "Waiting for memory: $avail_gb GB available, $need needed"
            else
                echo "Waiting for a free build slot of $slots"
            fi
        fi
        sleep 5
    done
}

# One target, in the background: slot, Unity, the checks. Never fails, the outcome is in the state files.
build_target() {
    local target="$1" copy="$WORK/$1" log="$LOG_DIR/build-$1.log" limit="${UNITY_BUILD_TIMEOUT:-1800}"
    local marker="$BUILD_MARKER_PREFIX/$1" profile output start end code=0 reason started
    profile="$(target_value "$target" profile)"
    output="$copy/$(target_value "$target" output)"
    start=$SECONDS
    if ! acquire_build_slot; then
        echo "FAIL|$((SECONDS - start))|-|-|low memory" >"$STATE/build.$target"
        return 0
    fi
    rm -rf "$output" "$copy/client/Temp"
    started="$(date +%s)"
    echo "Building $target: profile $profile -> $output (limit $limit s, log $log)"
    # 9>&-: no process of Unity holds the slot. In the background: the subshell can be stopped while Unity runs.
    (env "$marker" timeout --signal=KILL "$limit" "$UNITY" -batchmode -quit -projectPath "$copy/client" \
        -buildTarget WebGL -activeBuildProfile "$profile" -executeMethod Internal.WebGlBuild.Build \
        -buildOutput "$output" -logFile "$log"; exit $?) >/dev/null 2>&1 9>&- &
    wait $! || code=$?
    end="$(date +%s)"
    kill_markers "$marker" || reason="; processes survived SIGKILL"
    exec 9>&-

    if ((code != 0)); then
        if ((code == 137 && end - started >= limit)); then
            reason="limit $limit s$reason"
        elif grep -q "Caught fatal signal" "$log" 2>/dev/null; then
            reason="crash: $(grep -m1 -o "Caught fatal signal - signo:[0-9]*" "$log")$reason"
        else
            reason="exit $code$reason"
        fi
        grep -n -E "emcc: error|Tundra build failed|BuildFailedException|Submodule stripping failed|Caught fatal signal|Exception:|error CS" "$log" 2>/dev/null | tail -20 || true
        echo "FAIL|$((end - started))|$started|$end|$reason, $log" >"$STATE/build.$target"
        return 0
    fi
    echo "PASS|$((end - started))|$started|$end|$(date -d "@$started" +%T)-$(date -d "@$end" +%T), $(du -sh "$output" | cut -f1)" >"$STATE/build.$target"

    local verify_start=$SECONDS details result=PASS
    details="$(verify_target "$target" "$copy" "$output" "$log" "$started" 2>&1)" || result=FAIL
    echo "$details"
    echo "$result|$((SECONDS - verify_start))|$(tr '\n' ' ' <<<"$details" | sed 's/ *$//')" >"$STATE/verify.$target"
}

# Stops the build subshells of this run and every process of their Unity.
stop_builds() {
    local pid target
    for pid in "${BUILD_PIDS[@]+"${BUILD_PIDS[@]}"}"; do
        kill -TERM "$pid" 2>/dev/null || true
    done
    for pid in "${BUILD_PIDS[@]+"${BUILD_PIDS[@]}"}"; do
        wait "$pid" 2>/dev/null || true
    done
    BUILD_PIDS=()
    for target in "${TARGETS[@]}"; do
        kill_markers "$BUILD_MARKER_PREFIX/$target" || true
    done
}

# A line when the build subshell "<pid>" has ended.
report_build_line() {
    local i
    for i in "${!BUILD_PIDS[@]}"; do
        if [[ "${BUILD_PIDS[$i]}" == "$1" ]]; then
            local target="${TARGETS[$i]}"
            echo "Build $target: $(cut -d'|' -f1,2,5 "$STATE/build.$target" 2>/dev/null || echo stopped);" \
                "checks: $(cut -d'|' -f1,3 "$STATE/verify.$target" 2>/dev/null || echo none)"
        fi
    done
}

record_build() {
    local target="$1" row
    if [[ ! -f "$STATE/build.$target" ]]; then
        record "build $target" FAIL - "stopped"
        return
    fi
    row="$(cat "$STATE/build.$target")"
    record "build $target" "$(cut -d'|' -f1 <<<"$row")" "$(cut -d'|' -f2 <<<"$row")" "$(cut -d'|' -f5 <<<"$row")"
    if [[ -f "$STATE/verify.$target" ]]; then
        row="$(cat "$STATE/verify.$target")"
        record "checks $target" "$(cut -d'|' -f1 <<<"$row")" "$(cut -d'|' -f2 <<<"$row")" "$(cut -d'|' -f3- <<<"$row")"
    fi
}
