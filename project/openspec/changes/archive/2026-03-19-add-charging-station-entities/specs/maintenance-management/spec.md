## ADDED Requirements

### Requirement: Track station maintenance issues
The system SHALL persist maintenance issues for charging stations with status and service lifecycle timestamps.

#### Scenario: Maintenance issue is reported and resolved
- **GIVEN** a station fault is reported
- **WHEN** maintenance data is updated through resolution
- **THEN** the system stores issue details, assigned user, report time, and resolution time

