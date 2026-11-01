#!/usr/bin/env bash
# Candidates of a code audit: architecture, style, structure, bug signs. Read-only. Used by /code-audit and /plan-architecture.
# Usage: audit.sh <dir|file>...   (paths relative to the repo root or absolute)
# Prints candidates, not verdicts: every line is checked by a grep / a look before it goes into the plan.
set -uo pipefail
exec </dev/null   # a grep whose file list came out empty must end, not wait for stdin
SKILL_DIR="$(cd "$(dirname "$0")" && pwd)"
DEPS_CONFIG="$(git rev-parse --show-toplevel)/.claude/code-audit.json"   # read before AUDIT_ROOT: an old tree may not have it
cd "${AUDIT_ROOT:-$(git rev-parse --show-toplevel)}"   # AUDIT_ROOT: an exported old tree (git archive) to audit the "before" state

declare -a DIRS=()
for p in "$@"; do
    [[ -f "$p" ]] && p=$(dirname "$p")
    [[ -d "$p" ]] || { echo "no such path: $p" >&2; exit 1; }
    DIRS+=("$p")
done
mapfile -t DIRS < <(printf '%s\n' "${DIRS[@]}" | sort -u)
mapfile -t CS < <(find "${DIRS[@]}" -name '*.cs' -not -path '*/Generated/*' -not -name '*.g.cs' | sort -u)
ROOTS=(client/Assets shared backend)

