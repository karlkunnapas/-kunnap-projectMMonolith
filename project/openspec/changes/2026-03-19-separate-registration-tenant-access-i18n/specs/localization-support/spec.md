## ADDED Requirements

### Requirement: Registration pages support English and Estonian
The system SHALL provide localized registration text through existing `LangStr` translations, supporting English (`en`) and Estonian (`et`).

#### Scenario: Customer registration page is localized
- **GIVEN** the current UI culture is `en` or `et`
- **WHEN** the customer registration page is rendered
- **THEN** labels, help text, buttons, and validation messages are resolved from `LangStr` in the selected language

#### Scenario: Company registration page is localized
- **GIVEN** the current UI culture is `en` or `et`
- **WHEN** the company registration page is rendered
- **THEN** labels, help text, buttons, and validation messages are resolved from `LangStr` in the selected language

### Requirement: Tenant access status messages are localized
The system SHALL resolve tenant resolution status messages for unknown and inactive companies from `LangStr` translations.

#### Scenario: Unknown tenant message follows selected culture
- **GIVEN** a tenant slug is not found
- **AND** the current UI culture is `en` or `et`
- **WHEN** tenant resolution fails
- **THEN** the not-found message is returned from `LangStr` in the selected language

#### Scenario: Inactive tenant message follows selected culture
- **GIVEN** a tenant slug resolves to an inactive company
- **AND** the current UI culture is `en` or `et`
- **WHEN** tenant resolution blocks access
- **THEN** the forbidden message is returned from `LangStr` in the selected language

### Requirement: LangStr translation lookup follows existing fallback order
The system SHALL use existing `LangStr.Translate()` precedence: exact culture key, then neutral culture key, then `LangStr.DefaultCulture`.

#### Scenario: Neutral culture fallback is applied
- **GIVEN** a translation contains `et` but not `et-EE`
- **AND** the current UI culture is `et-EE`
- **WHEN** text is resolved through `LangStr.Translate()`
- **THEN** the `et` translation is returned

#### Scenario: Fallback culture is applied
- **GIVEN** a translation does not contain the exact or neutral culture key
- **WHEN** text is resolved through `LangStr.Translate()`
- **THEN** the system returns translation value from `LangStr.DefaultCulture`


