# The upload of one target (sourced by deploy-client.sh): the deploy script of the game from the root of the game, then
# its --list must name the deployed tag as the current release.

deploy_target() {
    local target="$1" script output log list_log start=$SECONDS args tag size
    script="$ROOT/$(config_value deploy)"
    output="$WORK/$target/$(target_value "$target" output)"
    log="$LOG_DIR/deploy-$target.log"
    list_log="$LOG_DIR/deploy-$target-list.log"
    mapfile -t args < <(target_list "$target" deployArgs)
    size="$(du -sh "$output" | cut -f1)"
    echo "Deploying $target from $output ($size): $script ${args[*]}"
    if ! (cd "$ROOT" && BUILD_DIR="$output" "$script" "${args[@]}") >"$log" 2>&1; then
        tail -15 "$log" >&2
        record "deploy $target" FAIL $((SECONDS - start)) "$log"
        return 1
    fi
    cat "$log"
    tag="$(sed -n 's/^Deployed .* \([^ ]*\)$/\1/p' "$log" | tail -1)"
    if [[ -z "$tag" ]]; then
        record "deploy $target" FAIL $((SECONDS - start)) "no 'Deployed ... <tag>' line in $log"
        return 1
    fi
    if ! (cd "$ROOT" && "$script" "${args[@]}" --list) >"$list_log" 2>&1; then
        record "deploy $target" FAIL $((SECONDS - start)) "--list failed after the upload of $tag: $list_log"
        return 1
    fi
    if [[ "$(grep -v '^[[:space:]]*$' "$list_log" | tail -1)" != *"$tag" ]]; then
        record "deploy $target" FAIL $((SECONDS - start)) "current is not $tag: $list_log"
        return 1
    fi
    record "deploy $target" PASS $((SECONDS - start)) "tag $tag, $size$(grep -m1 '^URL:' "$log" | sed 's/^URL:/,/')"
}