echo "## Size (P11): files > 300 lines"
for f in "${CS[@]}"; do [[ "$f" == */Tests*/* || "$f" == */Catalogues/* ]] && continue; n=$(wc -l < "$f"); (( n > 300 )) && echo "$n $f"; done | sort -rn

echo "## Several top-level types in a file (P11; the pair IX + X and XExtensions are allowed)"
TOP='^    (\[[^]]*\] *)?(public |internal |private )?(sealed |static |abstract |readonly |partial |ref |unsafe )*(class|struct|interface|enum|record) +\w+'
top_types() { grep -oE "$TOP" "$1" | awk '{print $NF}'; }
for f in "${CS[@]}"; do
    mapfile -t ts < <(top_types "$f")
    (( ${#ts[@]} > 1 )) || continue
    main=$(basename "$f" .cs)
    extra=$(printf '%s\n' "${ts[@]}" | grep -vxE "I?$main|${main}Extensions|I$main\w*" | paste -sd' ')
    [[ -n "$extra" ]] && echo "${#ts[@]} $f: extra types: $extra"
done

echo "## Folders (P12): content, .meta without a file, temporary files"
for d in "${DIRS[@]}"; do
    echo "- $d: $(find "$d" -maxdepth 1 -mindepth 1 -not -name '*.meta' -printf '%f ' | head -c 400)"
    find "$d" -name '*.meta' | while read -r m; do [[ -e "${m%.meta}" ]] || echo "  orphan meta: $m"; done
    find "$d" -not -name '*.meta' | grep -iE '/(temp|tmp|test_|harness|backup|old|copy|misc|utils?|helpers?|new)[^/]*$|(_old|_copy|_backup|_tmp|_test|[0-9])\.[a-z]+$' | sed 's/^/  suspicious name: /'
done

echo "## Dead code candidates (P13): top-level types never named outside their declaration"
for f in "${CS[@]}"; do
    for t in $(top_types "$f" | grep -v 'Extensions$'); do
        n=$(grep -rhow --include='*.cs' "$t" "${ROOTS[@]}" 2>/dev/null | wc -l)
        (( n > 1 )) && continue
        guid=$(grep -m1 -oE '[0-9a-f]{32}' "$f.meta" 2>/dev/null)
        [[ -n "$guid" ]] && grep -rlq --include='*.prefab' --include='*.asset' --include='*.unity' "$guid" client/Assets 2>/dev/null && continue
        echo "$t ($f)"
    done
done

echo "## Dependency direction (P14): sibling features, own composer, utility -> feature"
printf '%s\n' "${CS[@]}" | python3 -I "$SKILL_DIR/deps.py" "$DEPS_CONFIG"

echo "## Polling (P2): Has(/Contains(/!= null inside Update/Tick"
for f in "${CS[@]}"; do
    awk -v F="$f" '/(void|bool) +(On)?(Update|LateUpdate|FixedUpdate|Tick|Run)\(/ {u=1; d=0} u { d += gsub(/{/,"{"); d -= gsub(/}/,"}"); if ($0 ~ /\.Has\(|\.Contains\(|IsActive\(/) print F":"NR": "$0; if (d<=0 && $0 ~ /}/) u=0 }' "$f"
done | sed 's/  */ /g' | head -40

echo "## Branches by kind / variant (P1, P9)"
grep -HnE 'switch *\(|case +[A-Z]\w+ +\w+:| is [A-Z]\w+ +\w+|if *\(.*(IsLocal|IsOwner|IsHost|IsRemote)\b' "${CS[@]}" | grep -v '/Editor/\|/Tools/\|/Tests/' | head -40

echo "## Views from code (P3)"
grep -HnE 'new GameObject\(|AddComponent<' "${CS[@]}" | grep -v '/Editor/\|/Tools/\|/Tests/'

echo "## Data by index / name, writers (P4, P5)"
grep -HnE '^ +(public |private |\[SerializeField\] *)*(readonly )?(int|string|byte|int\[\]|string\[\]) +_?\w*(Index|Indices|Name|Names|Slot)\b *[;=]' $(printf '%s\n' "${CS[@]}" | grep -E 'Options|Definition|Catalog') 2>/dev/null | head -20
grep -HnE 'class +\w*(Writer|Presets|Generator)\b|AssetDatabase\.(CreateAsset|SaveAssets)' "${CS[@]}" | head -10

echo "## Public mutable state (P6)"
grep -HnP 'public +[\w<>\[\],. ]+ +\w+ *\{ *get; *set; *\}|^        public +(?!const|static|readonly|event|override|abstract|virtual|ref|void|bool \w+\()[\w<>\[\]]+ +\w+ *(;|= )' "${CS[@]}" | head -20

echo "## Local / remote pairs in separate files (P13 duplicates): shared member names"
for f in "${CS[@]}"; do
    b=$(basename "$f" .cs)
    [[ "$b" == *Local* ]] || continue
    r="$(dirname "$f")/${b/Local/Remote}.cs"
    [[ -f "$r" ]] || continue
    common=$(comm -12 <(grep -oE '(void|bool|float|int|Vector2) +\w+\(' "$f" | sort -u) <(grep -oE '(void|bool|float|int|Vector2) +\w+\(' "$r" | sort -u) | paste -sd' ')
    [[ -n "$common" ]] && echo "$f ~ $r: $common"
done

echo "## Style (CODE_STYLE_FULL)"
echo "- braces on the same line:"
grep -HnE '^\s*(public|private|protected|internal|static|class|struct|interface|enum|if|else|for|foreach|while|switch|try|catch|finally)\b.*\{\s*$' "${CS[@]}" | grep -vE '\{ *get|=> *\{|= *new|\{\s*\}' | head -20
echo "- .Forget() instead of .NoAwait():"
grep -Hn '\.Forget()' "${CS[@]}" | grep -v 'AsyncExtensions.cs' | head -20
echo "- Async suffix:"
grep -HnE '(UniTask|Task)(<.*>)? +[A-Z]\w*Async\(' "${CS[@]}" | grep -vE '\boverride\b|OnActivateAsync|OnDeactivateAsync|OnInitializedAsync|OnParametersSetAsync|OnAfterRenderAsync|ExecuteAsync|StartAsync|StopAsync|DisposeAsync' | head -20
echo "- async void:"
grep -Hn 'async void' "${CS[@]}"
echo "- XML docs:"
grep -Hc '^\s*///' "${CS[@]}" | grep -v ':0$' | sort -t: -k2 -rn | head -10
echo "- public setters (all code, states and tests included):"
grep -Hn '{ *get; *set; *}' "${CS[@]}" | head -30

echo "## Errors (P8): every catch, empty ones first"
for f in "${CS[@]}"; do
    awk -v F="$f" '/^[ \t]*catch([ (]|$)/ { line=NR; head=$0; getline a; getline b; if (a ~ /^[ \t]*\{[ \t]*\}?[ \t]*$/ && (a ~ /\}/ || b ~ /^[ \t]*\}[ \t]*$/)) print "EMPTY " F ":" line ": " head }' "$f"
done
grep -HnE '^\s*catch\b' "${CS[@]}" | head -40

echo "## Bug signs: subscription without a lifetime as the first argument"
grep -HnE '\.(Advise|View|ListenClick)\( *(\(?\w*\)? *=>|On[A-Z]\w*\s*\))' "${CS[@]}" | head -30

echo "## Framework names a game (framework rule 1)"
GAME_NS=$(python3 -I -c 'import json, sys; print("|".join(json.load(open(sys.argv[1], encoding="utf-8"))["game_namespaces"]))' "$DEPS_CONFIG")
printf '%s\n' "${CS[@]}" | grep 'tools/framework/' | while read -r f; do grep -nHE "^\s*using ($GAME_NS)(\.|;)|namespace ($GAME_NS)(\.|\s|$)" "$f"; done
exit 0
