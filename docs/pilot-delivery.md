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
| 1 | Dependency gate, maintained CI actions, secret-file exclusions, durable handoff | In progress | Clean advisory scan, Release build, all tests, API/Web publish, actual PR Actions checks |
| 2 | Account eligibility, session revocation, consistent role policies, first-admin transaction | Not started | Disabled/deleted/reassigned users rejected; correct TenantAdmin/Seller permissions; rollback and role regression tests |
| 3 | Tenant domains, verified routing/TLS, membership/host boundaries, complete branding | Not started | Two-tenant negative tests; working subdomain/custom domain; unknown hosts fail closed |
| 4 | Bird listing edit/media/documents, enforced compliance, review and public storefront | Not started | Draft to evidence review to approved tenant listing, with resubmission and protected files |
| 5 | Timed auction setup, concurrent bid rules, closure and winner/no-sale result | Not started | Invalid/self/late bids rejected; concurrency tests; one persisted authoritative outcome |
| 6 | Tenant PayFast settings, winner order, checkout, verified callbacks, reconciliation | Not started | Sandbox failure/retry/duplicate tests and authorized low-value live payment to tenant account |
| 7 | Pilot acceptance, operational recovery, monitoring, mobile/desktop journeys | Not started | Business acceptance, restore/restart drill, exact-revision green deploy and rollback procedure |

Merchant account approval, first-tenant domain access, payment totals/fees, and trading/compliance rules need business preparation now. Do not postpone payment discovery until Section 6; establish the sandbox/callback path early while keeping implementation PRs bounded.

## Active Section 1

Branch: `infra/pilot-section-1-validation`.

- [x] Trace flagged cryptography and MessagePack dependencies to their sources.
- [x] Select published patched package versions and verified Node-24 action commits.
- [x] Add local secret-file exclusions to Git and Docker context.
- [x] Keep the vulnerability gate enabled; add weekly validation and dependency-update proposals.
- [x] Pass refreshed advisory scan, Release build, tests and both publish commands.
- [ ] Open a PR, inspect actual validate job results and record the PR/check evidence here.
- [ ] Merge only after user approval, verify deployment, then sync master and clean the merged branch.

No business behavior, database schema, VPS credentials, or live user data is changed in Section 1. Weekly workflow runs validate only: deployment remains restricted to pushes on master. Dependabot proposes changes; it does not auto-merge them.

Local verification on 28 October: all eight projects have no flagged vulnerable packages; full Release build has zero warnings/errors; 19/19 tests pass; API and Web publish successfully. Local build and publish both use `/p:UseAppHost=false` because this checkout is on Google Drive. Publishing without that matching setting initially failed because no apphost had been built; matching the build/publish settings resolved it. The Linux Actions run remains the verification for the unchanged default apphost settings.

## Handoff Rules

1. Record the active branch/PR, exact verification results, remaining blockers and next section before stopping.
2. A completed local build is not a successful deployment. Inspect validate and deploy jobs explicitly.
3. A successful deploy is not business acceptance. Demonstrate the complete relevant user journey.
4. Keep one bounded implementation section active. Record new ideas separately instead of starting them mid-section.
5. Use staging/sandbox for authenticated and payment regression tests; never test destructive edge cases against live tenant data.

## Next Section

Section 2 is next after Section 1 is accepted. Start with disabled-account/tenant enforcement and session revocation, then align the TenantAdmin listing policies and make first-admin onboarding transactional. Include regression tests with the implementation.

Staging, exact-revision deployments, backup/restore evidence and runtime-image freshness remain open operational work from the audit; Section 1 does not claim to complete those items.
