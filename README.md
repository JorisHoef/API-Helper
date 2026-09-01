# Deucarian API

## Overview

Deucarian API is a reusable Unity/C# API client package built around `IApiClient`.
It wraps UnityWebRequest behind a small, testable client surface and supports
developer-friendly code endpoints, designer-friendly ScriptableObject endpoints,
structured error handling, authentication providers, cancellation, timeouts, and
safe certificate defaults.

The package is intentionally practical: use raw string routes for simple calls,
`ApiEndpoint` for richer code-defined endpoints, `ApiEndpointDefinition` assets
for non-developer configuration, and `ApiRequest` only for advanced one-off
requests.

Package ID: `com.deucarian.api`

## When To Use This

Use this package when a Unity project needs reusable HTTP/API transport, request building, response parsing, JSON/text/byte/texture responses, explicit environment selection, named clients, ScriptableObject endpoint catalogs, authentication providers, cancellation-aware calls, and Deucarian Logging-backed API diagnostics.

Do not use this package to own session lifecycle, object-loading source resolution, package installation, gameplay runtime behavior, or Unity object lifetime cleanup. Those capabilities belong to their owning Deucarian packages.

## Core Concepts

- `IApiClient` as the main injectable dependency.
- Safe TLS behavior by default. Certificate bypass is only available as an
  explicit development-only option.
- Central `ApiClientConfig` ScriptableObject for base URL, headers, timeout,
  authentication, JSON settings, certificate handling, and logging.
- Stable serializable environment, client, catalog, and endpoint identifiers.
- A first-class Local environment stage, distinct from Custom and ordered before
  the four conventional remote deployment stages.
- `ApiEnvironmentProfile` assets that map named clients to environment-specific base URLs.
- `ApiEndpointCatalog` assets that keep stable route IDs and HTTP metadata independent of hosts.
- `ApiComposition` for explicit environment resolution without active global state.
- Raw string endpoint workflow for route constants.
- Lightweight `ApiEndpoint` workflow for method/auth/timeout/header/query
  defaults in code.
- `ApiEndpointDefinition` ScriptableObject workflow for designers and
  non-developers.
- Advanced `ApiRequest` for custom headers, query parameters, body format, and
  timeout overrides.
- First-class response support for JSON DTOs, `string`, `byte[]`, and
  `Texture2D`.
- Raw text, raw byte, JSON, and multipart request body formats.
- Structured `ApiResult<T>` and `ApiError` responses.
- CancellationToken support.
- Legacy `ApiServices` facade for older call sites.

## Installation

API is distributed as a Unity Package Manager Git package.

Add one of these entries to your Unity project's `Packages/manifest.json`.

Stable branch:

```json
"com.deucarian.api": "https://github.com/Deucarian/API.git#main"
```

Beta branch:

```json
"com.deucarian.api": "https://github.com/Deucarian/API.git#develop"
```

Release channels:

- `main` is the stable channel.
- `develop` is the beta channel.
- This local repo currently has no stable release tag. When release tags are published, use the tag name as the Git ref for immutable installs.

The package expects Unity 2021.3 or newer and declares dependencies on
`com.deucarian.logging`, `com.unity.nuget.newtonsoft-json`, plus Unity's built-in UnityWebRequest modules.
Unity may pin Git package commits in `Packages/packages-lock.json`; to update,
open Package Manager and update the package, remove the lock entry, or change
the Git ref in `manifest.json`.

## Logging

This package uses `com.deucarian.logging`.

Deucarian API diagnostics use stable package categories: `Api`, `Api.Requests`, `Api.Authentication`, `Api.Certificates`, and `Api.Samples`. Configure Deucarian Logging filters by category and level to show request, authentication, certificate, or sample output independently. Entries also flow through the shared ring buffer for recent-diagnostic inspection and remain compatible with future telemetry sinks.

## Package Layout

```text
API/
  package.json
  README.md
  CHANGELOG.md
  LICENSE.md
  CONTRIBUTING.md
  Runtime/
  Editor/
  Tests/Editor/
  Samples~/ExampleScene/
```

Assembly definitions use assembly-name references instead of GUID references for
readability and package portability. The runtime assembly has no editor-only
references, the editor and test assemblies are Editor-only, and samples are kept
in their own `API.Samples` assembly.

## Public API

The main runtime APIs are:

