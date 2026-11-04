# The snapshot of the game and the copies of the targets (sourced by deploy-client.sh).
#
# Everything is copied with `cp -a --reflink=always`: on btrfs a copy of the tree, Library included, takes seconds and
# almost no disk; on another filesystem it fails instead of copying gigabytes.
#
# Left out of every snapshot:
#   .git, .claude/worktrees        neither the tests nor the build read git; worktrees are other trees
#   client/Temp, client/Logs, client/obj
#                                  the lock file of the open editor, its logs, the IDE builds
#   client/Library/Bee/Player*     the DAG of the last player build holds the absolute output paths of the game
#                                  (builds/<target> of the game root): Bee deletes the outputs of the previous DAG when a
#                                  build writes elsewhere, the copy would delete the builds of the game
#   client/Library/com.unity.web.stripping-tool, client/Library/BuildHistory
#                                  stripping backups and reports of earlier builds: the checks look for the ones of
#                                  this build only
#   the outputs of all targets, and "exclude" of the config
# The framework (tools/framework or FRAMEWORK_SOURCE) leaves out .git and the Library, Temp, Logs of client/project.

SNAPSHOT_EXCLUDES=(
    .git
    .claude/worktrees
    tools/framework
    client/Temp
    client/Logs
    client/obj
    'client/Library/Bee/Player*'
    client/Library/com.unity.web.stripping-tool
    client/Library/BuildHistory
)
FRAMEWORK_EXCLUDES=(
    .git
    client/project/Library
    client/project/Temp
    client/project/Logs
    client/project/obj
)

# Copies the folder "<src>" into "<dst>" without the paths of FILTER (globs relative to the top folder), descending only
# into the folders that hold an excluded path. "<rel>" is the path of "<src>" from the top ("" at the top).
copy_filtered() {
    local src="$1" dst="$2" rel="$3" entry name path pattern skip descend
    mkdir -p "$dst"
    shopt -s dotglob nullglob
    for entry in "$src"/*; do
        name="$(basename "$entry")"
        path="${rel:+$rel/}$name"
        skip=0
        descend=0
        for pattern in "${FILTER[@]}"; do
            # shellcheck disable=SC2053 # the patterns are globs
            if [[ "$path" == $pattern ]]; then
                skip=1
                break
            fi
            if [[ "$pattern" == "$path/"* ]]; then
                descend=1
            fi
        done
        ((skip == 1)) && continue
        if ((descend == 1)) && [[ -d "$entry" && ! -L "$entry" ]]; then
            copy_filtered "$entry" "$dst/$name" "$path"
        else
            cp -a --reflink=always "$entry" "$dst/$name"
        fi
    done
    shopt -u dotglob nullglob
}

take_snapshot() {
    local framework="${FRAMEWORK_SOURCE:-$ROOT/tools/framework}" target FILTER
    [[ -d "$framework/client/core" ]] || { echo "Not a framework: $framework" >&2; return 1; }
    FILTER=("${SNAPSHOT_EXCLUDES[@]}")
    mapfile -t -O "${#FILTER[@]}" FILTER < <(jq -r '.exclude // [] | .[]' "$CONFIG")
    for target in "${CONFIG_TARGETS[@]}"; do
        FILTER+=("$(target_value "$target" output)")
    done
    echo "Snapshot of $ROOT -> $SNAP without: ${FILTER[*]}"
    copy_filtered "$ROOT" "$SNAP" ""
    FILTER=("${FRAMEWORK_EXCLUDES[@]}")
    echo "Framework $framework -> $SNAP/tools/framework without: ${FILTER[*]}"
    copy_filtered "$framework" "$SNAP/tools/framework" ""
    if [[ -n "${FRAMEWORK_SOURCE:-}" ]]; then
        echo "framework: $(git -C "$framework" rev-parse --short HEAD) + working tree of $framework"
    else
        echo "framework: working tree of $ROOT/tools/framework"
    fi
}

# The "same" pairs of the config, compared in the snapshot. Prints the differing ones and fails on any.
check_same_files() {
    local count pair_index first second failed=()
    count="$(jq '.same // [] | length' "$CONFIG")"
    for ((pair_index = 0; pair_index < count; pair_index++)); do
        first="$(jq -r --argjson i "$pair_index" '.same[$i][0]' "$CONFIG")"
        second="$(jq -r --argjson i "$pair_index" '.same[$i][1]' "$CONFIG")"
        cmp -s "$SNAP/$first" "$SNAP/$second" || failed+=("$first != $second")
    done
    if ((${#failed[@]} > 0)); then
        echo "files differ: ${failed[*]}"
        return 1
    fi
    echo "$count same pairs"
}

# The tree a target builds in: a reflink copy of the snapshot, taken before the tests write into it.
make_target_copy() {
    local target="$1"
    echo "Copy of $target: $WORK/$target"
    cp -a --reflink=always "$SNAP" "$WORK/$target"
}
