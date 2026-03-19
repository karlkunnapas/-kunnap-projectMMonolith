## ADDED Requirements

### Requirement: Manage user vehicle profiles
The system SHALL persist user vehicles with make, model, and battery capacity data.

#### Scenario: Vehicle profile is stored
- **GIVEN** a user adds a vehicle
- **WHEN** vehicle data is saved
- **THEN** the system stores vehicle details linked to the user

### Requirement: Represent vehicle connector compatibility
The system SHALL support many-to-many mapping between vehicles and connector types.

#### Scenario: Vehicle is linked to connector types
- **GIVEN** a vehicle supports one or more connectors
- **WHEN** compatibility links are saved
- **THEN** the system stores `VehicleConnector` mappings

