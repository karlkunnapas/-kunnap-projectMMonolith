## 1. Domain Model

- [x] 1.1 Add core entities: `ChargingStation`, `Reservation`, `ChargingSession`, `Maintenance`, `Vehicle`, `Connector`, `Company`, `Promotion`.
- [x] 1.2 Add junction entities: `VehicleConnector`, `ChargingStationConnector`, `AppUserCompany`, `UserPromotion`.
- [x] 1.3 Add enums: `EStationStatus`, `EReservationStatus`, `EMaintenanceStatus`.
- [x] 1.4 Add `CompanyId` to `ChargingStation` for multi-tenant support.

## 2. Infrastructure

- [x] 2.1 Register all new entities in `AppDbContext` via `DbSet<>`.
- [x] 2.2 Update seed roles with `Admin`, `Customer`, and `MaintenancePersonnel`.

## 3. OpenSpec Completion

- [x] 3.1 Add change-level capability specs under `openspec/changes/add-charging-station-entities/specs`.
- [x] 3.2 Sync capability specs to `openspec/specs`.
- [x] 3.3 Archive change after sync.

