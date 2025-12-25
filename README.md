# StormBird Auction Platform

StormBird Auction Platform is a modern, scalable auction platform focused on birds—both exotic and indigenous. It’s built to support real-world auction workflows for buyers and sellers, with an admin back-office for operations and reference data management. The platform is designed to be **compliance-aware**, enabling regulatory considerations such as **CITES classification** and permit-related requirements to be modeled and enforced as the system grows.

## Key Concepts

- **Auctions**: Auction creation, listing discovery, bidding, and outcomes.
- **Buyers and sellers**: Marketplace roles and workflows for listing and purchasing.
- **Admin back-office**: Administrative UI for managing platform configuration and reference data.
- **Compliance-aware design**: Domain concepts that can incorporate rules and documentation needs (e.g., CITES / permits).

## Tech Stack

- **.NET 10**
- **ASP.NET Core Web API**
- **Blazor Web App** (UI + Admin)
- **PostgreSQL**
- **Entity Framework Core (EF Core)**
- **Docker**

## Architecture

StormBird follows **Clean Architecture** principles: domain logic is kept independent of infrastructure and delivery concerns, enabling maintainable iteration and long-term scalability.

- **API as the backend**: `StormBird.Api` exposes application functionality via an ASP.NET Core Web API.
- **Blazor as the UI client**: `StormBird.Web` provides the user-facing UI and admin back-office experience.
- **Database access only via the API**: the UI communicates through the API; direct database access from the UI is intentionally avoided.

High-level layering:

- `StormBird.Domain`: Core domain entities, enums, and business rules.
- `StormBird.Application`: Use-cases, DTOs, and application interfaces.
- `StormBird.Infrastructure`: EF Core persistence, migrations, and infrastructure implementations.
- `StormBird.Api`: HTTP endpoints, dependency injection wiring, and API configuration.
- `StormBird.Web`: Blazor Web App client (UI + Admin).

## Development Status

This repository is in an **early-stage foundation** phase.

Current state:

- **Database, API, and UI are wired** for end-to-end development.
- **Reference data is implemented**, providing initial foundational data needed for early workflows.