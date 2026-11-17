# The checks of one built target (sourced by deploy-client.sh). Whether the build is already live is checked by the
# deploy script of the game (it refuses a build-guid that is live on the target).

# "<target>" "<copy>" "<output>" "<build log>" "<start epoch>". Prints one line per failed check (or a short PASS line)
# and fails on any.
verify_target() {
    local target="$1" copy="$2" output="$3" log="$4" started="$5" failed=() guid kind files file text backup content
    local stripped

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

    # The content bundles of this build (the ContentBundle assets, shipped into Library/ContentShipped): each file in
    # StreamingAssets/Content exactly as shipped, or preloaded into the .data file, which also holds the catalog naming them.
    content_check "$copy/client" "$output" "$started" failed
    content="$CONTENT_SUMMARY"

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
    echo "guid $guid, payloads fresh, $content, stripped ($(basename "$backup"), $stripped), index.html and files ok"
}

# "<client folder>" "<output>" "<start epoch>" "<name of the failures array>". Appends its failures to that array and
# leaves a short summary in CONTENT_SUMMARY.
content_check() {
    local client="$1" output="$2" started="$3" shipped streaming data text file name bundles=0 preloaded=0 bytes=0 actual expected
    local -n failures="$4"
    CONTENT_SUMMARY="content ?"

    shipped="$client/Library/ContentShipped"
    if [[ ! -d "$shipped" ]] || [[ -z "$(find "$shipped" -type f -newermt "@$started" -print -quit)" ]]; then
        failures+=("no content bundles of this build in Library/ContentShipped")
        return
    fi

    if ! command -v brotli >/dev/null; then
        failures+=("brotli is not installed: the content bundles are not checked")
        return
    fi

    # The .data file once, unpacked: the catalog (Resources) names every file, the preloaded ones are in it. Resources
    # are in data.unity3d, a UnityFS archive of LZ4 blocks inside the .data: webdata.py unpacks it for the search.
    data="$(find "$output/Build" -maxdepth 1 -name '*.data.br' 2>/dev/null | head -1)"
    [[ -n "$data" ]] || return # reported by the payload check
    text="$(mktemp)"
    brotli -dc "$data" > "$text.data"
    if ! python3 -I "$LIB/webdata.py" "$text.data" "$text"; then
        failures+=("the .data file could not be unpacked by webdata.py")
        rm -f "$text" "$text.data"
        return
    fi
    rm -f "$text.data"

    streaming="$output/StreamingAssets/Content"
    for file in "$shipped"/*; do
        name="$(basename "$file")"
        bundles=$((bundles + 1))
        grep -aqF -- "${name%.gz}" "$text" || failures+=("the catalog in the .data file does not name $name")

        if [[ -f "$streaming/$name" ]]; then
            cmp -s "$file" "$streaming/$name" || failures+=("StreamingAssets/Content/$name is not the one of Library/ContentShipped")
            bytes=$((bytes + $(stat -c %s "$file")))
        else
            preloaded=$((preloaded + 1))
        fi

        if [[ "$name" == *.gz ]]; then
            [[ "$(gzip -dc "$file" | head -c 7)" == UnityFS ]] || failures+=("$name is no gzip of a bundle")
        else
            [[ "$(head -c 7 "$file")" == UnityFS ]] || failures+=("$name is no bundle (compressed again?)")
        fi
    done
    rm -f "$text"

    # Nothing of another build next to them.
    expected="$(cd "$shipped" && find . -type f -printf '%P\n' | sort)"
    actual="$( [[ -d "$streaming" ]] && cd "$streaming" && find . -type f -printf '%P\n' | sort)"
    [[ -z "$(comm -13 <(echo "$expected") <(echo "$actual"))" ]] || failures+=("StreamingAssets/Content has files of no bundle of this build")

    CONTENT_SUMMARY="content $bundles bundle files ($preloaded preloaded, $bytes bytes shipped)"
}
