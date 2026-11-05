#!/usr/bin/env bash
set -euo pipefail
script=$(cd "$(dirname "$0")/../.." && pwd)/deploy/release.sh
root=$(mktemp -d)
trap 'rm -rf "$root"' EXIT
mkdir -p "$root/bin"
export TEST_SHA=0123456789abcdef0123456789abcdef01234567
export PATH="$root/bin:$PATH"

cat > "$root/bin/git" <<'MOCK'
#!/usr/bin/env bash
set -eu
case "$*" in
  'diff --quiet') [[ "$TEST_CASE" != dirty ]] ;;
  'diff --cached --quiet'|'fetch origin master'|'switch master'|merge*) : ;;
  'rev-parse refs/remotes/origin/master') if [[ "$TEST_CASE" == stale ]]; then echo aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa; else echo "$TEST_SHA"; fi ;;
  'rev-parse HEAD') echo "$TEST_SHA" ;;
  archive*) echo tracked-archive ;;
  *) echo "Unexpected git call: $*" >&2; exit 1 ;;
esac
MOCK
cat > "$root/bin/docker" <<'MOCK'
#!/usr/bin/env bash
set -eu
printf '%s\n' "$*" >> "$FAUNEX_DEPLOY_ROOT/calls"
case "$*" in
  'image inspect'*) if [[ "$TEST_CASE" == label ]]; then echo wrong; else exit 1; fi ;;
  build*) cat >/dev/null; [[ "$TEST_CASE" != build ]] ;;
  'compose up'*) touch "$FAUNEX_DEPLOY_ROOT/activated"; [[ "$FAUNEX_RELEASE" == "$TEST_SHA" && "$TEST_CASE" != health ]] ;;
  inspect*) if [[ "$*" == *'.Image'* ]]; then echo sha256:previous; else echo "$TEST_SHA"; fi ;;
  'compose config --quiet'|'compose ps -a'|'compose run '*|'compose exec '*|tag*) : ;;
  *) echo "Unexpected docker call: $*" >&2; exit 1 ;;
esac
MOCK
cat > "$root/bin/curl" <<'MOCK'
#!/usr/bin/env bash
if [[ "$TEST_CASE" == smoke ]]; then echo 'ready wrong'; else echo "ready $TEST_SHA"; fi
MOCK
printf '#!/usr/bin/env bash\nexit 0\n' > "$root/bin/flock"
chmod +x "$root/bin/"*

for case_name in invalid stale dirty label build health smoke success; do
    export TEST_CASE="$case_name" FAUNEX_DEPLOY_ROOT="$root/$case_name"
    mkdir -p "$FAUNEX_DEPLOY_ROOT"
    revision="$TEST_SHA"; [[ "$case_name" != invalid ]] || revision='master;touch unsafe'
    if bash "$script" "$revision" > "$FAUNEX_DEPLOY_ROOT/output" 2>&1; then
        [[ "$case_name" == success ]]
        [[ $(<"$FAUNEX_DEPLOY_ROOT/.faunex-ops/current-release") == "$TEST_SHA" ]]
        grep -q -- "--wait --wait-timeout 600" "$FAUNEX_DEPLOY_ROOT/calls" || exit 1
    else
        [[ "$case_name" != success ]]
        [[ ! -f "$FAUNEX_DEPLOY_ROOT/.faunex-ops/current-release" ]]
        case "$case_name" in invalid|stale|dirty|label|build) [[ ! -f "$FAUNEX_DEPLOY_ROOT/activated" ]] ;; esac
    fi
    echo "Release guard passed: $case_name"
done
