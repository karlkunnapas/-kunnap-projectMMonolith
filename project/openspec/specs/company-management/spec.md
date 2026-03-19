## Purpose

Define company and user-company mapping requirements for multi-tenant ownership.

## Requirements

### Requirement: Manage company entities for multi-tenancy
The system SHALL persist companies with localized name, contact details, and active state.

#### Scenario: Company profile is created
- **GIVEN** a valid company profile
- **WHEN** the profile is saved
- **THEN** the system stores company identity and contact metadata

### Requirement: Map users to companies
The system SHALL support many-to-many user-company assignment.

#### Scenario: User-company membership is stored
- **GIVEN** a user is associated with a company
- **WHEN** membership is saved
- **THEN** the system stores an `AppUserCompany` mapping

