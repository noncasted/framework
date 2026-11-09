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

    # The content directory of this build (Content of ProjectSettings/Framework.json, built into Library/Content/<name>):
    # its index is inside the player (Resources, in the .data file) and, unless it is preloaded into the .data file too,
    # its files are next to the player in StreamingAssets/Content/<name> as <file>.br (the build is brotli), each one the
    # file of Library/Content/<name> once decompressed.
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
    local client="$1" output="$2" started="$3" name preloaded built hash shipped data expected actual broken raw bytes
    local -n failures="$4"
    CONTENT_SUMMARY="content ?"

    name="$(jq -r '.Content.Name // empty' "$client/ProjectSettings/Framework.json" 2>/dev/null)"
    if [[ -z "$name" ]]; then
        failures+=("no Content.Name in ProjectSettings/Framework.json")
        return
    fi
    preloaded="$(jq -r '.Content.IsPreloaded // false' "$client/ProjectSettings/Framework.json")"
    built="$client/Library/Content/$name"

    hash="$(cat "$built/BuildManifestHash.txt" 2>/dev/null || true)"
    if [[ -z "$hash" ]]; then
        failures+=("no content directory $name in Library/Content")
        return
    elif (($(stat -c %Y "$built/BuildManifestHash.txt") < started)); then
        failures+=("content directory $name older than the build")
    fi

    if ! command -v brotli >/dev/null; then
        failures+=("brotli is not installed: the content directory is not checked")
        return
    fi

    # The index carries the hash of the manifest it was written from. grep reads the whole stream (-c, not -q): an early
    # exit would kill brotli with SIGPIPE, and pipefail of the caller fails the pipeline although the hash is there.
    data="$(find "$output/Build" -maxdepth 1 -name '*.data.br' 2>/dev/null | head -1)"
    if [[ -z "$data" ]]; then
        : # reported by the payload check
    elif ! brotli -dc "$data" | grep -acF -- "$hash" >/dev/null; then
        failures+=("the .data file has no index of the content directory $name ($hash)")
    fi

    if [[ "$preloaded" == true ]]; then
        CONTENT_SUMMARY="content $name preloaded"
        return
    fi

    shipped="$output/StreamingAssets/Content/$name"
    if [[ ! -d "$shipped" ]]; then
        failures+=("no StreamingAssets/Content/$name")
        return
    fi
    # Exactly the files of the directory, BuildManifestHash.txt aside (the player writes it from its index), the manifest
    # among them; then every one decompressed against the built file, in parallel.
    expected="$(cd "$built" && find . -type f ! -name BuildManifestHash.txt -printf '%P.br\n' | sort)"
    actual="$(cd "$shipped" && find . -type f -printf '%P\n' | sort)"
    if [[ "$expected" != "$actual" ]]; then
        failures+=("StreamingAssets/Content/$name is not the brotli files of Library/Content/$name (an uncompressed or a stale directory)")
        return
    fi
    [[ -f "$shipped/$hash.json.br" ]] || failures+=("StreamingAssets/Content/$name has no manifest $hash.json.br")
    broken="$(cd "$built" && find . -type f ! -name BuildManifestHash.txt -printf '%P\0' |
        xargs -0 -r -n 64 -P "$(nproc)" sh -c 'for f; do brotli -dc "$0/$f.br" 2>/dev/null | cmp -s - "$f" || echo "$f"; done' "$(realpath "$shipped")" |
        wc -l)"
    ((broken == 0)) || failures+=("StreamingAssets/Content/$name: $broken files do not decompress into the files of Library/Content/$name")
    raw="$(find "$built" -type f ! -name BuildManifestHash.txt -printf '%s\n' | awk '{ s += $1 } END { print s + 0 }')"
    bytes="$(find "$shipped" -type f -printf '%s\n' | awk '{ s += $1 } END { print s + 0 }')"
    CONTENT_SUMMARY="content $name ($(wc -l <<<"$actual") files, $bytes bytes brotli of $raw raw)"
}
