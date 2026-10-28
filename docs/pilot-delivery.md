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
| 4 | Bird listing edit/media/documents, enforced compliance, review and public storefront | 4A merged/deployed; 4B protected uploads in progress | Draft to evidence review to approved tenant listing, with resubmission and protected files |
| 5 | Timed auction setup, concurrent bid rules, closure and winner/no-sale result | Not started | Invalid/self/late bids rejected; concurrency tests; one persisted authoritative outcome |
| 6 | Tenant PayFast settings, winner order, checkout, verified callbacks, reconciliation | Not started | Sandbox failure/retry/duplicate tests and authorized low-value live payment to tenant account |
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

## Active Section 4B: Protected Listing Files

Branch: `feature/section-4b-protected-uploads`. Do not merge without user approval and green checks for the final revision.

- [x] Add tenant/seller file screens and navigation for JPEG/PNG photos and PDF/image evidence.
- [x] Keep bytes outside the public web root in persistent API-only storage; use server-generated keys, SHA-256 integrity checks and bounded quotas.
- [x] Validate actual image content, dimensions and decoded pixels; normalize orientation, resize and remove metadata before storing images.
- [x] Scan every original upload with private-network ClamAV; reject malware and fail closed on unavailable/unknown scan results.
- [x] Require tenant/owner authorization for private downloads; reserve cross-tenant evidence reads for super-admin/compliance reviewers, not general platform staff.
- [x] Publish only photos of active, approved listings on the matching verified tenant host. Never publish supporting documents.
- [x] Enforce revision conflicts and review/auction locks; file changes return listings to inactive Draft.
- [x] Accept only intact managed evidence for configured requirements, including a fresh check at approval. Legacy URL metadata is not proof.
- [x] Finish local, real PostgreSQL/scanner CI and desktop/mobile browser verification; record exact PR/check evidence below. Recheck the final handoff revision before merge.
- [ ] Merge with approval, verify deployment and perform the production file/restart acceptance checklist in [listing file operations](listing-files.md).

VPS capacity confirmed by user: 4 CPU, 8 GB RAM. Scanner capped at 1.5 CPU and 4 GB RAM; no public scanner port. Uploads return a retryable 503 while it initializes or is unavailable; other site functions do not wait for scanner health. Operational resource checks remain necessary after deployment.

Documents remain optional unless a tenant/animal-class policy explicitly requires them. PDFs are antivirus-scanned attachments, not parsed for legal validity or certified as safe. Images are normalized copies; their original bytes are not retained. Full reviewer UI/history and configurable requirement UI are not completed by 4B. Do not close all of Section 4 or start auction/payment implementation from this slice alone.

Pull request: [#38](https://github.com/ryanreynoldsspecnet/Faunex/pull/38), not merged. Implementation `987c739539593bf309f741326b0bf8527704d219` passed [CI 40740289121](https://github.com/ryanreynoldsspecnet/Faunex/actions/runs/40740289121): all 153 tests executed/passed on PostgreSQL/Linux, including actual ClamAV clean/EICAR checks, clean photo/PDF uploads through the scanner, concurrent same-revision uploads, private role/tenant denials, missing/corrupt storage, review/auction locks, quotas, EXIF normalization and persistence across storage instances. The initial CI run caught a wrapped PostgreSQL serialization conflict returning 500; the correction traverses the provider exception chain and returns 409, with only one persisted file and cleanup of the losing upload. No gate was bypassed. Caddy validation, vulnerability scan, clean Release build and both publish steps passed; deployment was correctly skipped.

Local verification: 147 tests passed, six PostgreSQL/real-scanner cases explicitly skipped (153 total); no pending model changes; API/Web publish and dependency advisory scan passed. The additive migration creates `ListingFiles` plus a nullable link from existing document metadata. It does not fetch old external URLs or migrate untrusted bytes into managed storage.

Edge/Playwright verification used isolated tenant/admin accounts and the real local upload API/native image processing with test-only in-memory file storage and scanner doubles. It uploaded two photos and a PDF, checked preview images, downloaded the exact PDF bytes, reloaded persisted records, switched public gallery photos, and confirmed that deleting a photo unpublishes and revokes public reads (404). Desktop 1440px and mobile 390px screenshots were inspected; no page errors or page-level overflow. Document tables scroll horizontally on narrow screens. Only the external preview fixture materializes SQLite grid/detail reads and sets an approved fixture state; real PostgreSQL SQL, authorization and scan behavior are covered in CI. This is not production review acceptance or a real VPS restart/restore drill.

Next: obtain merge approval, inspect master validate/deploy, and verify scanner health and a disposable upload on Stormbird. Then continue **4C reviewer evidence inspection and decision history/resubmission**. Requirement UI remains deferred until business rules are agreed. A separate custom-domain acceptance test and operational backup/restore acceptance remain open. No live tenant data was changed during 4B development.
