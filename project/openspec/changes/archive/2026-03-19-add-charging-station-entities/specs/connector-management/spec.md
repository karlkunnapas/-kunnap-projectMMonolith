## ADDED Requirements

### Requirement: Manage connector type catalog
The system SHALL persist connector types with localized name and active state.

#### Scenario: Connector type is defined
- **GIVEN** an administrator defines a connector type
- **WHEN** connector data is persisted
- **THEN** the system stores the connector record for reuse across vehicles and stations

### Requirement: Map station connectors
The system SHALL support many-to-many mapping between stations and connectors.

#### Scenario: Station connector set is stored
- **GIVEN** a station supports one or more connector types
- **WHEN** station connector links are saved
- **THEN** the system stores `ChargingStationConnector` mappings

