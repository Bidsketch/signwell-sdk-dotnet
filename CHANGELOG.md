# Changelog

## 0.2.0-beta.1 — Unreleased

### Added

- Support the current documented SMS delivery fields, conditional rules and decisions, and embedded signing/branding options.
- Expose 21 CFR Part 11 indicators in document responses and updated passcode-delivery settings.

### Changed

- Refresh generated resources, request/response models, and API documentation from the current published SignWell API.
- Regenerate with OpenAPI Generator 7.26.0 while preserving existing minimum runtimes.
- Standardize the default SDK User-Agent; callers can continue to configure their own value.
- Represent authenticated-user and active-user IDs as strings so legacy numeric IDs deserialize correctly. Membership, account, and contact IDs retain their UUID types.

### Dependencies

- Update Microsoft runtime packages, SourceLink, the test SDK, and xUnit to current compatible releases.
- Update Polly to 8.8 while retaining its supported v7 policy API and existing retry behavior.

### Migration

- Code that explicitly uses `Guid` for user IDs should use `string`. This does not change membership, account, or contact ID types.

- Use named arguments for optional `RecipientsInner` constructor values (for example, `email: address`); SMS support makes email optional and changes its parameter position.
