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
| 2 | Account eligibility, session revocation, consistent role policies, first-admin transaction | Implemented and verified; awaiting merge approval | PR #34; 65/65 local and PostgreSQL CI tests; clean vulnerability/build/publish checks |
| 3 | Tenant domains, verified routing/TLS, membership/host boundaries, complete branding | Not started | Two-tenant negative tests; working subdomain/custom domain; unknown hosts fail closed |
| 4 | Bird listing edit/media/documents, enforced compliance, review and public storefront | Not started | Draft to evidence review to approved tenant listing, with resubmission and protected files |
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

## Active Section 2

Branch: `fix/pilot-section-2-account-access`. Production remains on Section 1 until a separate Section 2 merge is approved.

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
- [ ] Merge only with approval, verify production deployment and fresh-login smoke test, then clean the branch.

The HTTP tests reuse production auth/controller registration with isolated data and ephemeral test keys. Local tests use SQLite; Actions starts PostgreSQL 16 and runs these scenarios against unique temporary databases. Tests do not connect to the VPS, send real emails, or mutate real users. A patched SQLite native test dependency is pinned explicitly to keep the advisory gate clear.

Implementation commit `daa0d7f96ad4621598db80fab67cf67894dfda7c` passed [PostgreSQL CI run 40665335413](https://github.com/ryanreynoldsspecnet/Faunex/actions/runs/40665335413): 65 tests passed, including parallel last-admin deletion and all four onboarding success/failure paths; restore, vulnerability scan, Release build and both publish steps passed with zero warnings/errors. Deployment was correctly skipped. This handoff-only commit follows that implementation run; inspect the latest revision's checks before merging.

Deployment note: tokens issued before the security-stamp claim was introduced are deliberately invalidated. Users must log in again after Section 2 deployment; passwords and existing accounts are not reset. No schema migration is introduced in this section. Staging, domain/member resolution, full listing/compliance/auction behavior and payments are still separate work.

## Handoff Rules

1. Record the active branch/PR, exact verification results, remaining blockers and next section before stopping.
2. A completed local build is not a successful deployment. Inspect validate and deploy jobs explicitly.
3. A successful deploy is not business acceptance. Demonstrate the complete relevant user journey.
4. Keep one bounded implementation section active. Record new ideas separately instead of starting them mid-section.
5. Use staging/sandbox for authenticated and payment regression tests; never test destructive edge cases against live tenant data.

## Next Section

Section 3 follows Section 2 acceptance: verified tenant domains/routing/TLS, host/membership boundaries and complete tenant branding. Do not start it while Section 2 remains under review.

Staging, exact-revision deployments, backup/restore evidence and runtime-image freshness remain open operational work from the audit; Section 1 does not claim to complete those items.
