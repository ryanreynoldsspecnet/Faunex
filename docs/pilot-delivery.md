# Faunex Pilot Delivery

This is the restart point for development. Read this file and the [28 October audit](audit-2026-10-28.md) before starting another feature. Finish the active section, verify it, and record the handoff before opening another section.

## Confirmed Direction

- Controlled first-tenant pilot, targeting acceptance on 13 November 2026.
- Keep .NET 10, Blazor, PostgreSQL and the current layered architecture.
- Finish the existing workflows before adding new marketplace modes or animal verticals.
- Tenant subdomains and verified custom domains must preserve tenant isolation.
- Online payments are required: tenant-owned PayFast accounts receive tenant sales directly.
- Keep future Faunex subscription billing separate from tenant sales. Pricing/mandates are not decided.
- Recommended deferrals: Yoco implementation, native apps, wallets, seller payouts and sealed bids, unless scope is explicitly changed.

## Sections

| Section | Scope | Status | Evidence required to close |
| --- | --- | --- | --- |
| 1 | Dependency gate, maintained CI actions, secret-file exclusions, durable handoff | Merged and deployed | PR #28; validate/deploy passed; live homepage 200 |
| 2 | Account eligibility, session revocation, consistent role policies, first-admin transaction | Merged and deployed; business smoke test pending | PR #34; production run 40669423490 validate/deploy passed |
| 3 | Tenant domains, verified routing/TLS, membership/host boundaries, complete branding | Merged; Stormbird subdomain confirmed working; custom-domain acceptance pending | PRs #35/#36; deployment 40712476902 passed; user confirmed live homepage |
| 4 | Bird listing edit/media/documents, enforced compliance, review and public storefront | 4A/4B/4C merged/deployed; business acceptance open | Draft to evidence review to approved tenant listing, with resubmission and protected files |
| 5 | Timed auction setup, concurrent bid rules, closure and winner/no-sale result | Merged/deployed; live auction acceptance open | PR #40; deployment 41021191875 passed; disposable live auction/NTP/worker acceptance pending |
| 6 | Tenant PayFast settings, winner order, checkout, verified callbacks, reconciliation | 6A merged/deployed; 6B1 pricing verified, PR #42 unmerged; checkout/notifications pending | Sandbox failure/retry/duplicate tests and authorized low-value live payment to tenant account |
| 7 | Pilot acceptance, operational recovery, monitoring, mobile/desktop journeys | Not started | Business acceptance, restore/restart drill, exact-revision green deploy and rollback procedure |

Merchant account approval, first-tenant domain access, payment totals/fees, and trading/compliance rules need business preparation now. Do not postpone payment discovery until Section 6; establish the sandbox/callback path early while keeping implementation PRs bounded.

## Completed Section 1

Branch: `infra/pilot-section-1-validation`.