- `IApiClient`: injectable client used by application services.
- `ApiClientFactory`: creates the default client pipeline from `ApiClientConfig`.
- `ApiClientConfig`: ScriptableObject config for base URL, default headers, timeout, auth, JSON settings, certificate handling, response format, and logging.
- `ApiConnectionSettings`: project-owned ScriptableObject containing deployment hosts and safe policy overrides for one service definition.
- `ApiServiceDefinition`: package-owned, credential-free service identity, environment/client contract, endpoint catalog, and source provenance.
- `ApiEnvironmentId`, `ApiClientId`, `ApiServiceId`, `ApiCatalogId`, and `ApiEndpointId`: stable serializable identifiers used across package boundaries.
- `ApiEnvironmentProfile`: managed connection sub-asset with named-client base URLs and policy defaults.
- `ApiEndpointCatalog`: definition-owned stable endpoint IDs, relative route templates, methods, auth requirements, and sensitive-call logging suppression.
- `ApiComposition`: resolves an explicit environment and endpoint ID into the existing `ApiEndpoint`/`ApiRequest` pipeline.
- `ApiEnvironmentStatus`: sanitized resolution state for UI that intentionally omits hosts and headers.
- `ApiRequestPolicy` and `ApiRequestPolicyDefinition`: resolved policy values and serializable layered overrides.
- `IApiAuthProvider` and `ApiAuthProviderAsset`: code and ScriptableObject token providers.
- `ApiEndpoint`: code-defined endpoint with method, auth, timeout, headers, query parameters, path parameters, and response format.
- `ApiEndpointDefinition`: ScriptableObject endpoint asset that converts to `ApiEndpoint`.
- `ApiRequest`: advanced one-off request model.
- `ApiResult<T>` and `ApiError`: success/failure result model.
- `ApiServices`: legacy/convenience static facade over a configured `IApiClient`.

Most new code should depend on `IApiClient`, create it with `ApiClientFactory.Create`, and handle `ApiResult<T>` instead of catching transport exceptions for normal API failures.

## Samples

API includes lightweight learning assets under:

`Samples~/ExampleScene`

- `ExampleScene/APIExample.unity`: scene with an `ApiExampleSceneController`
  wired to sample config and endpoint assets.
- `ExampleScene/ExampleApiClientConfig.asset`: config using
  `https://jsonplaceholder.typicode.com`, safe certificate defaults, and verbose
  sample logging.
- `ExampleScene/ExampleGetPostEndpoint.asset`: ScriptableObject GET endpoint.
- `ExampleScene/ExampleCreatePostEndpoint.asset`: ScriptableObject POST endpoint.
- `ExampleScene/ExampleAuthProvider.cs`: sample-only auth provider showing how a
  project can return a bearer token.

To import the sample, open Unity Package Manager, select `API`, expand
`Samples`, and choose `Import` for `API Example Scene`. Then open
`APIExample.unity`, select the `API Example` GameObject, and use the
component context menu `Run Example Requests`. The sample calls require internet
access.

## Quick Start

Create a config asset:

`Assets > Create > Deucarian > API > Advanced > Building Blocks > Client Config`

Set:

- `Base Url`: for example `https://api.example.com/api/v1`
- `Timeout Seconds`: for example `30`
- `Certificate Handling Mode`: `Default Validation`
- `Authentication Mode`: `None` for public APIs, or `Bearer Token` when using an auth provider

Create a client:

```csharp
using System.Threading;
using System.Threading.Tasks;
using Deucarian.API.Core;
using Deucarian.API.Configuration;
using Deucarian.API.Models;

public sealed class ProjectService
{
    private readonly IApiClient apiClient;

    public ProjectService(ApiClientConfig config)
    {
        apiClient = ApiClientFactory.Create(config);
    }

    public Task<ApiResult<ProjectDto[]>> ListProjectsAsync(CancellationToken cancellationToken)
    {
        return apiClient.GetAsync<ProjectDto[]>("projects", cancellationToken);
    }
}
```

Handle the result:

```csharp
ApiResult<ProjectDto[]> result = await apiClient.GetAsync<ProjectDto[]>("projects", cancellationToken);

if (result.IsSuccess)
{
    ApiLog.Requests.Info("Loaded projects: " + result.Data.Length);
}
else
{
    ApiLog.Requests.Error(result.Error.Message);
}
```

## Configuration

`ApiClientConfig` is the central runtime configuration.

