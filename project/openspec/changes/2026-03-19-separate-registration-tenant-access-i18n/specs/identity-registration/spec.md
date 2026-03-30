## ADDED Requirements

### Requirement: Company owner registration is tenant onboarding
The system SHALL provide a dedicated company registration flow that creates both company tenant and owner identity.

#### Scenario: Company registration creates company and owner membership
- **GIVEN** a valid company registration request with owner credentials and company details
- **WHEN** the registration is submitted
- **THEN** the system creates the user account
- **AND** creates the company tenant
- **AND** stores owner membership in `AppUserCompany`
- **AND** assigns company owner privileges

#### Scenario: Duplicate company slug is rejected
- **GIVEN** a company slug already exists
- **WHEN** a new company registration uses the same slug
- **THEN** the registration fails with a validation error

### Requirement: Customer registration is tenant-agnostic
The system SHALL provide a separate customer registration flow that does not create or require company membership.

#### Scenario: Customer registration creates standalone account
- **GIVEN** a valid customer registration request
- **WHEN** the registration is submitted
- **THEN** the system creates the user account
- **AND** does not create a company
- **AND** does not create an `AppUserCompany` mapping

#### Scenario: Customer registration endpoint works without tenant slug
- **GIVEN** a request to global customer registration route
- **WHEN** no tenant slug is present in the URL
- **THEN** registration flow remains available and valid

### Requirement: Legacy identity registration route redirects to customer registration
The system SHALL keep backward compatibility by redirecting built-in identity registration path to the custom customer registration path.

#### Scenario: Built-in identity register path is redirected
- **GIVEN** a request to `/Identity/Account/Register`
- **WHEN** middleware processes the request
- **THEN** the response redirects to `/Account/Register`

