## 1. Implementation

- [x] 1.1 Add separate registration contracts and service methods for company owner and customer flows.
- [x] 1.2 Implement company registration UI in a dedicated company area and keep customer registration in global account endpoints.
- [x] 1.3 Update tenant access checks so company-scoped requests require a resolved tenant and active user membership in that company.
- [x] 1.4 Ensure customer endpoints bypass tenant requirements and do not apply company filters.
- [x] 1.5 Add/adjust role assignment and membership creation (`CompanyOwner`/`Customer`, `AppUserCompany`) for new flows.
- [x] 1.6 Add/adjust schema constraints or migrations if needed (slug uniqueness, membership integrity).
- [x] 1.7 Implement English/Estonian registration and tenant messages using existing `LangStr` values (no separate localization mechanism for this scope).

## 2. Security and Validation

- [x] 2.1 Block access to company-scoped routes when tenant slug is invalid or inactive.
- [x] 2.2 Block authenticated users from accessing another company's scoped data.
- [x] 2.3 Validate company registration input for unique slug and required owner account fields.

## 3. Testing

- [x] 3.1 Integration test: company registration creates company + owner membership.
- [x] 3.2 Integration test: customer registration creates user without company membership.
- [x] 3.3 Integration test: cross-company access is denied for company user.
- [x] 3.4 Integration test: customer endpoints function without tenant slug.
- [x] 3.5 Unit tests: tenant middleware branching (reserved segments, unknown slug, inactive company, resolved tenant).
- [x] 3.6 Localization tests: `LangStr.Translate()` resolves exact culture, neutral culture (for example `et-EE` -> `et`), and `LangStr.DefaultCulture` fallback for registration/tenant messages.

## 4. Spec

- [x] 4.1 Add company-management requirement deltas for route and membership enforcement.
- [x] 4.2 Add identity-registration capability with separate company/customer registration requirements.
- [x] 4.3 Add localization-support capability for two-language registration UX via `LangStr` behavior.