| Setting | Purpose |
| --- | --- |
| `BaseUrl` | Prepended to relative endpoints. Absolute URLs are used as-is. |
| `DefaultHeaders` | Headers applied to every request unless overridden. |
| `TimeoutSeconds` | Default request timeout. `0` lets UnityWebRequest use its default behavior. |
| `AuthenticationMode` | Default auth mode for requests using `UseConfigDefault`. |
| `AuthProvider` | Optional ScriptableObject token provider. |
| `JsonSerializerSettings` | Newtonsoft JSON options, including the global property-naming dropdown. Defaults to `snake_case`; explicit `JsonProperty` names always win. |
| `DefaultResponseFormat` | Response format fallback. Keep `Auto` for most projects. |
| `CertificateHandlingMode` | TLS/certificate behavior. `DefaultValidation` is the production default. |
| `LoggingMode` | Controls API logging. |
| `LogRawJson` | Logs successful JSON response bodies when enabled. |

```csharp
IApiClient apiClient = ApiClientFactory.Create(apiClientConfig);
```

For tests or small integrations, a runtime config is also possible:

```csharp
ApiClientConfig config = ApiClientConfig.CreateRuntimeDefault();
config.BaseUrl = "https://api.example.com/api/v1";
config.TimeoutSeconds = 30;

IApiClient apiClient = ApiClientFactory.Create(config);
```

Keep `DefaultResponseFormat` on `Auto` unless an entire client should default to
a non-JSON DTO-like response format. `Auto` maps DTOs to JSON, `string` to text,
`byte[]` to bytes, and `Texture2D` to textures. A global non-`Auto` value applies
to DTO-like calls whose request or endpoint still uses `Auto`, so prefer
per-request or per-endpoint `ResponseFormat` overrides for files, text endpoints,
and images.

Control Center reports only service-binding and configured/unconfigured environment counts. It never exposes host URLs, headers, credentials, or response payloads. Advanced raw-JSON logging is available only as a confirmation-marked Developer action.

## Environment And Endpoint Composition

`ApiServiceDefinition` owns the credential-free contract: service identity,
known environments, required named clients, endpoint catalog, and source
fingerprint. `ApiConnectionSettings` owns only project deployment hosts and safe
policy overrides. It stores no credential or active environment.

Create settings through an installed integration's explicit setup action, for
example `Assets > Create > Deucarian > Connections > Simultria Connection
Settings`, or through the **API Connections** card in `Tools > Deucarian > Control Center...`. The integration
supplies its service definition and the factory creates every required managed
environment/client slot with blank hosts. Missing hosts remain visible as
**Missing** and fail closed.

Environment profiles and endpoint catalogs have no normal asset creation menu.
Custom integrations may construct them through their own advanced tooling, but
ordinary project setup never asks developers to author internal building blocks.

An environment profile can define clients such as `primary`, `media`, or `telemetry`.
Every environment uses the same client IDs but supplies its own base URLs. A catalog
entry references one client ID and provides a relative route template such as
`projects/{id}`.

Integration packages can declare a standard ordered environment set with
`ApiEnvironmentDescriptor` without supplying any hosts. With the descriptor-aware
`ApiComposition` overload, a profile whose client hosts are all blank is reported as
`Unconfigured` and cannot resolve traffic. A partially filled or malformed profile
remains invalid. Existing constructors retain their strict configured-profile
requirement.

`ApiEnvironmentStage.Local` is a built-in package contract, not a Custom stage.
Its serialized value is additive so the existing `Custom`, `Development`,
`Testing`, `Acceptance`, and `Production` values remain unchanged.
`ApiEnvironmentStages.All` exposes the user-facing order Local, Development,
Testing, Acceptance, Production. `ApiEnvironmentStages.Standard` remains the
four conventional remote stages for source compatibility. A Local profile may
have blank hosts; it then remains visibly unconfigured and cannot silently route
traffic through Development or any other environment. Existing settings assets
created before a package adds an environment remain valid: the new package-owned
environment appears as unconfigured, and **Add Missing Connection Slots** adds a
blank project-owned slot without changing existing hosts.

```csharp
ApiComposition composition = new ApiComposition(
    new[] { developmentEnvironment, productionEnvironment },
    buildingApiCatalog);

ApiResolvedEndpoint resolved = composition.ResolveEndpoint(
    new ApiEnvironmentId("development"),
    new ApiEndpointId("projects.get"));

ApiEndpoint endpoint = resolved.Endpoint.WithPathParameter("id", projectId);
ApiResult<ProjectDto> result = await apiClient.SendAsync<ProjectDto>(
    endpoint,
    cancellationToken);
```

With the project-facing aggregate, the usual call is simply:

```csharp
ApiComposition composition = connectionProfile.CreateComposition();
```

The resolved endpoint uses an absolute URL, so it travels through the existing
`IApiClient` pipeline without changing old `ApiClientConfig`, raw-string, or
`ApiEndpoint` workflows. Named-client headers are applied first and endpoint headers
override them.

