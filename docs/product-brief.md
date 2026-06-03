# Faunex Product Brief

## Purpose

Faunex is a commercial multi-tenant SaaS marketplace and auction platform for regulated and specialist trading communities.

The platform began from the need to support Storm Bird Auctions, but the product vision is broader than a single bird-auction website. Faunex should support many specialist marketplaces from one shared codebase and infrastructure base, with each marketplace operating as an independent tenant.

The guiding principle is:

> Build Faunex as a platform. Build Storm Bird as a tenant.

## Product Vision

Faunex should become a premier auction and specialist trading platform for South Africa, Africa, and eventually broader markets.

The platform should be suitable for:

- aviculture and bird auctions
- livestock auctions
- poultry auctions
- game and exotic animal auctions
- breeder communities
- associations and membership marketplaces
- conservation or compliance-heavy trading networks
- future specialist marketplaces that need regulated listing, review, bidding, and document workflows

Storm Bird Auctions is Tenant 1 and the first proving ground.

Future tenant examples:

- Storm Bird Auctions
- WGAA Marketplace
- Poultry Auctions SA
- Livestock Exchange
- Exotic Breeders Africa
- Conservation Trading Network
- Specialist Association Marketplaces

## Core Product Principles

Faunex must be:

- multi-tenant
- API-first
- mobile-ready
- compliance-driven
- real-time
- scalable
- cloud deployable
- containerized
- extensible

Product and engineering decisions should favour reusable platform capabilities over tenant-specific shortcuts.

## Tenant Model

Every tenant represents an independent marketplace.

Each tenant should eventually support:

- branding
- logo
- custom domain
- email templates
- commission structures
- species, categories, or marketplace taxonomy
- compliance requirements
- moderators
- administrators
- subscription plans

Most business data should belong to a tenant through `TenantId`.

Important tenant-aware concepts include:

- `Tenant`
- `TenantUser`
- `TenantSettings`
- `TenantBranding`
- `TenantSubscription`
- `Auction`
- `Listing`
- `Species`
- `Document`
- `Wallet`
- `Notification`
- `User`

## Current Technology Stack

The original planning notes referenced .NET 9. The current repository has moved to .NET 10.

Current implementation stack:

- Backend: ASP.NET Core / .NET 10
- Frontend: Blazor Web App
- Mobile target: .NET MAUI for Android and iOS later
- Database: PostgreSQL
- Cache and scale-out target: Redis
- Realtime target: SignalR
- Infrastructure: Docker
- Local orchestration target: .NET Aspire
- CI/CD: GitHub Actions
- Hosting: Xneelo Linux VPS

## Architecture Standards

Use Clean Architecture.

Current solution projects:

- `Faunex.Domain`
- `Faunex.Application`
- `Faunex.Infrastructure`
- `Faunex.Api`
- `Faunex.Web`
- `Faunex.AppHost`
- `Faunex.Shared`

Expected layer responsibilities:

- Domain: entities, aggregates, value objects, domain events, business rules
- Application: use cases, commands, queries, validators, DTOs, interfaces
- Infrastructure: EF Core, PostgreSQL, Redis, file storage, email services, payment providers, notification providers
- Presentation: API, Blazor UI, future MAUI clients

Backend design must avoid web-only assumptions so future mobile apps can use the same API cleanly.

## Identity And Roles

Users may eventually belong to one or more tenants.

Required roles:

- Platform Super Admin
- Tenant Owner
- Tenant Administrator
- Moderator
- Seller
- Buyer
- Member

Identity should support:

- JWT
- refresh-token readiness
- MFA readiness
- external-login readiness
- role-based permissions
- future KYC support

## Marketplace Framework

Faunex should support more than auctions over time.

Marketplace modes may include:

- timed auctions
- sealed-bid auctions
- buy-now sales
- classified listings
- breeder directories
- membership platforms

The architecture should not assume that all future tenant workflows are auctions.

## Auction Framework

Initial auction types:

- Timed Auction
- Buy Now
- Sealed Bid

Timed auction capabilities:

- start time
- end time
- reserve price
- bid increments
- anti-sniping

Buy-now capabilities:

- fixed price
- instant purchase

Sealed-bid capabilities:

- hidden bids
- winner selected after close

## Compliance Framework

Compliance is a core differentiator.

Faunex must support tenant-specific compliance rules and document requirements.

Examples:

