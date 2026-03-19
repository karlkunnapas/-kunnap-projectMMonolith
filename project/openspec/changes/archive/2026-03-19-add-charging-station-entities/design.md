## Context

This change introduces the initial operational domain model for the EV charging platform. The implementation is already present in the codebase and includes entities, junction entities, enums, DbContext registration, and role seed updates.

## Goals

- Represent charging operations in a normalized relational model.
- Support multi-tenant ownership via `Company` and station-level `CompanyId`.
- Keep architecture aligned with Domain -> EF Core DbContext integration.
- Enable future service-layer and API workflows without schema redesign.

## Domain Design

### Core Entities

- `ChargingStation`: station identity, location, status, pricing, power, activity flag, optional tenant ownership.
- `Reservation`: booking window between user and station.
- `ChargingSession`: actual charging activity with usage and cost.
- `Maintenance`: fault/maintenance lifecycle for stations.
- `Vehicle`: user-owned EV profile.
- `Connector`: connector type catalog.
- `Company`: tenant/owner profile.
- `Promotion`: discount definitions, optionally tenant-scoped.

### Junction Entities

- `VehicleConnector`: compatible connectors for vehicles.
- `ChargingStationConnector`: available connectors per station.
- `AppUserCompany`: user-to-company association.
- `UserPromotion`: user-to-promotion assignment.

### Enums

- `EStationStatus`
- `EReservationStatus`
- `EMaintenanceStatus`

## Data Model Integration

- All new entities are registered as `DbSet<>` in `AppDbContext`.
- Relationship behavior follows global delete restriction policy.
- `ChargingStation.CompanyId` supports multi-tenant partitioning.
- Seeded roles include `Admin`, `Customer`, and `MaintenancePersonnel`.

## Validation and Constraints

- String fields use data annotations for size and basic validation.
- Required relationships are represented with non-nullable foreign keys where appropriate.
- Optional tenancy is modeled with nullable foreign keys where needed (for example, company-scoped promotions).

## Risks and Mitigations

- **Risk:** Wide initial schema can increase migration size.
  - **Mitigation:** Keep schema additive and rely on explicit migrations.
- **Risk:** Missing capability specs can drift from implemented domain.
  - **Mitigation:** Maintain one capability spec per proposal capability and sync to main specs before archive.

## Outcome

The implemented domain model provides the foundational schema and relationships required for reservation, charging, maintenance, tenant ownership, and promotion workflows.

