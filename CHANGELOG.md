# Changelog

## 2.0.1 - 2026-08-31

- Registered API Connections and sanitized service/environment readiness with Deucarian Control Center.
- Moved raw JSON logging from the global menu into a confirmation-marked Developer action.
- Removed the package-owned global API menu and updated the shared editor dependency to 1.2.0.

## 2.0.0 - 2026-08-26

- Updated the exact Logging dependency to 1.0.3 for the coordinated editor UX
  release.
- Replaced `ApiConnectionProfile` with the single project-owned
  `ApiConnectionSettings` concept.
- Added package-owned `ApiServiceDefinition` contracts with typed service,
  environment, client, catalog, and endpoint identities plus source provenance.
- Removed normal asset menus for internal environment and endpoint building
  blocks; integrations now create complete service-aware settings explicitly.
- Removed duplicated known-environment metadata from project connection assets.

## 1.5.0 - 2026-08-24

- Added a global JSON property-naming dropdown with `snake_case` as the default.
- Added optional endpoint-level naming overrides for camelCase, PascalCase,
  kebab-case, SCREAMING_SNAKE_CASE, and as-declared contracts.
- Preserved explicit `JsonProperty` names and dictionary keys across all policies.

## 1.4.2 - 2026-08-19

- Prevented both normal and Advanced connection-profile controls from editing
  referenced environment assets that are not owned by the current project.
- Added a clear read-only state for package-managed or transient environment
  references while retaining manual project-owned composition.

## 1.4.1 - 2026-08-19

- Updated the guided connection-profile inspector to render every configured
  named client instead of assuming integrations use the generic `primary` ID.
- Kept the single-client layout minimal while labeling multi-client hosts with
  their stable client IDs.

## 1.4.0 - 2026-08-19

- Added the project-facing `ApiConnectionProfile` aggregate with serializable
  known-environment metadata and composition helpers.
- Added one guided Connection Profile creation workflow that produces four blank
  conventional environment slots without inventing hosts or a catalog.
- Added a focused connection-profile inspector for catalog ownership, environment
  status, Base URL editing, and advanced manual identifiers and policies.
- Moved raw Client Config, Environment Profile, Endpoint Catalog, and Endpoint
  Definition creation commands under the Advanced/Building Blocks API submenu
  without changing their public types or serialized asset compatibility.

## 1.3.0 - 2026-08-19

- Added vendor-neutral Development, Testing, Acceptance, and Production stage
  metadata without assigning vendor hosts.
- Added sanitized Configured, Unconfigured, and Unknown environment status.
- Added opt-in known-environment composition so intentionally blank profile
  slots fail closed without preventing configured environments from resolving.
- Kept legacy composition constructors strict and continued rejecting malformed
  or partially configured profiles.

## 1.2.0 - 2026-08-19

- Added serializable stable IDs for environments, named clients, endpoint catalogs,
  and endpoints.
- Added vendor-neutral environment profiles with named client/base-URL resolution.
- Added endpoint catalogs with stable endpoint IDs, relative route templates, HTTP
  metadata, headers, query defaults, and policy overlays.
- Added explicit `ApiComposition`, resolved client/endpoint models, and sanitized
  environment status for connection UI without global environment state.
- Added layered request policy values for timeout, retry/backoff, and rate-limit hints;
  the existing transport now applies a resolved policy timeout without changing legacy
  timeout precedence.
- Kept token acquisition, refresh, expiry, and session lifecycle outside this package
  behind the existing `IApiAuthProvider` boundary.

## 1.1.6 - 2026-08-18

- Routed the package-owned API diagnostics menu through the shared
  `DeucarianEditorUxStandards.MenuRoot` convention.

## 1.1.5 - 2026-08-18

- Added per-request logging suppression for credential and token exchanges so
  sensitive URLs, response bodies, and transport errors never reach API logs.
- Declared the canonical Editor dependency required by the package-owned API
  diagnostics menu.

## 1.1.4 - 2026-07-17

- Documented the Example Scene sample and aligned the exact Logging dependency for the portfolio release.
- Aligned expected API error-log coverage with the canonical Deucarian Logging console format.

## 1.1.3 - 2026-06-22

- Updated the exact `com.deucarian.logging` dependency to `1.0.1`.

## 1.1.2 - 2026-06-22

- Accepted the public release automation state for `com.deucarian.api` 1.1.2 on develop.

## 1.1.1 - 2026-06-22

- Promoted the prepared API 1.1.1 release metadata into develop.

## 1.1.0 - 2026-06-19

- Added `ApiResponseFormat.AssetBundle` with automatic `AssetBundle` response detection.
- Added `ApiAssetBundleRequestOptions` for CRC and Unity AssetBundle cache metadata.
- Added API transfer progress callbacks with normalized progress and byte counts.
- Routed AssetBundle responses through `UnityWebRequestAssetBundle` and `DownloadHandlerAssetBundle` without reading `downloadHandler.data`.

## 1.0.2 - 2026-06-17

- Renamed Session API package documentation from bridge terminology to integration terminology.

## 1.0.1 - 2026-06-15

- Improved README structure for overview, core concepts, public API, samples, integrations, versioning, and limitations.
- Moved the raw JSON debug toggle under `Tools > Deucarian > API`.

## 1.0.1 - 2026-06-15

- Standardized package logging on com.deucarian.logging.
- Added `ApiLog` package categories and removed the internal API logger abstraction.

## 1.0.0 - 2026-06-03

- Converted APIHelper into a standalone Unity Package Manager Git package.
- Migrated the refactored `IApiClient`-based APIHelper runtime, editor code,
  tests, documentation, and sample scene from the HoloHelmet project.
- Added package metadata, release-channel documentation, and GitHub Actions
  validation.
- Kept legacy `ApiServices` compatibility wrappers with obsolete migration
  guidance.
