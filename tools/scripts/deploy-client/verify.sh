# The checks of one built target (sourced by deploy-client.sh). Whether the build is already live is checked by the
# deploy script of the game (it refuses a build-guid that is live on the target).

# "<target>" "<copy>" "<output>" "<build log>" "<start epoch>". Prints one line per failed check (or a short PASS line)
# and fails on any.
verify_target() {
    local target="$1" copy="$2" output="$3" log="$4" started="$5" failed=() guid kind files file text catalog backup
    local duplicates stripped

    guid="$(cat "$output/build-guid.txt" 2>/dev/null || true)"
    [[ -n "$guid" ]] || failed+=("no build-guid.txt")

    # Exactly one fresh payload of each kind: an older or a second one is a leftover of another build.
    for kind in '*.wasm.br' '*.data.br' '*.framework.js.br' '*.loader.js'; do
        mapfile -t files < <(find "$output/Build" -maxdepth 1 -name "$kind" 2>/dev/null)
        if ((${#files[@]} != 1)); then
            failed+=("Build/$kind: ${#files[@]} files, expected 1")
        elif (($(stat -c %Y "${files[0]}") < started)); then
            failed+=("Build/$kind older than the build")
        fi
    done

    # Addressables built for the active target: a catalog that points at Standalone bundles ships without them.
    catalog="$output/StreamingAssets/aa/catalog.bin"
    if [[ ! -f "$catalog" ]]; then
        failed+=("no StreamingAssets/aa/catalog.bin")
    elif grep -aq "StandaloneLinux64\|StandaloneWindows\|StandaloneOSX" "$catalog"; then
        failed+=("catalog.bin references Standalone bundles")
    fi
    duplicates="$(find "$output/StreamingAssets/aa" -name '*.bundle' -printf '%f\n' 2>/dev/null \
        | sed -E 's/_[0-9a-f]{32}\.bundle$//' | sort | uniq -d | head -5 | tr '\n' ' ')"
    [[ -z "$duplicates" ]] || failed+=("bundles built twice: $duplicates")

    # The stripping pass ran on this build: its backup folder (made for every build, stripped or not) holds the info and
    # the original wasm only after a strip.
    backup="$copy/client/Library/com.unity.web.stripping-tool/$(basename "$output")-$guid"
    [[ -f "$backup/stripping_info.json" ]] || failed+=("no stripping_info.json in $backup")
    compgen -G "$backup/*.wasm.br.bak" >/dev/null || failed+=("no *.wasm.br.bak in $backup")
    ! grep -q "Submodule stripping failed" "$log" || failed+=("Submodule stripping failed in the log")
    stripped="$(jq -r '"\(.strippedSubmodules | length) submodules, \(.strippedSize) bytes"' "$backup/stripping_info.json" 2>/dev/null || echo "?")"

    [[ -f "$output/index.html" ]] || failed+=("no index.html")
    while IFS= read -r text; do
        [[ -n "$text" ]] || continue
        grep -qF -- "$text" "$output/index.html" 2>/dev/null || failed+=("index.html lacks $text")
    done < <(target_list "$target" indexContains)
    while IFS= read -r text; do
        [[ -n "$text" ]] || continue
        ! grep -qF -- "$text" "$output/index.html" 2>/dev/null || failed+=("index.html holds $text")
    done < <(target_list "$target" indexExcludes)
    while IFS= read -r file; do
        [[ -n "$file" ]] || continue
        [[ -e "$output/$file" ]] || failed+=("no $file")
    done < <(target_list "$target" files)

    if ((${#failed[@]} > 0)); then
        printf '%s;\n' "${failed[@]}"
        return 1
    fi
    echo "guid $guid, payloads fresh, catalog WebGL, stripped ($(basename "$backup"), $stripped), index.html and files ok"
}
