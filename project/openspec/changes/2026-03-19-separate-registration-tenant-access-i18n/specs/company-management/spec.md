## MODIFIED Requirements

### Requirement: Manage company entities for multi-tenancy
The system SHALL persist companies with localized name, contact details, active state, and a unique tenant slug for route-based resolution.

#### Scenario: Company profile includes route slug
- **GIVEN** a valid company profile and a requested tenant slug
- **WHEN** company registration is completed
- **THEN** the system stores the company with that slug
- **AND** the slug is unique for tenant route resolution

### Requirement: Map users to companies
The system SHALL support user-company membership and enforce membership on company-scoped routes.

#### Scenario: Company owner membership is created during company registration
- **GIVEN** a new company owner account is registered
- **WHEN** company registration succeeds
- **THEN** the system creates an `AppUserCompany` membership for the owner
- **AND** the owner can access routes scoped to that company slug

#### Scenario: Cross-company access is denied
- **GIVEN** an authenticated user belongs to Company A
- **AND** the request targets Company B tenant route
- **WHEN** the user accesses company-scoped data
- **THEN** the request is denied

#### Scenario: Unknown company slug returns not found
- **GIVEN** a request targets a tenant slug that does not exist
- **WHEN** tenant resolution runs
- **THEN** the request returns `404 Not Found`

#### Scenario: Inactive company is blocked
- **GIVEN** a request targets an inactive company slug
- **WHEN** tenant resolution runs
- **THEN** the request returns `403 Forbidden`

