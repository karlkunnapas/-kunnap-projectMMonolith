## ADDED Requirements

### Requirement: Per-company audit trail records
The system SHALL persist immutable audit trail entries for entity state changes, scoped to a company.

#### Scenario: Audit entry contains company scope and action metadata
- **GIVEN** a business entity change is persisted
- **WHEN** an audit trail entry is written
- **THEN** it includes `CompanyId`, `EntityName`, `EntityId`, `Action`, and `AtUtc`

### Requirement: Actor is captured by username
The system SHALL capture the actor identity as username instead of user id.

#### Scenario: Username is stored as actor identity
- **GIVEN** a user performs a data-changing operation
- **WHEN** an audit record is created
- **THEN** the record stores `UserName`
- **AND** it does not require a `UserId` foreign key

### Requirement: Changes payload supports detailed diff storage
The system SHALL support storing serialized field-level changes for auditable operations.

#### Scenario: Optional changes payload is present
- **GIVEN** a create, update, or delete operation produces a change payload
- **WHEN** the audit entry is saved
- **THEN** the payload is persisted in `ChangesJson`
- **AND** the field accepts null when no payload is available

