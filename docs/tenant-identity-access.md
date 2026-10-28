# Tenant Identity And Access

Faunex is a white-label multi-tenant marketplace platform. Identity and authorization must preserve that platform shape.

## Access Levels

### Public Visitor

Not signed in. Can browse public tenant marketplace content and register/login.

### Registered Buyer

Signed-in user. Public registration creates a `Buyer` user. When registration happens through a configured tenant domain, the user is attached to that tenant. When registration happens through the Faunex platform domain, the user remains tenantless for now.

### Seller

Tenant-scoped user who can create and manage listings for their tenant.

### Tenant Administrator

Tenant-scoped administrator who can manage tenant listings, tenant users, compliance workflows, and tenant configuration.

### Platform Support / Compliance

Faunex operator roles used to assist or review activity across tenants without becoming a tenant user.

### Platform Administrator / Super Administrator

Faunex operator roles. Platform users are not assigned to a tenant and can administer tenants from the platform surface.

## Current Role Assignment Rules

- Public registration defaults to `Buyer`.
- Public registration through a configured active tenant domain assigns the new user to that tenant.
- Platform roles cannot be mixed with tenant roles on the same user.
- Platform users must not have a `TenantId`.
- `TenantAdmin` and `Seller` users must have a valid active `TenantId`.
- A user assigned only `Buyer` may be tenantless when registering through the Faunex platform domain.

## Domain-Based Tenancy

Tenant resolution happens before registration and will later be used for marketplace actions:

- `faunex.co.za` is the platform domain.
- Tenant domains, for example `tenant-example.co.za`, resolve to a tenant.
- A user registering through a tenant domain should be created in that tenant context.
- Faunex platform administrators can still manage all tenants from the platform domain.

Platform administrators can manage tenant domains through:

- `GET /api/platform/tenants/{tenantId}/domains`
- `POST /api/platform/tenants/{tenantId}/domains`
- `DELETE /api/platform/tenants/{tenantId}/domains/{domainId}`

Domain hostnames are normalized before storage and lookup. Each hostname can belong to only one tenant.

## Account And Session Enforcement

JWT signatures and expiry are validated first. Every authenticated API request then checks the current Identity account, security stamp, role set, tenant assignment, platform flag, email and tenant activity. Disabled/deleted accounts, inactive/missing tenants and obsolete claims are rejected. Pre-security-stamp tokens require a fresh login after the Section 2 deployment.

Administrator security updates rotate the security stamp. Password resets use Identity's stamp rotation. Re-enabling an account does not restore its old browser session. Five incorrect passwords temporarily lock an enabled account for five minutes; a successful login clears the failure count.

The Blazor UI clears its stored token when the API returns 401 and checks token lifetime when resolving authentication state. A 403 is a permission denial, not a reason to sign out. There is no idle-session polling timer.

TenantAdmin and Seller can manage listings subject to tenant/ownership checks. Only TenantAdmin can open/close auctions. PlatformSuperAdmin or PlatformComplianceAdmin can perform compliance review; a platform flag alone does not grant review permission.

Only a platform super administrator can assign the super-admin role or modify another super administrator. User mutations protect the last active tenant/platform super administrator and run inside serializable transactions. The current account cannot delete or disable itself.

Tenant creation, first-user creation and its TenantAdmin assignment share a database transaction. Failure rolls back the complete onboarding operation. The legacy domain trading profile is synchronized from Identity before tenant token issuance for listing/bid foreign-key compatibility; it never grants permissions.

The tenant-domain registration/membership model is still Section 3 work. Platform UI registration is hidden; the older API contract still supports tenantless buyers when no tenant host resolves and must not be treated as completed domain enforcement.