For connection UI, use the sanitized status object:

```csharp
ApiEnvironmentStatus status = composition.GetEnvironmentStatus(selectedEnvironmentId);
environmentLabel.text = status.IsResolved ? status.DisplayName : "Environment unavailable";
```

`ApiEnvironmentStatus` deliberately excludes base URLs and headers. A viewer or
integration package may serialize `ApiEnvironmentId` directly in its own
ScriptableObject; API never stores or changes an active selection globally.

### Request Policies

Policy definitions layer in this order:

1. Package-safe defaults.
2. Environment defaults.
3. Named-client overrides.
4. Endpoint overrides.

Values cover timeout, maximum retry attempts, exponential backoff, and advisory
rate-limit window hints. The built-in UnityWebRequest transport applies the resolved
timeout. Retry/backoff and rate-limit values are metadata for policy-aware service
decorators or schedulers; API does not silently retry requests, which preserves
existing behavior and avoids replaying non-idempotent operations.

Authentication is orthogonal to environment composition. Catalog entries select an
existing `ApiAuthenticationRequirement`, while an external `IApiAuthProvider` supplies
tokens. Environment assets and endpoint catalogs must never store credentials.

## Authentication

Authentication is provided through `IApiAuthProvider`. The provider returns the
token only; API creates the `Authorization: Bearer ...` header. The
following example is project code; API does not include `ISessionService`.

```csharp
public sealed class ProjectSessionAuthProvider : IApiAuthProvider
{
    private readonly ISessionService sessionService;

    public ProjectSessionAuthProvider(ISessionService sessionService)
    {
        this.sessionService = sessionService;
    }

    public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(sessionService.GetActiveSession()?.AccessToken);
    }
}

IApiClient apiClient = ApiClientFactory.Create(
    apiClientConfig,
    new ProjectSessionAuthProvider(sessionService));
```

For designer-assigned auth providers, derive from `ApiAuthProviderAsset` and
assign it to the `ApiClientConfig` asset.

Requests and endpoints can choose:

- `UseConfigDefault`: follow `ApiClientConfig.AuthenticationMode`.
- `Required`: token must be available.
- `Optional`: token is used if available.
- `Disabled`: never attach auth.

## Integrations

API does not depend on the other Deucarian runtime packages.

Other packages or project code can integrate by implementing `IApiAuthProvider`. Session token support lives in the separate `com.deucarian.session.api-integration` package, not in API. No scripting define symbol is required for that integration.

## String Endpoint Workflow

Use string constants for simple routes. This is the most compact workflow and
works well with IDE search/refactoring.

```csharp
public static class ApiRoutes
{
    public const string Login = "auth/login";
    public const string Projects = "projects";
}

ApiResult<ProjectDto[]> result =
    await apiClient.GetAsync<ProjectDto[]>(ApiRoutes.Projects, cancellationToken);
```

POST with body:

```csharp
var request = new CreateProjectRequest { Name = "Site A" };

ApiResult<ProjectDto> result =
    await apiClient.PostAsync<ProjectDto>(ApiRoutes.Projects, request, cancellationToken);
```

## ApiEndpoint Workflow

Use `ApiEndpoint` when method, auth, timeout, headers, or query parameters should
travel with the endpoint definition.

```csharp
public static class ProjectApiEndpoints
{
    public static readonly ApiEndpoint GetAll =
        new ApiEndpoint("projects", HttpMethod.GET, ApiAuthenticationRequirement.Required);

    public static readonly ApiEndpoint Create =
        new ApiEndpoint("projects", HttpMethod.POST, ApiAuthenticationRequirement.Required);

    public static ApiEndpoint GetById(string projectId) =>
        new ApiEndpoint("projects/{id}", HttpMethod.GET, ApiAuthenticationRequirement.Required)
            .WithPathParameter("id", projectId);
}
```

Usage:

```csharp
ApiResult<ProjectDto[]> projects =
    await apiClient.SendAsync<ProjectDto[]>(ProjectApiEndpoints.GetAll, cancellationToken);

ApiResult<ProjectDto> created =
    await apiClient.SendAsync<ProjectDto>(
        ProjectApiEndpoints.Create,
        new CreateProjectRequest { Name = "Site A" },
        cancellationToken);

ApiResult<ProjectDto> project =
    await apiClient.SendAsync<ProjectDto>(
        ProjectApiEndpoints.GetById(projectId),
        cancellationToken);
```

Default headers and query parameters:

