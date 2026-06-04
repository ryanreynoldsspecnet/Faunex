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
