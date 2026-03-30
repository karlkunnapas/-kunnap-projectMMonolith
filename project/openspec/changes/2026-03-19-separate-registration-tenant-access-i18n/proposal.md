## Why

The current registration experience mixes company onboarding and regular customer signup. This creates unclear UX, weak tenant boundaries on company routes, and inconsistent behavior when non-company users access tenant-prefixed URLs.

The system needs explicit separation:
- company owner registration creates and owns a company tenant,
- customer registration creates a standalone user without company membership,
- company-scoped pages enforce tenant resolution and membership checks,
- registration and tenant-facing messages support two languages (English and Estonian) via existing `LangStr` translations.

## What Changes

- Split registration into two explicit flows:
  - Company registration (tenant-aware, creates company + owner membership)
  - Customer registration (tenant-agnostic, no company link)
- Update tenant route handling to protect company routes and ensure only users belonging to the resolved company can access company data.
- Keep global account/customer endpoints working without tenant slug and without tenant filters.
- Add localization requirements for both registration flows in English and Estonian using the existing `LangStr` model (`Translate`, neutral-culture lookup, `DefaultCulture` fallback).

## Capabilities

### Modified Capabilities

- `company-management`: clarify tenant membership enforcement for company-scoped routes and separate company registration expectations.

### New Capabilities

- `identity-registration`: define dedicated company owner vs customer registration behavior.
- `localization-support`: define bilingual (en, et) requirements for registration and tenant-access messages.

## Impact

- Web: separate controllers/views/areas for company and customer registration.
- Identity/BLL: distinct DTOs and service methods for company owner registration and customer registration.
- Tenant security: middleware + authorization checks must block cross-company access.
- Data model: customer users remain without company membership; company users are linked via `AppUserCompany`.
- Testing: integration and unit coverage for both flows, tenant isolation, and `LangStr` translation/fallback behavior.