```csharp
public static readonly ApiEndpoint Search =
    new ApiEndpoint("projects/search", HttpMethod.GET, ApiAuthenticationRequirement.Required)
        .WithQueryParameter("include", "users")
        .WithHeader("X-Client", "Unity");
```

Path parameters are URL escaped. Missing path parameters fail clearly before a
request is sent.

## ScriptableObject Endpoint Workflow

Create an endpoint asset:

`Assets > Create > Deucarian > API > Advanced > Building Blocks > Endpoint Definition`

Configure:

- Display name
- Path
- Method
- Authentication
- Timeout override
- Default headers
- Default query parameters

Use it from a MonoBehaviour or service:

```csharp
using System.Threading;
using System.Threading.Tasks;
using Deucarian.API.Configuration;
using Deucarian.API.Core;
using Deucarian.API.Models;
using UnityEngine;

public sealed class ProjectListController : MonoBehaviour
{
    [SerializeField] private ApiClientConfig apiClientConfig;
    [SerializeField] private ApiEndpointDefinition getProjectsEndpoint;

    private IApiClient apiClient;

    private void Awake()
    {
        apiClient = ApiClientFactory.Create(apiClientConfig);
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        ApiResult<ProjectDto[]> result =
            await apiClient.SendAsync<ProjectDto[]>(getProjectsEndpoint, cancellationToken);

        if (!result.IsSuccess)
        {
            ApiLog.Requests.Error(result.Error.Message);
        }
    }
}
```

`ApiEndpointDefinition` converts to the same `ApiEndpoint` model used by
code-defined endpoints. It does not store runtime state.

## Advanced ApiRequest Workflow

Use `ApiRequest` when one call needs custom details that do not belong in a
route constant or endpoint definition.

```csharp
var request = new ApiRequest(
    "projects",
    HttpMethod.GET,
    ApiAuthenticationRequirement.Required);

request.QueryParameters["include"] = "users";
request.Headers["X-Client"] = "Unity";
request.TimeoutSeconds = 10;

ApiResult<ProjectDto[]> result =
    await apiClient.SendAsync<ProjectDto[]>(request, cancellationToken);
```

For credential exchanges and token endpoints, set
`request.SuppressLogging = true`. This overrides verbose and raw-JSON logging
for the entire request so URLs, response bodies, and errors remain out of
diagnostic output.

POST raw or multipart content by setting `Body`, `BodyFormat`, and headers as
needed. Use `ResponseFormat` when you want to override the format inferred from
`TResponse`.

## Media Types

API officially supports these response shapes:

- JSON DTOs
- `string`
- `byte[]`
- `Texture2D`

Audio and video do not have dedicated built-in handlers. Download audio/video
as `byte[]` and let the consuming application decide how to store, stream, or
decode it.

### Response Formats

`ApiResponseFormat.Auto` is the default:

- DTO response types use JSON.
- `string` uses text.
- `byte[]` uses raw bytes.
- `Texture2D` uses `DownloadHandlerTexture`.

Recommended usage:

| Response type | Recommended format |
| --- | --- |
| DTO objects/arrays | `Auto` or `Json` |
| `string` | `Auto` or `Text` |
| `byte[]` | `Auto` or `Bytes` |
| `Texture2D` | `Auto` or `Texture` |

```csharp
ApiResult<ProjectDto[]> projects =
    await apiClient.GetAsync<ProjectDto[]>(ApiRoutes.Projects, cancellationToken);

ApiResult<string> health =
    await apiClient.GetAsync<string>(StatusEndpoints.Health, cancellationToken);

ApiResult<byte[]> file =
    await apiClient.GetAsync<byte[]>(FileEndpoints.Download, cancellationToken);

ApiResult<Texture2D> avatar =
    await apiClient.GetAsync<Texture2D>(ImageEndpoints.Avatar, cancellationToken);
```

Use an advanced request when the response format should be explicit:

```csharp
var request = new ApiRequest("reports/latest.pdf", HttpMethod.GET)
{
    ResponseFormat = ApiResponseFormat.Bytes
};

ApiResult<byte[]> result = await apiClient.SendAsync<byte[]>(request, cancellationToken);
```

Texture responses use Unity's `DownloadHandlerTexture`. API preserves the
HTTP status code, response headers, raw bytes, and any useful text body Unity
exposes for failed texture requests. Some servers return JSON error bodies for
image endpoints, but `DownloadHandlerTexture` does not guarantee rich text error
access. If you need reliable structured error parsing for a media endpoint,
request `byte[]` or `string` for that workflow and decode the media in project
code.

### Request Body Formats

JSON remains the default:

