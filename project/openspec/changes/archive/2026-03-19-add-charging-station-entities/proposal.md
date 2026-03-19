## Why

The electric car charging stations management system requires core domain entities to support the fundamental business operations of managing charging stations, user reservations, charging sessions, maintenance tracking, and multi-tenant company operations. Without these entities, the system cannot store or manage the essential data for charging station network operations.

## What Changes

- Add 8 new domain entities: ChargingStation, Reservation, ChargingSession, Maintenance, Vehicle, Connector, Company, Promotion
- Add 4 junction entities for many-to-many relationships: VehicleConnector, ChargingStationConnector, AppUserCompany, UserPromotion
- Add 3 new enums: EStationStatus, EReservationStatus, EMaintenanceStatus
- Update AppDbContext with new DbSets for all entities
- Update seeding data with new user roles (Admin, Customer, MaintenancePersonnel)
- Add CompanyId to ChargingStation for multi-tenant support

## Capabilities

### New Capabilities
- `charging-station-management`: Core functionality for managing charging station inventory, status, and configuration
- `reservation-system`: User reservation handling for charging time slots
- `charging-session-tracking`: Real-time monitoring and recording of charging sessions
- `maintenance-management`: Issue logging, tracking, and resolution for charging stations
- `vehicle-management`: User vehicle profiles with connector compatibility
- `connector-management`: Definition and management of charging connector types
- `company-management`: Multi-tenant company operations and station ownership
- `promotion-system`: Discount code management for system-wide or company-specific promotions

### Modified Capabilities
- None (this is the initial implementation of core domain entities)

## Impact

- Database: New tables for all entities and junction tables
- Code: Updated AppDbContext, new entity classes in App.Domain
- Authentication: New roles added to seeding for role-based access
- Architecture: Maintains layered architecture (WebApp → App.BLL → App.DAL.EF → App.Domain)
- Dependencies: No new external dependencies required