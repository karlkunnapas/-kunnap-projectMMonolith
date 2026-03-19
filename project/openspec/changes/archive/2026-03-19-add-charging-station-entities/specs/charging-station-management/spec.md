## ADDED Requirements

### Requirement: Manage charging station inventory
The system SHALL persist charging stations with identity, location, status, pricing, power limits, and active state.

#### Scenario: Create a charging station
- **GIVEN** an operator provides valid station data
- **WHEN** the station is saved
- **THEN** the system stores station metadata and operational configuration

### Requirement: Support optional station company ownership
The system SHALL support tenant ownership for stations using an optional `CompanyId`.

#### Scenario: Company-linked station
- **GIVEN** a charging station belongs to a company
- **WHEN** the station record is persisted
- **THEN** the station stores the associated `CompanyId`

