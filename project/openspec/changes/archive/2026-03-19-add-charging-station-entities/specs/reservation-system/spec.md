## ADDED Requirements

### Requirement: Reserve charging time slots
The system SHALL persist reservations that link a user to a charging station and a reserved time window.

#### Scenario: Reservation is stored
- **GIVEN** a user selects a station and time range
- **WHEN** the reservation is created
- **THEN** the system stores user, station, start time, end time, and reservation status

