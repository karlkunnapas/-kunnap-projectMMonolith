## Why

The platform needs traceability for data changes across tenants. Without an audit trail, administrators cannot reliably answer who changed business data, what was changed, and when the change happened.

## What Changes

- Add a new `AuditLog` domain entity for immutable audit records.
- Scope each audit record to a company using `CompanyId`.
- Record actor identity as `UserName` instead of `UserId` to keep historical readability even if user records are removed.
- Store audit metadata (`EntityName`, `EntityId`, `Action`, `AtUtc`) and optional JSON change payload (`ChangesJson`).
- Add `AuditLogs` to `AppDbContext` and company navigation for tenant-centric querying.

## Capabilities

### New Capabilities

- `audit-logging`: Persist per-company audit trail entries with actor, target entity, action, timestamp, and optional serialized field deltas.

## Impact

- Database: New `AuditLogs` table with company relationship and query indexes.
- Domain model: New `AuditLog` entity and `Company.AuditLogs` navigation.
- Application behavior: Infrastructure can now persist normalized audit records for create/update/delete operations.