```csharp
ApiResult<ProjectDto> result =
    await apiClient.PostAsync<ProjectDto>(
        "projects",
        new CreateProjectRequest { Name = "Site A" },
        cancellationToken);
```

Raw text body:

```csharp
var request = new ApiRequest("status", HttpMethod.POST)
{
    Body = "ready",
    BodyFormat = ApiRequestBodyFormat.RawText,
    ResponseFormat = ApiResponseFormat.Text
};

ApiResult<string> result = await apiClient.SendAsync<string>(request, cancellationToken);
```

Raw byte body:

```csharp
var request = new ApiRequest("files/upload", HttpMethod.POST)
{
    Body = fileBytes,
    BodyFormat = ApiRequestBodyFormat.RawBytes,
    ResponseFormat = ApiResponseFormat.Json
};

ApiResult<UploadResultDto> result =
    await apiClient.SendAsync<UploadResultDto>(request, cancellationToken);
```

Multipart upload:

```csharp
using UnityEngine.Networking;

var sections = new List<IMultipartFormSection>
{
    new MultipartFormDataSection("description", "Profile image"),
    new MultipartFormFileSection("avatar", imageBytes, "avatar.png", "image/png")
};

var request = new ApiRequest("profile/avatar", HttpMethod.POST)
{
    Body = sections,
    BodyFormat = ApiRequestBodyFormat.MultipartForm
};

ApiResult<ProfileDto> result =
    await apiClient.SendAsync<ProfileDto>(request, cancellationToken);
```

### Headers

API applies these default `Accept` headers when callers do not provide
one:

| Response format | Default `Accept` |
| --- | --- |
| JSON | `application/json` |
| Text | `text/plain,*/*` |
| Bytes | `*/*` |
| Texture | `image/png,image/jpeg,image/webp,*/*` |

Default `Content-Type` values are:

| Body format | Default `Content-Type` |
| --- | --- |
| JSON | `application/json` |
| RawText | `text/plain` |
| RawBytes | `application/octet-stream` |
| MultipartForm | Unity-generated `multipart/form-data` with boundary |

Explicit headers are never overwritten. You can set `Accept` or `Content-Type`
through `ApiClientConfig.DefaultHeaders`, `ApiEndpoint` default headers, or
`ApiRequest.Headers`.

```csharp
var request = new ApiRequest("exports/csv", HttpMethod.POST)
{
    Body = csv,
    BodyFormat = ApiRequestBodyFormat.RawText,
    ResponseFormat = ApiResponseFormat.Bytes
};

request.Headers["Content-Type"] = "text/csv";
request.Headers["Accept"] = "application/octet-stream";
```

For custom formats such as CSV, PDF, audio, or video, request `string` or
`byte[]` and parse/decode in your application code.

### Extension Points

The package-level extension point for custom media is intentionally small:
request text or bytes, then parse outside API. For example, a project can
build its own CSV, PDF, audio, or video helper around `IApiClient.GetAsync<string>`
or `IApiClient.GetAsync<byte[]>` without modifying the package.

API does not currently expose a built-in `IApiResponseHandler<T>` registry.
That keeps the package lightweight and avoids a framework-style plugin system.
Add such an abstraction only if project code repeatedly needs custom response
decoders that cannot be handled cleanly with `string` or `byte[]`.

## Versioning

Current package version: `2.0.2`.

Branch strategy:

- `main`: stable package branch.
- `develop`: development package branch.
- Stable release tags should use names such as `v1.0.0` when published.
- Beta tags may use prerelease versions such as `v1.1.0-beta.1`.

Unity may pin Git package commits in `Packages/packages-lock.json`; to update, open Package Manager and update the package, remove the lock entry, or change the Git ref in `manifest.json`.

## CI And Releases

GitHub Actions validates package structure on pull requests and pushes to
`develop` or `main`. The validation checks package metadata, asmdefs, required
docs, sample placement, and banned Unity-generated artifacts.

Unity EditMode tests are available through the manual `Unity EditMode Tests`
workflow. Enable it by configuring the repository secrets required by
GameCI/Unity:

- `UNITY_LICENSE`
- `UNITY_EMAIL`
- `UNITY_PASSWORD`

Unity Asset Store publishing is not part of this package workflow. It can be
added later as a separate release channel.

## Error Handling

All client calls return `ApiResult<T>`.