- bird permits
- CITES permits
- veterinary certificates
- membership verification
- ownership transfers
- import/export documentation
- identity documents
- health certificates
- compliance records

Compliance should be configurable per tenant. Storm Bird is the first implementation, not the hard-coded model.

Document workflow:

- Uploaded
- Pending Review
- Approved
- Rejected
- Expired

Every compliance action should be auditable.

## Species And Taxonomy Framework

The first taxonomy implementation is for Storm Bird, but the model should become reusable across tenants and animal categories.

Storm Bird taxonomy requirements include:

- biological classification: class, order, family, genus, species, common name
- regional classification: indigenous or exotic, habitat, geographic origin
- compliance classification: CITES status, permit requirements, trading restrictions
- breeding data: sex, age, hatch date, breeding status
- identification data: ring number, microchip number, health status
- listing metadata: temperament, sale purpose, special requirements

The taxonomy engine should remain extensible enough for livestock, poultry, game, and future specialist categories.

## Search And Discovery

Search is a major selling point.

Search should support:

- full-text search
- species search
- taxonomy search
- compliance filtering
- auction filtering
- geographic filtering

Near-term implementation can use PostgreSQL search.

Future roadmap:

- Elasticsearch or OpenSearch

## Realtime Architecture

Use SignalR for live platform updates.

Realtime events include:

- new bids
- auction countdowns
- listing changes
- notifications
- status updates

Redis backplane support is required for future scaling.

## Wallet And Payments

The financial system should be future-proof even if not implemented immediately.

Buyer capabilities:

- deposit funds
- view balances
- view transactions

Seller capabilities:

- receive settlements
- track payouts

Payment providers to consider:

- Yoco
- Ozow
- PayFast

Commission models:

- buyer pays
- seller pays
- split commission

Commission configuration should be tenant-specific.

## Notification Framework

Notifications should be event-driven.

Channels:

- email
- WhatsApp
- push notifications
- SMS

Events:

- outbid
- auction won
- auction lost
- listing approved
- compliance approved
- payment received

## Mobile Applications

Future .NET MAUI apps should support:

- browse listings
- realtime bidding
- wallet management
- notifications
- document uploads

The API must remain mobile-first and must not depend on Blazor-specific assumptions.

## Security Requirements

Mandatory:

- HTTPS
- JWT
- role-based permissions
- audit logging
- rate limiting
- secure file storage
- KYC readiness
- POPIA compliance

## Current Implementation State

Foundations already in place:

- solution structure
- ASP.NET Core API
- Blazor Web App
- PostgreSQL integration
- Docker deployment
- Aspire host
- GitHub Actions CI/CD
- deployment to Xneelo Linux VPS
- vulnerability remediation
- initial foundation tests
- tenant query-filter fix

Existing API/service areas include:

- species
- bird listings
- auctions
- health
- authentication
- platform admin surface

Some endpoints expose contracts while business implementation is still incomplete.

## Development Priorities

### Phase 1: Platform Foundations

- multi-tenancy
- identity
- authorization
- tenant onboarding
- tenant-aware navigation

### Phase 2: Storm Bird Taxonomy And Compliance

- species database
- taxonomy engine
- compliance engine
- permit framework

### Phase 3: Listings

- listing creation
- images
- documents
- moderation
- approval workflow

### Phase 4: Auction Engine

- timed auctions
- sealed bids
- buy now
- bid rules
- auction lifecycle

### Phase 5: Realtime Infrastructure

- SignalR
- Redis backplane
- live auction updates

### Phase 6: Wallets And Payments

- deposits
- settlements
- commissions
- provider integration

### Phase 7: Notifications

- email
- WhatsApp
- push
- SMS

### Phase 8: Mobile Applications

- MAUI Android
- MAUI iOS

### Phase 9: Tenant Commercialization

- custom domains
- branding
- subscription plans
- tenant billing

## Two-Month Delivery Bias

Given the near-term deadline, development should focus on a working vertical slice rather than broad platform completeness.

Recommended first usable vertical slice:

1. Faunex-branded public home and navigation
2. authentication and role-aware navigation
3. seller listing creation
4. compliance/admin review
5. buyer browse/listing detail
6. basic timed auction or bid workflow
7. deployment through the validated CI/CD pipeline

Every feature should still be implemented as a reusable platform capability, with Storm Bird as Tenant 1.
