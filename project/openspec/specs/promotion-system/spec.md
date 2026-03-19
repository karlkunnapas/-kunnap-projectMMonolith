## Purpose

Define promotion persistence and assignment requirements for discount workflows.

## Requirements

### Requirement: Manage promotion definitions
The system SHALL persist promotions with code, discount value, validity period, and active state.

#### Scenario: Promotion is created
- **GIVEN** a valid promotion payload
- **WHEN** the promotion is saved
- **THEN** the system stores commercial terms and validity dates

### Requirement: Support tenant-scoped and user-assigned promotions
The system SHALL support optional company scoping and user assignment for promotions.

#### Scenario: Promotion is linked to company and users
- **GIVEN** a promotion is company-specific and assigned to users
- **WHEN** links are persisted
- **THEN** the system stores company association and `UserPromotion` mappings