```csharp
ApiResult<ProjectDto> result = await apiClient.SendAsync<ProjectDto>(endpoint, cancellationToken);

if (result.IsSuccess)
{
    ProjectDto project = result.Data;
}
else
{
    ApiError error = result.Error;

    ApiLog.Requests.Error(error.Message);
    ApiLog.Requests.Info("Status: " + error.HttpStatusCode);
    ApiLog.Requests.Info("URL: " + error.RequestUrl);
    ApiLog.Requests.Info("Raw body: " + error.RawResponseBody);
    if (error.ResponseHeaders.TryGetValue("X-Trace-Id", out string traceId))
    {
        ApiLog.Requests.Info("Trace header: " + traceId);
    }

    if (error.HasValidationErrors)
    {
        foreach (KeyValuePair<string, string[]> pair in error.ValidationErrors)
        {
            ApiLog.Requests.Info(pair.Key + ": " + string.Join(", ", pair.Value));
        }
    }
}
```

`ApiError` includes:

- Success/failure message
- HTTP status code
- Request URL
- Raw response body
- Response headers
- Backend code/message when available
- Validation errors when available
- Exception details when relevant
- Cancellation and timeout flags

`ApiTransportResponse` is internal package plumbing between the Unity transport
and response parsers. Application code should use `ApiResult<T>` and `ApiError`
for success and failure details.

## Legacy ApiServices Migration

`ApiServices` remains as a legacy/convenience facade. It delegates to a
process-wide `IApiClient`, but new code should inject `IApiClient` instead of
using static global state.

Old:

```csharp
ApiCallResult<ProjectDto[]> result =
    await ApiServices.GetAsync<ProjectDto[]>("projects", true, accessToken);
```

New:

```csharp
IApiClient apiClient = ApiClientFactory.Create(apiClientConfig, authProvider);

ApiResult<ProjectDto[]> result =
    await apiClient.SendAsync<ProjectDto[]>(ProjectApiEndpoints.GetAll, cancellationToken);
```

Temporary facade migration:

```csharp
ApiServices.Configure(apiClient);

ApiResult<ProjectDto[]> result =
    await ApiServices.SendAsync<ProjectDto[]>(ProjectApiEndpoints.GetAll, cancellationToken);
```

Obsolete `ApiServices` overloads that take `requiresAuthentication` and
`accessToken` still call the new pipeline, but should be replaced.

Legacy byte and texture download helpers are compatibility wrappers around the
configured `IApiClient`. Prefer:

```csharp
ApiResult<byte[]> bytes = await apiClient.GetAsync<byte[]>(FileEndpoints.Download, cancellationToken);
ApiResult<Texture2D> texture = await apiClient.GetAsync<Texture2D>(ImageEndpoints.Avatar, cancellationToken);
```

## Limitations

- API uses UnityWebRequest as its built-in transport.
- Built-in response handling covers JSON DTOs, `string`, `byte[]`, and `Texture2D`.
- Audio, video, PDF, CSV, archive, and other custom formats should be downloaded as `byte[]` or `string` and decoded by application code.
- There is no built-in response-handler registry.
- The built-in transport applies policy timeouts but does not automatically execute
  retries or rate limiting. Use the resolved policy values in an explicit service
  decorator so unsafe methods are never replayed accidentally.
- Environment profiles and endpoint catalogs are configuration, not a global
  environment-selection service.
- `ApiServices` remains for legacy and simple facade use, but new services should depend on `IApiClient`.
- Certificate bypass is intended only for explicit development scenarios; keep production configs on `DefaultValidation`.

## Complete Example

```csharp
using System.Threading;
using System.Threading.Tasks;
using Deucarian.API;
using Deucarian.API.Authentication;
using Deucarian.API.Configuration;
using Deucarian.API.Core;
using Deucarian.API.Models;
using UnityEngine;

public sealed class ProjectsExample : MonoBehaviour
{
    [SerializeField] private ApiClientConfig apiClientConfig;

    private IApiClient apiClient;

    private static class Endpoints
    {
        public static readonly ApiEndpoint List =
            new ApiEndpoint("projects", HttpMethod.GET, ApiAuthenticationRequirement.Required);

        public static readonly ApiEndpoint Create =
            new ApiEndpoint("projects", HttpMethod.POST, ApiAuthenticationRequirement.Required);
    }

    private void Awake()
    {
        apiClient = ApiClientFactory.Create(apiClientConfig, new ExampleTokenProvider());
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        ApiResult<ProjectDto[]> listResult =
            await apiClient.SendAsync<ProjectDto[]>(Endpoints.List, cancellationToken);

        if (listResult.IsFailure)
        {
            ApiLog.Requests.Error(listResult.Error.Message);
            return;
        }

        var body = new CreateProjectRequest { Name = "Site A" };
        ApiResult<ProjectDto> createResult =
            await apiClient.SendAsync<ProjectDto>(Endpoints.Create, body, cancellationToken);

        if (createResult.IsSuccess)
        {
            ApiLog.Requests.Info("Created project: " + createResult.Data.Name);
        }
    }
}

public sealed class ExampleTokenProvider : IApiAuthProvider
{
    public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult("replace-with-session-token");
    }
}

public sealed class ProjectDto
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public sealed class CreateProjectRequest
{
    public string Name { get; set; }
}
```

