# Tenant Domain Operations

## Boundaries

`faunex.co.za` is the platform portal, not a public cross-tenant marketplace. Platform accounts use that host. Tenant administrators may use the central portal or their own verified marketplace. Buyers/sellers register, log in and reset passwords on their tenant marketplace only. Each account currently belongs to one tenant; email uniqueness remains global.

The Web service derives the marketplace hostname from the actual browser URL and supplies `X-Faunex-Marketplace-Host` to the internal API. This header is not an authentication credential. Keep the API port private, as in the Compose configuration; any future public/mobile API gateway must derive/validate this origin itself rather than blindly trust a caller-supplied header. API claims and tenant membership still authorize private data on every request. Caddy enforces TLS SNI/Host agreement.

## Add a Faunex Subdomain

1. In Platform > Tenants > Domains, add the chosen hostname, for example `stormbird.faunex.co.za`.
2. The platform owns this namespace, so assignment by a platform administrator verifies it. Reserved names such as `www`, `api`, `admin`, `mail` and nested subdomains cannot be assigned.
3. In the Faunex DNS zone, add an A record for the chosen subdomain pointing to `154.65.98.94`. Existing wildcard DNS is also sufficient, but does not authorize arbitrary tenant names in the application.
4. Remove an incorrect AAAA record for this specific hostname if one exists; do not change unrelated email records.
5. Once DNS resolves, visit the HTTPS URL. Caddy requests the certificate on the first handshake. Select Set Primary if this should be the tenant's advertised hostname.

## Add a Custom Domain

1. Add the exact hostname under the correct tenant. Apex and `www` are separate assignments; verify each one actually needed.
2. Copy the displayed TXT name and value into the domain's DNS zone. Proof is `_faunex-verification.<hostname>` with `faunex=<random token>`. DNS dashboards may expect a zone-relative name rather than the full name.
3. Add an A record pointing that hostname to `154.65.98.94`, or a suitable CNAME for a non-apex hostname. A records are the simplest pilot setup. Avoid an incorrect AAAA record; check restrictive CAA records if certificate issuance fails.
4. Click Verify after the TXT record is publicly visible. The verifier uses public DNS resolvers; proof expires after seven days. New TXT Record invalidates the previous token.
5. Visit the HTTPS hostname and check branding, registration, login, password reset and logout. Select Set Primary if appropriate.

DNS proof establishes control; it does not prove A/AAAA routing, certificate issuance or a complete buyer journey. Those require separate live checks. Do not mark this acceptance complete based on the Verify button alone. Faunex never collects tenant DNS-provider credentials in this workflow.

## TLS and Deployment

Caddy's on-demand TLS configuration calls the private API permission endpoint. Only verified, active assignments belonging to active tenants return 200. Unknown, unverified, disabled and reserved hosts are denied. Certificate issuance/renewal additionally depends on public DNS, inbound TCP 80/443, CA availability/rate limits and the persisted `caddy_data` volume. Do not delete that volume. HTTP/3 additionally uses UDP 443 but is not required for HTTPS.

CI validates the Caddyfile before deployment. Compose mounts the configuration directory (not a stale individual file inode), and deployment reloads Caddy explicitly after Compose starts the application. This mount change recreates the proxy on the first deployment, with a brief connection interruption; persisted certificates remain in `caddy_data`. The allowlist changes dynamically through the database; assigning a tenant domain does not require editing/restarting Caddy.

After an approved deployment, use read-only checks on the VPS:

```bash
docker compose ps
docker compose logs --since 10m caddy faunex-web faunex-api
curl -sS -o /dev/null -w '%{http_code}\n' https://faunex.co.za/
curl -sS -o /dev/null -w '%{http_code}\n' https://YOUR-TENANT-HOST/login
```

Do not paste SMTP credentials, JWT keys, passwords or reset tokens into logs/issues. GET-based curl checks avoid the app's unsupported HEAD behavior on some pages.

## Existing Assignments and Removal

The additive verification migration leaves old assignments unverified. Review them from the central portal. Managed subdomains can use Verify directly; custom domains need New TXT Record then Verify. This intentionally avoids treating historical database entries as ownership proof.

Disabling/deleting a domain or tenant stops application routing and new TLS authorization. Already-issued certificates may remain cached until expiry; the application gate still denies the host. Remove tenant-controlled DNS when offboarding. Domain reassignment requires a new record/challenge; never reuse an old verification token.

Before rollout, obtain a database/volume backup through the existing operations process. Reverting the application/Caddy commit leaves these nullable schema additions harmless; do not run a destructive down migration on live data merely to roll back code.

## Acceptance Still Required

- Choose first tenant, managed subdomain and a custom hostname controlled by the user.
- Deploy approved PR, configure DNS and verify both HTTPS certificates.
- Check desktop/mobile branding and buyer registration/reset on those hosts.
- Attempt cross-tenant account access and confirm unknown hosts do not expose a marketplace.
- Confirm platform administrator and central tenant-administrator access still work.

No real DNS changes, live certificate requests or production-user mutations were performed during local implementation.
