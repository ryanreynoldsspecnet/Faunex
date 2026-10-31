#!/usr/bin/env bash
set -euo pipefail
[[ ${CI:-} == true && ${GITHUB_ACTIONS:-} == true ]] || { echo 'This disposable container test is for GitHub-hosted CI only.' >&2; exit 1; }
[[ ${GITHUB_SHA:-} =~ ^[0-9a-f]{40}$ ]] || exit 1
export FAUNEX_RELEASE="$GITHUB_SHA"
export POSTGRES_DB=faunex_readiness POSTGRES_USER=faunex_ci POSTGRES_PASSWORD=ci-only-readiness-password
export FAUNEX_CONNECTIONSTRING='Host=db;Database=faunex_readiness;Username=faunex_ci;Password=ci-only-readiness-password;Timeout=3;Command Timeout=3'
export FAUNEX_JWT_ISSUER=faunex-ci FAUNEX_JWT_AUDIENCE=faunex-ci
export FAUNEX_JWT_SIGNING_KEY=FAUNEX_CI_ONLY_READINESS_SIGNING_KEY_NOT_FOR_PRODUCTION_123456789
export FAUNEX_SUPERADMIN_PASSWORD='Readiness!CIOnly12345'
export FAUNEX_PUBLIC_BASE_URL=https://faunex.co.za
export FAUNEX_EMAIL_SMTP_HOST='' FAUNEX_EMAIL_SMTP_PORT=587 FAUNEX_EMAIL_SMTP_USERNAME='' FAUNEX_EMAIL_SMTP_PASSWORD=''
export FAUNEX_EMAIL_SMTP_ENABLE_SSL=true FAUNEX_EMAIL_FROM_EMAIL='' FAUNEX_EMAIL_FROM_NAME=Faunex
export FAUNEX_PAYFAST_SANDBOX_ENABLED=false FAUNEX_PAYMENTS_INGRESS_TOKEN=''
compose() { docker compose -p faunex-readiness "$@"; }
cleanup() { compose ps -a || true; compose logs --tail 40 faunex-api faunex-web || true; compose down -v --remove-orphans; }
trap cleanup EXIT

# Release the earlier scanner before starting this project's own scanner on a small runner.
docker rm -f faunex-test-clamav
dotnet build-server shutdown
compose config --quiet
for service in api web; do
    git archive --format=tar "$GITHUB_SHA" | docker build --build-arg "RELEASE_REVISION=$GITHUB_SHA" \
        --tag "faunex-$service:$GITHUB_SHA" --file "Faunex.${service^}/Dockerfile" -
done
compose up -d --no-build --wait --wait-timeout 600 faunex-web
probe() { compose exec -T faunex-web curl --fail --silent --show-error --max-time 12 -H 'Host: faunex.co.za' http://localhost:8080/__ready; }
[[ $(probe) == "ready $GITHUB_SHA" ]]

compose stop db
status=$(compose exec -T faunex-web curl --silent --max-time 12 -o /dev/null -w '%{http_code}' -H 'Host: faunex.co.za' http://localhost:8080/__ready)
[[ "$status" == 503 ]]
compose start db
compose up -d --no-build --wait --wait-timeout 180 faunex-web
[[ $(probe) == "ready $GITHUB_SHA" ]]
compose restart faunex-api faunex-web
compose up -d --no-build --wait --wait-timeout 180 faunex-web
[[ $(probe) == "ready $GITHUB_SHA" ]]
echo 'Isolated production-image readiness, database failure/recovery and restart checks passed.'