Pull request: [#28](https://github.com/ryanreynoldsspecnet/Faunex/pull/28).

- [x] Trace flagged cryptography and MessagePack dependencies to their sources.
- [x] Select published patched package versions and verified Node-24 action commits.
- [x] Add local secret-file exclusions to Git and Docker context.
- [x] Keep the vulnerability gate enabled; add weekly validation and dependency-update proposals.
- [x] Pass refreshed advisory scan, Release build, tests and both publish commands.
- [x] Open a PR, inspect actual validate job results and record the PR/check evidence here.
- [x] Merge only after user approval, verify deployment, then sync master and clean the merged branch.

No business behavior, database schema, VPS credentials, or live user data is changed in Section 1. Weekly workflow runs validate only: deployment remains restricted to pushes on master. Dependabot proposes changes; it does not auto-merge them.

Local verification on 28 October: all eight projects have no flagged vulnerable packages; full Release build has zero warnings/errors; 19/19 tests pass; API and Web publish successfully. Local build and publish both use `/p:UseAppHost=false` because this checkout is on Google Drive. Publishing without that matching setting initially failed because no apphost had been built; matching the build/publish settings resolved it. The Linux Actions run remains the verification for the unchanged default apphost settings.

GitHub verification: [final branch validation](https://github.com/ryanreynoldsspecnet/Faunex/actions/runs/40655771547) passed. PR #28 was merged with user approval at `dbbf294c2a14367e867744a2bb7ac8aed538cbe5`. [Production run 40660376225](https://github.com/ryanreynoldsspecnet/Faunex/actions/runs/40660376225) passed both validate and deploy; live HTTPS homepage returned 200. Local master was fast-forwarded and the merged branch was removed locally and remotely.

## Completed Implementation: Section 2

Merged at `e1961bb1988d4f02a91eecd4664bb5d2f0b0ac12`. [Production run 40669423490](https://github.com/ryanreynoldsspecnet/Faunex/actions/runs/40669423490) passed validate and deploy. Local master was fast-forwarded and the merged branch removed. A fresh-login business smoke test remains for the user; no production credentials were used by the agent.

Pull request: [#34](https://github.com/ryanreynoldsspecnet/Faunex/pull/34).

- [x] Check current account, lockout, tenant, security stamp, roles and identity claims on authenticated API requests.
- [x] Reject disabled/inactive accounts at login; lock out five failed passwords for five minutes and reset failure count on success.
- [x] Rotate security stamps during administrator security changes; reject old tokens after password reset, deletion, reassignment and role changes.
- [x] Clear browser auth state after API 401; reject expired/malformed tokens when resolving UI state. There is no idle polling/logout timer; server authorization remains authoritative on every API request.
- [x] Allow TenantAdmin or Seller listing management; reserve auction open/close for TenantAdmin; return proper authorization denials.
- [x] Protect super-admin assignment and last-admin removal using serializable Identity mutation transactions.
- [x] Share the tenant/Identity transaction when creating a tenant and first administrator; test validation failure, late role failure and unexpected exceptions.
- [x] Synchronize the legacy trading profile before issuing tenant tokens, so listing/bid foreign keys refer to the authenticated user. This profile is not an authorization source.
- [x] Pass the local 65-test suite, clean Release build, API/Web publish and dependency scan.
- [x] Inspect actual PostgreSQL CI results, including concurrent last-admin removal, and open/verify the PR.
- [x] Verify the user-completed merge and successful production deployment, then clean the branch.
- [ ] User fresh-login business smoke test on production.

The HTTP tests reuse production auth/controller registration with isolated data and ephemeral test keys. Local tests use SQLite; Actions starts PostgreSQL 16 and runs these scenarios against unique temporary databases. Tests do not connect to the VPS, send real emails, or mutate real users. A patched SQLite native test dependency is pinned explicitly to keep the advisory gate clear.

Implementation commit `daa0d7f96ad4621598db80fab67cf67894dfda7c` passed [PostgreSQL CI run 40665335413](https://github.com/ryanreynoldsspecnet/Faunex/actions/runs/40665335413): 65 tests passed, including parallel last-admin deletion and all four onboarding success/failure paths; restore, vulnerability scan, Release build and both publish steps passed with zero warnings/errors. Deployment was correctly skipped. This handoff-only commit follows that implementation run; inspect the latest revision's checks before merging.

Deployment note: tokens issued before the security-stamp claim was introduced are deliberately invalidated. Users must log in again after Section 2 deployment; passwords and existing accounts are not reset. No schema migration is introduced in this section. Staging, domain/member resolution, full listing/compliance/auction behavior and payments are still separate work.

## Handoff Rules

1. Record the active branch/PR, exact verification results, remaining blockers and next section before stopping.
2. A completed local build is not a successful deployment. Inspect validate and deploy jobs explicitly.
3. A successful deploy is not business acceptance. Demonstrate the complete relevant user journey.
4. Keep one bounded implementation section active. Record new ideas separately instead of starting them mid-section.
5. Use staging/sandbox for authenticated and payment regression tests; never test destructive edge cases against live tenant data.

## Section 3: Implemented, Custom-Domain Acceptance Pending

PR #35 was user-merged at `3c4273a567f808ee54e825f6f531a62a13a32ccd`. [Master deployment 40686415138](https://github.com/ryanreynoldsspecnet/Faunex/actions/runs/40686415138) completed successfully. Local master was synchronized and the merged local branch removed. First live tenant host: `stormbird.faunex.co.za`; the user added DNS and assigned the domain. The initial homepage listings error was resolved by PR #36 below. Separate custom-domain acceptance remains open.

- [x] Add an operator domain registry with search, add, verify, rotate challenge, set primary, disable and delete.
- [x] Require custom-domain DNS TXT proof with a random, expiring challenge; reject stale in-flight verification.
- [x] Restrict routing and Caddy's certificate permission endpoint to verified, active domains of active tenants.
- [x] Bind registration, login, reset links and authenticated requests to the marketplace hostname. Preserve central access for tenant administrators while setup is incomplete.
- [x] Apply tenant name/logo/colour in shared navigation and authentication pages; validate logo sources and colours server-side.
- [x] Add an additive schema migration and pin the matching local EF tool.
- [x] Add Caddy configuration validation to CI and explicitly reload Caddy after deployment.
- [x] Pass 97 local tests, including two-tenant denials, pending/disabled domains, verification rotation and tenant reset links.
- [x] Complete browser, PostgreSQL migration and Caddy CI checks; record PR and exact results.
- [ ] Merge with approval; verify a real subdomain and real custom-domain TLS journey with user-controlled DNS.

Read [tenant domain operations](tenant-domains.md) before rollout. Existing domains deliberately become unverified after migration; custom domains need fresh proof, and managed Faunex subdomains can be verified by a platform operator. The platform root and central tenant-administration login remain available. Do not remove working DNS records or copy secrets into commits.

Pull request: [#35](https://github.com/ryanreynoldsspecnet/Faunex/pull/35). Implementation `3e71f61bfed864a580ddc034e079c039e891f841` passed [CI run 40674950322](https://github.com/ryanreynoldsspecnet/Faunex/actions/runs/40674950322): Caddy configuration validation, complete PostgreSQL migration chain, tests, vulnerability gate, Release build and both publishes passed. Deploy was correctly skipped. A final UI polish/handoff commit follows this run; inspect that revision's checks before merging.

Edge/Playwright verification used an isolated in-memory API with test-only accounts, not the VPS. Platform login redirected to the tenant list; adding a custom hostname returned to Domains with pending status and TXT details; failed verification displayed an error; search worked; mobile had no page-level overflow; unknown hosts returned 404; tenant login showed tenant navigation branding. No browser page errors occurred. Desktop/mobile screenshots are local artifacts. Table content scrolls horizontally on narrow screens. A configurable `DataProtection:KeyPath` enabled the Windows-only preview without changing the existing Linux production default. Preview keys/accounts are not committed.

Pilot scope retains one tenant per account and globally unique email addresses. This section enforces that boundary; it does not implement multi-tenant memberships, cross-domain single sign-on, a logo-upload store, automatic DNS editing, or wildcard certificates. Primary-domain selection is metadata, not a cross-origin login redirect. Logo settings continue using HTTPS image URLs or existing local assets.

The user confirmed Stormbird is working after PR #36 and approved proceeding to Section 4. A separate tenant-owned custom hostname has not been supplied; its live DNS/TLS acceptance remains open and must not be described as completed.

### Marketplace Loading Follow-Up

PR [#36](https://github.com/ryanreynoldsspecnet/Faunex/pull/36) was user-merged at `4fd4392a942499bf052c8cfa3e21ab51386da980`. [Production run 40712476902](https://github.com/ryanreynoldsspecnet/Faunex/actions/runs/40712476902) passed validate and deploy. Read-only HTTPS checks of Stormbird home/Browse and Faunex home returned 200, with no former 500/unavailable message; Stormbird home correctly showed no approved listings. The user separately confirmed the live site works. Local master was synchronized and the merged local fix branch removed before starting 4A.

The Stormbird screenshot and a read-only public GET confirm the page reports an API 500 while tenant branding loads. Local PostgreSQL-provider SQL compilation reproduces failures composing browse ordering and detail filtering over the positional `ApprovedListingRow` record projection. These failures occur before SQL is sent, even for an empty marketplace. Server-log confirmation was requested separately; no VPS credentials/data were accessed.

The narrow correction uses an EF-translatable member-initializer projection while preserving server-side filtering, ordering, tenant scoping and approval rules. The homepage now keeps a resolved tenant visible if only listings fail, shows a retryable listings error, and does not mislabel an API failure as a missing marketplace.

Regression coverage adds browse/detail/auction SQL translation checks that run without a database, plus actual PostgreSQL HTTP tests for empty and populated tenant marketplaces, approval/active filters, ordering, null review timestamps and cross-tenant denials. PostgreSQL-only HTTP tests are explicitly skipped on local SQLite (which cannot order DateTimeOffset), not silently treated as passes; CI must execute them before review. Browser retry/error/empty-state verification uses test-only local responses, not production data. Record final check evidence in the follow-up PR before merging, then re-test the live Stormbird homepage and Browse Listings.

Staging, exact-revision deployments, backup/restore evidence and runtime-image freshness remain open operational work from the audit; Section 1 does not claim to complete those items.

## Implemented Section 4A: Bird Draft Editor

Branch: `feature/section-4a-bird-listing-editor`. Section 4 is deliberately split into reviewable pieces; this does not claim the upload-to-approval journey is finished.

- [x] Replace separate tenant/seller creation forms with one species-based create/edit screen and entry points from listing details.
- [x] Default new drafts to ZAR and persist quantity/location, ring/microchip identifiers, sex and hatch date. Existing USD remains unchanged unless explicitly edited; no currency conversion is performed.
- [x] Share server validation across canonical creation, legacy creation and updates. No arbitrary seller transfer or client-controlled activation.
- [x] Enforce seller ownership and tenant boundaries; tenant admins can edit their tenant's listings.
- [x] Reject stale revisions, review/suspended states and listings with existing auction activity. Edits return listings to inactive Draft and require review again.
- [x] Consolidate submission methods and load animal classifications before checking tenant-specific required evidence. With no configured rules, documents are optional; submission still requires manual review. Missing configured evidence remains PendingDocuments and may be resubmitted.
- [x] Add nullable bird-detail fields and a listing revision token through an additive migration. Existing rows receive the empty revision and rotate on their first edit.
- [x] Complete local/real-PostgreSQL regression, browser workflow, vulnerability, publish and migration checks; record PR and exact results below. Recheck final revision CI before merge.
- [x] Verify the user-completed merge and production deployment; synchronize master and remove the merged branch.
- [ ] User acceptance of draft creation/editing on the production tenant host.

Revision checks rotate on tracked listing changes and compliance submissions/review decisions. Concurrency with auction setup/bidding still belongs to Section 5; this slice rejects auctions already present, not a completed auction state machine. Old API edit clients must reload and send the current Revision. Direct client attempts to change SellerId or publish via IsActive are rejected/ignored respectively.

Business decision on 28 October: the user cannot yet define permit/certificate rules and explicitly requested optional documents until the owners/partners establish the requirements. Replace the old blanket animal requirements with an empty-by-default tenant/animal-class policy. No CITES/veterinary or other animal documents are made mandatory automatically. The policy table supports later configuration; requirement UI is deferred pending business decisions and protected uploads follow in 4B. Manual review is still mandatory before publication. Platform approval is not legal certification. Existing stored document metadata is not yet a protected upload store. Do not mark document security or legal compliance complete from this slice.

Next within Section 4: **4B protected image/evidence uploads**, then **4C reviewer evidence inspection, review history/decisions, resubmission and demonstrated publication**. Tenant requirement controls remain deferred until business rules are agreed; the empty-by-default policy table is already present. Do not begin auctions/payments merely because the draft editor is merged. Full taxonomy, new animal verticals and commercial billing remain outside this slice.

Pull request: [#37](https://github.com/ryanreynoldsspecnet/Faunex/pull/37). Initial implementation `c36ce27012da3e44928050095b5b5326c04ff241` passed [CI 40719976568](https://github.com/ryanreynoldsspecnet/Faunex/actions/runs/40719976568): all 122 tests executed/passed on PostgreSQL, including concurrent saves, optional/default and missing configured evidence; both migrations, Caddy validation, vulnerability gate, Release build and API/Web publish passed. Deployment correctly skipped.

Browser QA then exposed a decimal RangeAttribute limit parsed using the machine culture, terminating the Blazor circuit. This was fixed using invariant limit parsing and covered under en-ZA, af-ZA, en-US and fr-FR cultures. Final local suite: 123 passed, 3 explicitly skipped PostgreSQL-only cases, 126 total; no model changes pending. The follow-up commit includes this fix and UI spacing; inspect its actual CI evidence in PR #37, not only the initial run.

Edge/Playwright created a draft using the real local API, returned to the tenant list, reopened it, verified ZAR/quantity/ring/microchip fields, edited the listing on mobile, saved and reopened it to confirm persistence. No browser page errors or page-level mobile overflow. Desktop/mobile screenshots were inspected. Test-only SQLite grid rendering materializes before timestamp sorting because SQLite cannot sort DateTimeOffset; this workaround exists only in the external preview fixture, not application code. Production-provider SQL and migrations are validated in PostgreSQL CI. No production users, files, email or data were changed.

PR #37 was user-merged at `2cecd8c8c7377c88c5402a4ed8a79accfa85110b`. [Production run 40724005267](https://github.com/ryanreynoldsspecnet/Faunex/actions/runs/40724005267) passed validate/deploy, with all 126 tests executing successfully in CI. Read-only Stormbird home/Browse and Faunex home checks returned 200 without marketplace errors. Master was synchronized and the merged branch removed. This verifies deployment, not an authenticated production business acceptance test.

## Implemented Section 4B: Protected Listing Files

Merged branch: `feature/section-4b-protected-uploads`, removed locally and remotely after PR #38.

- [x] Add tenant/seller file screens and navigation for JPEG/PNG photos and PDF/image evidence.
- [x] Keep bytes outside the public web root in persistent API-only storage; use server-generated keys, SHA-256 integrity checks and bounded quotas.
- [x] Validate actual image content, dimensions and decoded pixels; normalize orientation, resize and remove metadata before storing images.
- [x] Scan every original upload with private-network ClamAV; reject malware and fail closed on unavailable/unknown scan results.
- [x] Require tenant/owner authorization for private downloads; reserve cross-tenant evidence reads for super-admin/compliance reviewers, not general platform staff.
- [x] Publish only photos of active, approved listings on the matching verified tenant host. Never publish supporting documents.
- [x] Enforce revision conflicts and review/auction locks; file changes return listings to inactive Draft.
- [x] Accept only intact managed evidence for configured requirements, including a fresh check at approval. Legacy URL metadata is not proof.
- [x] Finish local, real PostgreSQL/scanner CI and desktop/mobile browser verification; record exact PR/check evidence below. Recheck the final handoff revision before merge.
- [x] Verify user-completed merge and production deploy; synchronize master and remove merged branch.
- [ ] Live scanner-health, upload and restart/restore acceptance in [listing file operations](listing-files.md).

VPS capacity confirmed by user: 4 CPU, 8 GB RAM. Scanner capped at 1.5 CPU and 4 GB RAM; no public scanner port. Uploads return a retryable 503 while it initializes or is unavailable; other site functions do not wait for scanner health. Operational resource checks remain necessary after deployment.

Documents remain optional unless a tenant/animal-class policy explicitly requires them. PDFs are antivirus-scanned attachments, not parsed for legal validity or certified as safe. Images are normalized copies; their original bytes are not retained. Full reviewer UI/history and configurable requirement UI are not completed by 4B. Do not close all of Section 4 or start auction/payment implementation from this slice alone.

Pull request: [#38](https://github.com/ryanreynoldsspecnet/Faunex/pull/38), user-merged at `9aac7379d0f7ea6f980b3f2190232811ffced2bb`. [Production run 40742473221](https://github.com/ryanreynoldsspecnet/Faunex/actions/runs/40742473221) passed validate/deploy. The deploy log confirms the scanner started and the two new volumes were created; it is not proof of scanner health. Faunex home and Stormbird home/Browse returned 200 without marketplace errors. Local SSH host-key verification was unavailable and was not bypassed; a PuTTY `docker compose ps -a` check was requested. Master was synchronized and the merged branch removed before 4C.

Implementation `987c739539593bf309f741326b0bf8527704d219` passed [CI 40740289121](https://github.com/ryanreynoldsspecnet/Faunex/actions/runs/40740289121): all 153 tests executed/passed on PostgreSQL/Linux, including actual ClamAV clean/EICAR checks, clean photo/PDF uploads through the scanner, concurrent same-revision uploads, private role/tenant denials, missing/corrupt storage, review/auction locks, quotas, EXIF normalization and persistence across storage instances. The initial CI run caught a wrapped PostgreSQL serialization conflict returning 500; the correction traverses the provider exception chain and returns 409, with only one persisted file and cleanup of the losing upload. No gate was bypassed. Caddy validation, vulnerability scan, clean Release build and both publish steps passed; deployment was correctly skipped.

Local verification: 147 tests passed, six PostgreSQL/real-scanner cases explicitly skipped (153 total); no pending model changes; API/Web publish and dependency advisory scan passed. The additive migration creates `ListingFiles` plus a nullable link from existing document metadata. It does not fetch old external URLs or migrate untrusted bytes into managed storage.

Edge/Playwright verification used isolated tenant/admin accounts and the real local upload API/native image processing with test-only in-memory file storage and scanner doubles. It uploaded two photos and a PDF, checked preview images, downloaded the exact PDF bytes, reloaded persisted records, switched public gallery photos, and confirmed that deleting a photo unpublishes and revokes public reads (404). Desktop 1440px and mobile 390px screenshots were inspected; no page errors or page-level overflow. Document tables scroll horizontally on narrow screens. Only the external preview fixture materializes SQLite grid/detail reads and sets an approved fixture state; real PostgreSQL SQL, authorization and scan behavior are covered in CI. This is not production review acceptance or a real VPS restart/restore drill.

Next: **4C reviewer evidence inspection and decision history/resubmission**, while scanner/file operational acceptance remains open. Requirement UI remains deferred until business rules are agreed. A separate custom-domain acceptance test and operational backup/restore acceptance remain open. No live tenant data was changed during 4B development.

## Implemented Section 4C: Review Workflow

Merged branch: `feature/section-4c-compliance-review`. PR #39 was user-merged at `8e92bda05040e1ca0fb737d4fb098d0a549d0e59`. [Production run 40883226790](https://github.com/ryanreynoldsspecnet/Faunex/actions/runs/40883226790) passed validate/deploy. Faunex home and Stormbird home/Browse returned 200 without marketplace errors. Master was synchronized and the merged branch removed. On 29 October the user explicitly asked development to continue while business review is deferred. Acceptance remains open, not waived.

- [x] Replace quick approve/reject rows with a searchable, status-filtered, paginated queue and detail-first review.
- [x] Show listing/animal details, tenant/seller, requirements and private read-only file access to super-admin/compliance reviewers.
- [x] Require current revision for every review decision, bounded notes, reasons for requested changes/suspension, valid states, active tenant and no existing auction activity.
- [x] Recheck required evidence and every current file's integrity before approval; publish only on approval.
- [x] Persist actor/time/from/to/notes/revision history atomically with status changes, including submission, edits and file changes. No history-edit endpoint; application writes reject modification/deletion of tracked history records.
- [x] Show tenant/seller status/history and submission/resubmission. Suspended listings can be returned for changes by a reviewer; they do not resume publication automatically.
- [x] Verify local/real PostgreSQL concurrency, migrations, scanner, browser workflow, vulnerability/build/publish gates; record PR and exact results below. Recheck final handoff revision before merge.
- [x] Verify user-completed merge and successful production deployment.
- [ ] Business acceptance using disposable tenant data; live scanner/upload/restart checks remain open.

History begins at this deployment; do not invent or backfill old decisions. The latest 200 events are displayed, with all events retained in the database while the listing exists. This is status/revision history, not a complete immutable snapshot of prior listing content or a legal certification. SQL maintenance or cascade removal of a listing is outside the application append-only guard. Retention/backup policies remain operational acceptance work. Reviewer notes are visible to the owning seller and tenant administrators, so do not enter confidential internal-only comments.

Old submission endpoints remain compatible when no revision is supplied, but still enforce current status, ownership, evidence and optimistic concurrency. New UI submission sends the revision. Review decision requests without Revision are now rejected; old approve/reject clients must reload and update. Requirement-management UI remains deferred. Auction lifecycle/payment implementation is still Sections 5/6, not part of 4C.

Pull request: [#39](https://github.com/ryanreynoldsspecnet/Faunex/pull/39), merged. Implementation `eb6ae95474b9e1a1ae7efcc35017ca1164262b2d` passed [CI 40747419892](https://github.com/ryanreynoldsspecnet/Faunex/actions/runs/40747419892): all 162 tests executed/passed, including PostgreSQL queue search/status/pagination, competing reviewers (one committed decision, one 409, one history decision), full correction/resubmission/approval, required and missing optional-file checks, inactive-tenant/auction guards, role/tenant denials, append-only history and real ClamAV regression. All migrations, vulnerability/Caddy/build/publish checks passed; deploy correctly skipped. Local: 154 passed, eight explicitly skipped provider/scanner cases; no pending model changes and no flagged NuGet advisories. A final UI-spacing/handoff commit follows; inspect that revision's actual checks before merge.

Edge/Playwright used separate isolated tenant and platform reviewer sessions. It created a bird draft, uploaded a photo/PDF, submitted, downloaded private PDF evidence as the reviewer, confirmed no reviewer file-delete controls, requested a ring-number correction, showed rejection notes to the tenant, edited/resubmitted, approved and rendered the public gallery. A second stale reviewer page received a conflict; suspension revoked a fresh public photo GET (404). Desktop 1440px and mobile 390px captures were inspected; no page errors or page-level overflow. Tables scroll horizontally on small screens. All writes, review detail/history reads and file authorization used the real local API with test-only storage/scanner doubles; existing grid/tenant detail/public detail and the queue used external SQLite rendering shims for unsupported date/decimal sorts. Real query/migration/authorization behavior is covered in PostgreSQL CI. No production accounts, email or files were changed.

Following the user-approved merge, master validate/deploy was verified. The user explicitly approved continuing Section 5 while their review is deferred. Demonstrating the review journey on disposable Stormbird data and live scanner/upload/restart acceptance remain open; continuing development does not waive those checks. Separate custom-domain and off-server restore acceptance remain open; do not describe all pilot work as complete.


## Implemented Section 5: Timed Auctions

Branch: `feature/section-5-timed-auctions`. User approved continuing development without waiting for business review. Do not merge new work automatically or claim that deferred acceptance checks passed.

- [x] Add seller/tenant draft setup with validated future start/end, approved starting price, optional private reserve and configurable minimum increment.
- [x] Restrict publication/cancellation/finalisation to tenant administrators; publish locks terms, cancellation is pre-start/no-bids only and needs a reason.
- [x] Enforce approved/active listing, buyer role, tenant/owner boundaries, no self-bids, server acceptance window, valid two-decimal amounts, starting floor and minimum increment.
- [x] Commit accepted bid, monotonic bid number, cached leader and listing/auction revisions atomically. Conflicting bids reload; the server never automatically increases a buyer's bid.
- [x] Persist NoBids/ReserveNotMet/Sold results and winner only for Sold; repeated closure is idempotent, results cannot reopen. Add a periodic recoverable sweep for expired configured auctions.
- [x] Separate public auction/bid DTOs from private reserve, bidder/winner identities and internal revisions. Legacy/unconfigured/sealed records are not public or biddable.
- [x] Add buyer server-status polling and tenant workspace controls. Draft setup still allows listing corrections/review; published or legacy auction activity locks listing/file changes.
- [x] Complete isolated local and real PostgreSQL/scanner CI, additive migrations, desktop/mobile browser, dependency/build/publish checks; record evidence below. Recheck final handoff revision CI before merge.
- [ ] Merge with approval; inspect production deploy and perform disposable-auction business acceptance.

Pilot defaults for owner review: one listing is one auction lot, one winner, the winning bid is the total lot price (quantity does not create split winners). First bid may equal the approved starting price; later bids must be at least the leader plus the configured increment (default 10 in the listing currency). An optional reserve is private and must be at least the starting price. No automatic extensions, proxy/max bidding, sealed bids, buy-now checkout, mid-auction cancellation, early closure or relisting are introduced. Terms are editable only before publication; a published/cancelled/closed record is not silently recycled. These are bounded pilot rules, not legal or commercial approval.

Dates persist as UTC; setup and displays explicitly use SAST (UTC+02:00). Windows are one minute to 30 days. Bidding accepts only StartsAt <= server acceptance time < EndsAt, independent of browser time; durable commit/response may follow the acceptance timestamp. A delayed result worker does not extend bidding. Closure uses the persisted accepted leader, checks its stored linkage/counter, and atomically records an outcome. It continues to settle already accepted auctions of an inactive tenant. An inconsistent ledger requires reconciliation, not an invented winner.

Auction currency is frozen when terms are saved. If the listing currency changes during draft corrections, publication requires an explicit terms re-save; numeric reserves/increments are not converted. Bids and settlement check the frozen currency. Published listing terms are locked. VPS clock synchronization is operationally essential: verify NTP before real bidding. The production closing worker sweeps up to 100 due records every ten seconds with fresh per-auction scopes, survives restarts and retries conflicts; monitor settlement errors and overdue results rather than promising immediate closure under every load.

Deployment is additive: existing auction rows keep RulesVersion 0 and are not automatically published or settled. Existing bids are retained; no result is fabricated for legacy data. Before rollout, inspect legacy rows, especially any with bids; stop and reconcile any real pre-existing activity rather than treating it as a new pilot auction. A legacy record without bids can be explicitly configured as a draft; multiple legacy auctions for a listing require reconciliation. The unique bid-number index applies only to positive numbers so old zero-number rows are not rewritten.

The local Google Drive checkout returned a semaphore/read timeout on an unchanged static asset and had a loaded DLL lock. Verification therefore uses an exact committed source export on local disk, not disabled static-asset checks or changes to production paths. Local test hosts also use isolated temporary configuration roots rather than reading/watching repository appsettings. GitHub remains the verification for actual Linux/PostgreSQL migrations and concurrency.

Pull request: [#40](https://github.com/ryanreynoldsspecnet/Faunex/pull/40). Implementation `ba68cbc4e552787449a459fd2c33e93927d78961` passed [CI 40900520899](https://github.com/ryanreynoldsspecnet/Faunex/actions/runs/40900520899): all 182 tests executed/passed, including real PostgreSQL/scanner cases; migrations, Caddy validation, dependency advisory gate, Release build and both publishes succeeded. Deploy was correctly skipped. Recheck the final documentation revision's checks before merge. Local verification: 171 passed, 11 explicitly skipped provider/scanner cases, 182 total; Release Web publish succeeded and EF reports no pending model changes. Two additive migrations introduce lifecycle/concurrency fields, the filtered positive bid-number index and frozen currency; no live auction was created or migrated by local verification.

Coverage includes role/owner/foreign-tenant denials, draft corrections/reapproval, start/end boundaries, minimum/precision limits, private reserve/identity protection, Sold/ReserveNotMet/NoBids, pre-start cancellation, immutable published terms/results, currency changes, exact compatibility bid responses, concurrent equal bids (one acceptance), duplicate setup, and competing settlement workers even for an inactive tenant. Existing file/review and real ClamAV tests remain enabled. Initial fixture disposal and legacy conflict-response failures were corrected; no CI gate was bypassed.

Final Edge/Playwright verification used isolated tenant/reviewer/two-buyer sessions: create bird draft, upload photo, submit and approve through the real API, configure reserve/increment/SAST dates, publish, prevent pre-start bidding, accept 1500/1525/1575 ZAR bids, stop the form at the deadline, finalise and show Sold privately/publicly. Desktop 1440px and mobile 390px screenshots were inspected; no page errors or page-level overflow. Galleries render actual uploaded bytes; the fixture image is not real inventory. Auction reads/writes, bidding and settlement use actual application code. Only existing SQLite grid/detail/queue rendering uses external shims for unsupported timestamp/decimal ordering; database concurrency and migrations run on PostgreSQL in CI. Test-only clock controls exercise boundaries; the browser test invokes the actual manual finalisation path, not proof of VPS worker operation. No production accounts, files, email or auction data were changed.

After Section 5: Section 6 tenant-owned PayFast settings, winner order/checkout, verified notifications, idempotency/reconciliation. No payment or platform subscription billing is implemented here. Scanner health, first-tenant business acceptance, independent custom-domain TLS, backups/restore and operational rollout checks remain open and must be recorded before pilot go-live.

PR #40 was user-merged at `0b1f4cc5a3d7ff87c25a0b0070a63506dda30330`. [Production run 41021191875](https://github.com/ryanreynoldsspecnet/Faunex/actions/runs/41021191875) passed validate/deploy. Read-only Faunex home and Stormbird home/Browse returned 200 without marketplace errors. Master was synchronized and the merged branch removed. This does not close live bidding/worker/NTP, scanner, restore or business acceptance. On 30 October the user approved starting Section 6.

## Active Section 6A: Payment Readiness And Winner Orders

Branch: `feature/section-6a-payment-readiness`. Pull request: [#41](https://github.com/ryanreynoldsspecnet/Faunex/pull/41). This is a bounded foundation, not a completed PayFast integration. No money can be collected; checkout is unavailable, no paid state or browser-return shortcut exists, and the API rejects live mode.

- [x] Add tenant-admin-only sandbox merchant settings with revision checks, credential replacement/removal, actor/time metadata and secret-free read DTOs.
- [x] Protect credentials with tenant-bound ASP.NET Data Protection purposes and a dedicated API-only key volume, separate from shared login/reset keys. No key/passphrase is returned to the browser after saving.
- [x] Create one immutable order snapshot atomically with a configured auction's Sold result; no order for NoBids/ReserveNotMet/cancellation. Preserve winning amount/currency, lot quantity/title, buyer/seller and winning-bid linkage.
- [x] Give buyers their own order list/detail and tenant administrators their tenant's searchable order list/detail; deny other buyers, foreign tenants and platform staff.
- [x] Allow a winning buyer to recover an order for a pre-6A closed Sold result without accepting caller-supplied totals or identities. Repeated requests return the same order; simultaneous creation must retain one snapshot.
- [x] Finish real PostgreSQL/scanner CI and desktop/mobile browser evidence; record exact verification below. Check the final documentation revision's CI in PR #41 before merge.
- [ ] User-approved merge, production validation/deployment, key-volume persistence and business acceptance.

The stored order Amount is the winning-bid subtotal for the whole lot, not unit price multiplied by quantity and not a final payable total. On 30 October the user confirmed tenants need configurable additional charges: specialist delivery can depend on cage size, animal/plant type or other requirements, and other extras may apply. The API explicitly reports ChargesNotFinalised and the UI identifies the lot subtotal; checkout remains disabled. Before checkout, implement tenant-owned charge definitions/options, itemised order charges and an immutable final pricing quote. A no-extra-charge option must be explicit, not assumed. Changing a charge catalogue must not rewrite accepted quotes, and currency/rounding must remain consistent. Do not invent delivery rates, cage-size formulas, buyer-premium percentages or tax treatment; VAT inclusion/exclusion and final rules still require confirmation. Existing USD auctions remain USD, with no conversion; future PayFast checkout must reject unsupported currency rather than relabel it ZAR. New orders snapshot listing title/quantity at closure; recovery of older Section 5 results snapshots their currently stored listing details, not an invented historic content version.

Settings are explicitly sandbox-only. Storing a merchant ID/key/passphrase does not verify account ownership or prove they are correct. Blank secret fields retain the existing pair only for the same merchant; changing merchant or replacing either secret requires both. Initial/changed settings require the current revision. Removal clears the current protected credential blob, not database backups or historical WAL. Never put real credentials in Git, issue comments, screenshots or chat. Platform administrators have no settings-secret read or tenant-payment-write bypass.

Production rollout adds a `payment_keys` Docker volume mounted only in the API at `/var/lib/faunex/payment-keys`; no new VPS environment variable is required. `Payments:KeyPath` can override the location for other deployments. Database ciphertext is tenant-bound, but the key ring is not externally wrapped by default: a host/root compromise or possession of both database and keys defeats this protection. Restrict host/backup access and encrypt off-server backups. Back up the key volume together with the database, preserve old keys, and verify restore/restart before accepting credentials for live use. Losing keys requires merchant re-entry, never a plaintext fallback. The test verifies provider recreation using a temporary key directory, not VPS restore acceptance.

An additive migration creates the settings/orders tables and a unique auction-order index, with restrictive order foreign keys. Existing rows are not charged, auto-reconciled or bulk backfilled. The application rejects modifying/deleting order snapshots; direct SQL remains an operational trust boundary. Section 6B should add payment attempts/notifications separately instead of rewriting winning totals. PostgreSQL revealed competing closers can lose the unique order insert before the auction revision update; that specific constraint now becomes a retryable concurrency conflict. The existing worker conflict path retries safely, and the race test still requires one final auction result and one order.

PayFast research: [official custom-integration requirements](https://payfast.io/integration/submit-your-solution/), [passphrase configuration](https://support.payfast.help/portal/en/kb/articles/how-do-i-enable-a-passphrase-on-my-payfast-account-20-9-2022), [developer documentation](https://developers.payfast.co.za/). Developer-page fetches timed out during 6A; support/requirements pages were accessible. Before implementing 6B, retrieve the complete current custom-integration specification, including canonical field order/encoding, signature, ITN source validation, gross-amount/merchant/reference checks and server-to-server validation. Do not substitute the API signature format or guess validation details.

Implementation `5e9c1fe4299462239dfa530d0eaba72fffe2655c` passed [CI 41036387615](https://github.com/ryanreynoldsspecnet/Faunex/actions/runs/41036387615): all 196 tests executed/passed, no skips, including actual PostgreSQL concurrent settings/recovery/settlement, the complete migration chain and real ClamAV regression. Caddy validation, dependency advisory gate, Release build and API/Web publishes passed; deploy correctly skipped. No gate was bypassed. Local suite: 183 passed, 13 explicitly skipped provider/scanner cases, 196 total; no pending model changes. Normalized source comparison confirmed the local-disk verification snapshot matches the Google Drive branch. A final documentation commit follows; inspect its checks before merging.

Final Edge/Playwright used isolated tenant/reviewer/two-buyer sessions. It saved sandbox credentials, toggled visibility, verified fields clear after save/reload, created/uploaded/submitted/approved a listing, published and bid, finalised a Sold result, displayed the winning subtotal with Charges not finalised and checkout unavailable, showed the winner's order and the losing buyer's empty list, searched/opened the tenant order, and removed credentials. Desktop 1440px and mobile 390px screenshots were inspected, with no page errors or page-level overflow. Order and settings calls use actual application endpoints without response shims; existing SQLite listing grid/detail/queue shims and test-only storage/scanner/clock controls remain isolated in the external preview. Actual PostgreSQL/scanner coverage runs in CI. Test-only merchant values were used; no PayFast network transaction, production mutation, email, live payment or VPS restart/restore occurred.

Next: **6B pricing and sandbox checkout**, starting with tenant-configurable extra charges/delivery options and an explicit final itemised quote, then checkout and verified ITNs. Payment attempts must snapshot the final quote, merchant and environment; use explicit ZAR support, trusted callback forwarding, bounded validation/timeouts, durable idempotency and duplicate/conflicting-event reconciliation. A return/cancel page must never mark an order paid. Test sandbox completion/failure/retry/duplicate/out-of-order/forged notifications before an explicitly authorized low-value live payment to the tenant's own account. Faunex subscription billing, Yoco, refunds, seller payouts and split payments remain separate scope.

PR #41 was user-merged at `03e92dc909f4e3fcb72335a9963a8517041b3582`. [Production run 41073978763](https://github.com/ryanreynoldsspecnet/Faunex/actions/runs/41073978763) passed validate/deploy. Read-only Faunex home and Stormbird home/Browse returned 200 without marketplace errors. Master was synchronized and the merged local branch removed. This verifies deployment, not live credentials, payment, auction or key-volume restart/restore acceptance. The user approved continuing delivery options/additional charges on 30 October.

## Active Section 6B1: Delivery, Extras And Final Pricing

Branch: `feature/section-6b-tenant-charges`. Pull request: [#42](https://github.com/ryanreynoldsspecnet/Faunex/pull/42). This is the pricing part of Section 6B, not PayFast checkout. Payment remains unavailable and no order is marked paid by accepting a price.

- [x] Add a searchable tenant-only charge catalogue with add/edit/remove, active flags, currency, tenant-entered pricing/tax terms and explicit publication.
- [x] Support named delivery/collection alternatives, required extras and optional extras, each a fixed non-negative two-decimal charge per whole lot. Free collection must be an explicit zero-priced option.
- [x] Validate ownership, role/host boundaries, option IDs, bounded fields/counts and revisions. Use a catalogue concurrency stamp to serialize publication against edits without changing the semantic catalogue revision just because an auction was published.
- [x] Freeze active pricing terms at auction publication, disclose them publicly before bidding, and copy them unchanged into the winner order. Later edits/removal/deactivation/unpublishing affect future auctions only.
- [x] Let the winning buyer select exactly one delivery/collection option and optional extras; always apply required extras. Calculate the itemised total on the server, review it, and explicitly accept one immutable quote per order. Tenant administrators can read, not accept on the buyer's behalf.
- [x] Reject forged totals, foreign/unknown/duplicate/wrong-kind selections, stale terms, duplicate conflicting acceptance and unsupported totals. Retried identical acceptance returns the same quote; concurrent creation has one persisted result.
- [x] Complete real PostgreSQL/scanner CI, additive migration and desktop/mobile journey verification; record evidence below. Inspect final handoff revision checks before merge.
- [ ] User-approved merge, successful production deployment and first-tenant pricing acceptance.

Default catalogue is absent, not a claim of free delivery or zero fees. Existing auctions without recorded pricing remain biddable under their existing auction rules, but cannot obtain a final quote/checkout by borrowing a newer catalogue. No backfill or retroactive fees are introduced. They require business reconciliation; do not recycle an auction with accepted bids. If a tenant has created a draft/unpublished catalogue or its currency differs from the listing, publishing an auction is blocked until the catalogue is published in that currency. New payment-ready auctions must have recorded terms before bidding begins.

Pilot operation: tenant admin configures currency/terms at `/tenant/charges`, adds delivery or explicit free collection plus any required/optional extras, then publishes the catalogue. Publishing a subsequent auction records that version. The winning buyer uses My Orders to select options, Review Total and confirm acceptance. Tenant Orders then shows the same final breakdown. The winning subtotal remains immutable and is never multiplied by lot quantity; extras are applied once per lot. The catalogue is limited to 40 options; names/descriptions/terms are bounded. ZAR and existing USD pricing are explicit, never converted. PayFast currency eligibility still belongs to the upcoming checkout slice.

Rates and conditions are tenant-defined. Named cage-size/region/delivery choices are supported as flat options with descriptions, not automatic eligibility rules, cage measurement, distance/postcode rating or carrier booking. No address or fulfilment workflow is claimed. Do not invent VAT rates, tax inclusion, buyer premiums, percentages or per-animal multipliers. Terms are the tenant's declaration, not legal certification. Quote terms/labels/prices are durable structured JSON snapshots, not live references to editable catalogue rows. Accepted quotes cannot be repriced/cancelled through this slice; a future correction/payment cancellation policy must preserve accepted history rather than overwrite it.

The additive migration creates `TenantChargeCatalogs` and `OrderPriceQuotes`, adds nullable pricing snapshots to auctions/orders, and enforces one quote per order with restrictive foreign keys. Existing auction/order data remains unchanged. Application saves reject modifying/deleting accepted quotes; direct SQL and database retention are operational trust boundaries. No payment secrets, volumes or public API proxy routes change in this slice.

Implementation `aea5b91f20f8c8d0fc07965bcd478bfd7b6d4f8b` passed [CI 41078282113](https://github.com/ryanreynoldsspecnet/Faunex/actions/runs/41078282113): all 211 tests executed/passed with no skips, actual PostgreSQL concurrent publication/catalogue edits and quote confirmations, full migration chain, real ClamAV regression, dependency advisory gate, Caddy validation, Release build and both publishes. Deploy correctly skipped. Initial CI found sub-microsecond acceptance timestamps differed between the first response and a persisted PostgreSQL retry; normalising to microseconds before saving corrected the contract without weakening the equality assertion. Local suite: 196 passed, 15 explicitly skipped database/scanner cases, 211 total; no pending EF model changes, and normalised source comparison matched the local-disk verification snapshot to the Google Drive branch. A final documentation commit follows; check its CI before merging.

Final Edge/Playwright used isolated tenant/reviewer/two-buyer sessions. It configured collection/delivery/required handling/optional crate prices through the UI, published terms, created/uploaded/reviewed an approved listing, published and bid on its auction, and closed Sold. The buyer selected delivery plus crate, reviewed a 1575 ZAR subtotal plus 135 ZAR extras, accepted 1710 ZAR, reloaded the durable quote and saw editing controls disappear. Raising the tenant delivery price to 999 afterward changed neither the accepted quote nor the auction's disclosed 100 ZAR delivery option. Tenant search/detail and the losing buyer's empty order list also passed. Desktop 1440px/mobile 390px screenshots were inspected; no page errors or page-level overflow. Narrow tables scroll horizontally. New catalogue/pricing endpoints use real application code; only pre-existing SQLite listing grid/detail/queue rendering shims and isolated storage/scanner/clock controls remain in the external fixture. No PayFast transaction, carrier booking, production record, real credential or live email was involved.

Next after pricing verification: **6B2 PayFast sandbox checkout and verified notifications**, creating payment attempts from the accepted quote, with immutable tenant merchant/environment snapshots, bounded provider validation, idempotency and reconciliation. Keep live activation and an explicitly authorized low-value payment separate from coding completion. Scanner, key-volume restart/restore, authenticated auction acceptance and independent custom-domain acceptance remain open.
