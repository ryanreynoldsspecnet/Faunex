# Tenant Identity And Access

Faunex is a white-label multi-tenant marketplace platform. Identity and authorization must preserve that platform shape.

## Access Levels

### Public Visitor

Not signed in. Can browse public tenant marketplace content and register/login.

### Registered Buyer

Signed-in user. Public registration currently creates a `Buyer` user. Domain-aware registration will later attach the user to the tenant resolved from the request host.

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
- Platform roles cannot be mixed with tenant roles on the same user.
- Platform users must not have a `TenantId`.
- `TenantAdmin` and `Seller` users must have a valid active `TenantId`.
- A user assigned only `Buyer` may currently be tenantless. Tenant-aware registration will tighten this once tenant domain resolution is implemented.

## Future Domain-Based Tenancy

Tenant resolution should happen before registration or marketplace actions:

- `faunex.co.za` is the platform domain.
- Tenant domains, for example `tenant-example.co.za`, resolve to a tenant.
- A user registering through a tenant domain should be created in that tenant context.
- Faunex platform administrators can still manage all tenants from the platform domain.

The next domain-specific model should introduce tenant domain records and request-host tenant resolution.