## Best Practices

- Depend on `IApiClient` in application services.
- Keep `ApiServices` only for legacy migration or very small projects.
- Use string constants for simple endpoints.
- Use `ApiEndpoint` when route metadata matters.
- Use `ApiEndpointDefinition` when designers/non-developers need inspector
  configuration.
- Use `ApiRequest` only for advanced one-off custom calls.
- Use `byte[]` for audio, video, PDFs, archives, and other binary payloads.
- Set explicit `Accept`/`Content-Type` headers only when an API requires them.
- Keep tokens in an `IApiAuthProvider`; do not pass tokens through every call.
- Prefer relative paths plus `ApiClientConfig.BaseUrl`.
- For multi-environment products, persist only `ApiEnvironmentId` and resolve routes
  through `ApiComposition`.
- Keep concrete hosts in environment profiles and stable relative routes in endpoint catalogs.
- Keep credentials out of profiles and catalogs; provide them through `IApiAuthProvider`.
- Keep certificate handling on `DefaultValidation` for production.
- Pass `CancellationToken` from MonoBehaviour lifetime, UI flow, or service flow.

## FAQ / Troubleshooting

### Why did my relative endpoint fail?

Check `ApiClientConfig.BaseUrl`. Relative endpoints are joined to this value.
Absolute URLs skip the base URL.

### Why is no Authorization header sent?

Check three things:

- The request or endpoint auth mode is `Required`, `Optional`, or `UseConfigDefault`
  with config auth set to `BearerToken`.
- The config has an `ApiAuthProviderAsset`, or a code `IApiAuthProvider` was
  passed to `ApiClientFactory.Create`.
- The provider returns a non-empty token.

### Why does a path like `projects/{id}` throw before sending?

`ApiEndpoint` requires path parameters to be resolved before request creation.
Use `.WithPathParameter("id", value)`.

### Can designers configure endpoints?

Yes. Use `ApiEndpointDefinition` assets. They convert to `ApiEndpoint` at
runtime and share the same request pipeline.

### Can API download audio or video?

Yes, as bytes. API intentionally does not include built-in `AudioClip` or
`VideoPlayer` integrations. Download the payload with `GetAsync<byte[]>`, then
let your application decode, stream, cache, or pass it to a media-specific
system.

### Why is `RawJson` obsolete?

`ApiResult<T>.RawJson` was kept for compatibility, but API now supports
text, bytes, and textures too. Use `RawResponseBody` for response text.

### Can I bypass certificates for local development?

Only explicitly through `BypassInDevelopmentOnly`. This mode returns a bypass
handler only in the Unity Editor or development builds. Production defaults to
normal certificate validation.

### Why are old ApiServices calls marked obsolete?

They pass auth decisions and tokens per call, which makes APIs noisy and harder
to configure safely. Use `IApiClient` with config/auth providers instead.

## Validation

Run the shared package validator from this repository root:

```powershell
python C:/Repositories/Package-Registry/Tools/deucarian_package_validator.py --registry-root C:/Repositories/Package-Registry --repository-root . --config deucarian-package.json
```

Documentation-only updates should still pass:

```powershell
git diff --check
```

## Documentation Maintenance Policy

Documentation must be updated whenever public APIs are added, changed,
deprecated, or removed. This includes:

- README examples and migration notes.
- XML documentation on public types and members.
- Sample scripts or scenes when public workflow behavior changes.
- Obsolete messages that point to the current replacement API.
- Contributor guidance in `CONTRIBUTING.md` when process expectations change.

## Architecture / Contributor Notes

- [AGENTS.md](AGENTS.md) contains repository-specific ownership and Codex guidance.
- Deucarian architecture rules live in [Package Registry](https://github.com/Deucarian/Package-Registry/blob/develop/ARCHITECTURE.md).
- Capability ownership is tracked in [CAPABILITY_OWNERSHIP.md](https://github.com/Deucarian/Package-Registry/blob/develop/CAPABILITY_OWNERSHIP.md).

## License

MIT. See [LICENSE.md](LICENSE.md).
