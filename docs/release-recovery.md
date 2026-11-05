# Release And Recovery Operations

Section 7A1 adds exact-source-revision deployment and readiness. It does not create an off-server backup, prove a restore, enable live payments or perform an automatic database rollback. Production restart and restore drills require separate approval. Keep the existing merge-to-master release workflow; do not run release scripts against the VPS just to test this PR.

## Normal Deployment

After validation, the master push job sends its full commit SHA and the checked-out `deploy/release.sh` to the VPS. GitHub serializes production jobs; the script also holds a VPS `flock` while selecting/building/activating the release. It rejects dirty tracked files, stale runs and a local master that cannot fast-forward exactly. It builds API/Web from `git archive` of the selected commit, excluding untracked and ignored VPS files such as `.env`, with revision-tagged images and revision labels. This pins source, not byte-for-byte reproducibility: SDK/runtime and third-party image tags and package feeds remain external inputs. Shipping signed/prebuilt artifacts is a future improvement.

Images are `faunex-api:<full-sha>` and `faunex-web:<full-sha>`. Existing release tags with the expected revision label can be reused; unexpected labels fail closed. The script does not prune previous images or volumes. After successful readiness, `:current` aliases point to that release so ordinary Compose recreation without a build uses the last successful application images. Never use `docker compose up --build` or prune release images as routine production maintenance. Old pre-7A images may have the previous implicit Compose names; the first release records their immutable image IDs for investigation, not a promise that they support automated rollback.

The script validates Caddy, waits up to 600 seconds for Compose health, verifies running API/Web revision labels, forces Caddy reload and checks `https://faunex.co.za/__ready` for the exact release marker with normal certificate validation. Only then does it update `.faunex-ops/current-release`; `.faunex-ops/previous-release` retains the previous successful marker, when one existed. Attempted revision and pre-change image IDs are recorded separately. These files contain release metadata, not credentials, and are excluded from Git/Docker context. A failed attempt may leave some services changed and the old success marker intact: markers are not a substitute for inspecting running containers.

No new VPS secret is required. Before the first merge, verify that the VPS has a current Docker Compose plugin supporting `up --wait`, plus `git`, `curl` and `flock`. Do not paste rendered Compose configuration or `.env` into chat. The read-only checks are:

```sh
cd /home/ubuntu/faunex
docker compose version
docker compose up --help | grep -E -- '--wait|--wait-timeout'
command -v git curl flock
git status --short
df -h . /var/lib/docker
```

Cold ClamAV startup can take several minutes; 4 GB scanner memory and the existing resource limits remain unchanged. API/Web/database now restart unless stopped. A health failure alone does not make Docker restart a running unhealthy process. Dependency health conditions gate Compose startup, not continuous ordering after an arbitrary host outage. The API can exit on startup database failure and retry under its restart policy; Web readiness remains 503 until API/database connectivity recovers.

## Readiness Meaning

- Private API `/api/health/ready` queries both application and identity schema tables. Successful startup migrations/seeding happen before API listening. Failures return generic `not ready` / 503 without database details.
- Web `/__ready` requires the API to return a ready marker matching the Web image's revision. It returns no-store plaintext, not credentials, users, table names or exception details. Root platform host probes avoid needing a tenant login; unknown tenant domains remain governed by the existing host gateway.
- Existing `/__ping` and `/api/health/ping` remain liveness only. Neither proves database readiness or the deployed revision.
- Compose separately checks PostgreSQL readiness and ClamAV PING. Public Web readiness does not claim scanner signature freshness, file/key writability, SMTP delivery, PayFast availability or business correctness. External alerting and those acceptance checks remain open. Do not call SMTP/PayFast for every health request.

After a merged deployment, inspect:

```sh
cd /home/ubuntu/faunex
docker compose ps -a
cat .faunex-ops/current-release
curl --fail --silent --show-error https://faunex.co.za/__ready
docker compose logs --since 10m --tail 80 faunex-api faunex-web
```

Review logs privately and redact before sharing. Compare the workflow SHA, current marker, ready marker and running image labels. A generic homepage 200 can hide a failed upgrade.

## Failed Release And Rollback

Do not retry a completed payment, delete volumes, remove key files, reset the database or blindly roll back code. No automatic rollback is implemented because API startup applies migrations and some changes may not be backwards compatible. Failure returns a failed Actions job and service status, without printing application secrets.

1. Record the failed run/SHA and inspect actual health, `.faunex-ops/attempted-release`, the successful marker and `.faunex-ops/pre-release-*-image`. Preserve these before another attempt, which can replace attempted-release evidence.
2. Decide whether to roll forward with a fix or restore previous application images. Review database migrations, API/Web contracts, Caddy/Compose changes and provider callbacks first. A previous image tag existing does not prove compatibility.
3. For a reviewed, compatible image rollback, use a trusted Compose file from the approved revision and set `FAUNEX_RELEASE` to that full SHA. Verify both retained image labels/IDs and use `--no-build --pull never`; do not rebuild an old image from changed feeds and describe it as the retained artifact. Keep the existing Compose project/volume identities. Approve downtime and test the exact procedure in isolation before production execution.
4. Recheck the matching public/API/Web ready markers and authenticated workflows. Preserve audit/payment history and late notification processing. Record the actual deployed rollback version; do not manually alter success markers to conceal a failed release.
5. If migrations are incompatible, use an approved recovery plan. Restoring an older database loses newer bids/orders/payments unless separately reconciled; incoming provider notifications and completed payments must be accounted for. Do not run down migrations or overwrite production data without an explicit reviewed decision.

## Backup And Isolated Restore Gate (Next 7A2)

Persistent volumes are not backups. Confirm an off-server destination, encryption/key custody, retention, access and acceptable data-loss/recovery times with the owner before implementing upload/retention automation. No destination or credentials have been assumed. Do not store secrets in this repository or attach database/key archives to public CI artifacts.

The recovery set must include PostgreSQL (both EF contexts share the database), uploaded listing files, shared authentication/reset Data Protection keys, API-only payment keys, required Caddy certificate/config data, the deployed source/Compose revision and a separately protected configuration copy. Possession of database ciphertext and its keys can expose merchant credentials. Encryption, storage access and key separation are required operational controls, not something a plain tar archive achieves.

For an initial coordinated backup, schedule a maintenance window and quiesce application writes/uploads/bidding/payment callbacks before taking a logical PostgreSQL dump plus file/key snapshots. Do not copy a running PostgreSQL data directory as a backup or assume unrelated live file/dump snapshots are consistent. Provider notifications may be retried or arrive during maintenance; preserve and reconcile them. Restore only into a separately named environment with isolated networks/volumes, no public routes, disabled outbound email/live payment side effects and restricted access. Never run a restore script against `/home/ubuntu/faunex` or production Compose volume names.

Acceptance requires checking archive integrity and successfully recovering tenant/account boundaries, uploaded bytes, encrypted sandbox credentials with their original key ring, a sample winner order and its verified payment history. Prove restart persistence, record timings and reconcile the backup timestamp with newer activity. A written procedure, successful archive command or CI service restart is not a demonstrated off-server restore. No backup/restore or VPS restart drill has been performed in 7A1.
