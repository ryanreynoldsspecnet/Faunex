#!/usr/bin/env bash
set -euo pipefail
umask 077

revision=${1:-}
[[ "$revision" =~ ^[0-9a-f]{40}$ ]] || { echo 'A full validated commit SHA is required.' >&2; exit 1; }
cd "${FAUNEX_DEPLOY_ROOT:-/home/ubuntu/faunex}"
for command in git docker flock curl; do command -v "$command" >/dev/null; done
exec 9>.faunex-deploy.lock
flock -w 900 9

git diff --quiet
git diff --cached --quiet
git fetch origin master
[[ $(git rev-parse refs/remotes/origin/master) == "$revision" ]] || {
    echo 'This run is stale: master no longer matches the validated revision. No release activated.' >&2; exit 1;
}
git switch master
git merge --ff-only "$revision"
[[ $(git rev-parse HEAD) == "$revision" ]] || { echo 'Local master did not fast-forward to the validated revision.' >&2; exit 1; }

export FAUNEX_RELEASE="$revision"
mkdir -p .faunex-ops
trap 'echo "Release failed. Inspect service health and the recovery runbook; no automatic database rollback was attempted." >&2; docker compose ps -a >&2 || true' ERR
docker compose config --quiet

for service in api web; do
    image="faunex-$service:$revision"
    if label=$(docker image inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$image" 2>/dev/null); then
        [[ "$label" == "$revision" ]] || { echo 'Existing release image has an unexpected revision label.' >&2; exit 1; }
    else
        # Tracked commit contents only: VPS .env and untracked build files never enter this context.
        git archive --format=tar "$revision" | docker build --build-arg "RELEASE_REVISION=$revision" \
            --tag "$image" --file "Faunex.${service^}/Dockerfile" -
    fi
done

[[ $(git rev-parse HEAD) == "$revision" ]]
git diff --quiet
git diff --cached --quiet
docker compose run --rm --no-deps --entrypoint caddy caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile
printf '%s\n' "$revision" > .faunex-ops/attempted-release
for service in api web; do
    docker inspect --format '{{.Image}}' "faunex-$service" > ".faunex-ops/pre-release-$service-image" 2>/dev/null || true
done
docker compose up -d --no-build --pull missing --remove-orphans --wait --wait-timeout 600
for service in api web; do
    actual=$(docker inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "faunex-$service")
    [[ "$actual" == "$revision" ]] || { echo 'A running container has the wrong revision.' >&2; exit 1; }
done
docker compose exec -T caddy caddy reload --force --config /etc/caddy/Caddyfile --adapter caddyfile
body=$(curl --fail --silent --show-error --max-time 10 --retry 10 --retry-delay 3 --retry-all-errors https://faunex.co.za/__ready)
[[ "$body" == "ready $revision" ]] || { echo 'Public HTTPS readiness did not match the validated revision.' >&2; exit 1; }

for service in api web; do docker tag "faunex-$service:$revision" "faunex-$service:current"; done
if [[ -f .faunex-ops/current-release ]]; then
    cp .faunex-ops/current-release .faunex-ops/previous-release
fi
printf '%s\n' "$revision" > .faunex-ops/current-release
echo "Release ready: $revision"
