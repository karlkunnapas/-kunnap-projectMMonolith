## Purpose

Define charging session persistence requirements for operational tracking and billing.

## Requirements

### Requirement: Track charging sessions
The system SHALL persist charging sessions with user, station, start/end timestamps, energy consumed, and total cost.

#### Scenario: Charging session lifecycle is recorded
- **GIVEN** a charging session is started and later completed
- **WHEN** session data is persisted
- **THEN** the system stores the session timeline and billing-related metrics

