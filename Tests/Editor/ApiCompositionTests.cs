using System;
using System.Collections.Generic;
using System.Threading;
using Deucarian.API.Certificates;
using Deucarian.API.Configuration;
using Deucarian.API.Core;
using Deucarian.API.Models;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;

namespace Deucarian.API.Tests
{
    public sealed class ApiCompositionTests
    {
        private readonly List<UnityEngine.Object> createdAssets = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object asset in createdAssets)
            {
                if (asset != null)
                {
                    UnityEngine.Object.DestroyImmediate(asset);
                }
            }

            createdAssets.Clear();
        }

        [Test]
        public void StableIdentifiers_AreSerializableAndRejectDisplayLabels()
        {
            ApiEnvironmentId environmentId = new ApiEnvironmentId("acceptance.eu-west");
            ApiEndpointId endpointId;

            Assert.AreEqual("acceptance.eu-west", environmentId.Value);
            Assert.IsTrue(ApiEndpointId.TryParse("projects.list", out endpointId));
            Assert.IsFalse(ApiEndpointId.TryParse("Projects List", out endpointId));
            Assert.Throws<ArgumentException>(() => new ApiClientId("Primary API"));
        }

        [Test]
        public void Composition_ResolvesNamedClientRouteMetadataHeadersAndLayeredPolicy()
        {
            ApiEnvironmentProfile environment = CreateEnvironment(
                "development",
                "Development",
                "https://dev.example.com/api/v2");
            environment.DefaultRequestPolicy.TimeoutSeconds = 45;
            environment.DefaultRequestPolicy.MaxRetryAttempts = 2;
            environment.DefaultRequestPolicy.InitialRetryBackoffMilliseconds = 200;
            environment.Clients[0].RequestPolicy.TimeoutSeconds = 20;
            environment.Clients[0].DefaultHeaders.Add(Pair("X-Environment", "development"));

            ApiEndpointCatalog catalog = CreateCatalog();
            ApiEndpointCatalogEntry entry = CreateEndpoint(
                "projects.get",
                "primary",
                "projects/{id}",
                HttpMethod.GET);
            entry.DefaultHeaders.Add(Pair("X-Feature", "projects"));
            entry.DefaultQueryParameters.Add(Pair("include", "users"));
            entry.RequestPolicy.RateLimitRequestCountHint = 10;
            entry.RequestPolicy.RateLimitWindowSecondsHint = 1f;
            entry.SuppressLogging = true;
            catalog.Endpoints.Add(entry);

            ApiComposition composition = new ApiComposition(environment, catalog);
            ApiResolvedEndpoint resolved = composition.ResolveEndpoint(
                new ApiEnvironmentId("development"),
                new ApiEndpointId("projects.get"));
            ApiRequest request = resolved.Endpoint.WithPathParameter("id", "A B").CreateRequest();

            Assert.AreEqual("https://dev.example.com/api/v2/projects/A%20B", request.Endpoint);
            Assert.AreEqual(HttpMethod.GET, request.Method);
            Assert.AreEqual("development", request.Headers["X-Environment"]);
            Assert.AreEqual("projects", request.Headers["X-Feature"]);
            Assert.AreEqual("users", request.QueryParameters["include"]);
            Assert.AreEqual(20, request.TimeoutSeconds);
            Assert.AreSame(resolved.RequestPolicy, request.RequestPolicy);
            Assert.AreEqual(2, request.RequestPolicy.MaxRetryAttempts);
            Assert.AreEqual(200, request.RequestPolicy.InitialRetryBackoffMilliseconds);
            Assert.AreEqual(10, request.RequestPolicy.RateLimitRequestCountHint);
            Assert.AreEqual(1f, request.RequestPolicy.RateLimitWindowSecondsHint);
            Assert.IsTrue(request.SuppressLogging);
        }

        [Test]
        public void Composition_UsesExplicitEnvironmentSelectionWithoutGlobalState()
        {
            ApiEnvironmentProfile development = CreateEnvironment(
                "development",
                "Development",
                "https://dev.example.com/api/v2");
            ApiEnvironmentProfile production = CreateEnvironment(
                "production",
                "Production",
                "https://api.example.com/api/v2");
            ApiEndpointCatalog catalog = CreateCatalog();
            catalog.Endpoints.Add(CreateEndpoint("health.get", "primary", "health", HttpMethod.GET));

            ApiComposition composition = new ApiComposition(
                new[] { development, production },
                catalog);

            ApiResolvedEndpoint developmentEndpoint = composition.ResolveEndpoint(
                new ApiEnvironmentId("development"),
                new ApiEndpointId("health.get"));
            ApiResolvedEndpoint productionEndpoint = composition.ResolveEndpoint(
                new ApiEnvironmentId("production"),
                new ApiEndpointId("health.get"));

            Assert.AreEqual("https://dev.example.com/api/v2/health", developmentEndpoint.Endpoint.Path);
            Assert.AreEqual("https://api.example.com/api/v2/health", productionEndpoint.Endpoint.Path);
        }

        [Test]
        public void EnvironmentStatus_IsSanitizedAndDoesNotExposeConnectionDetails()
        {
            ApiEnvironmentProfile environment = CreateEnvironment(
                "acceptance",
                "Acceptance",
                "https://secret-host.example.com/api");
            ApiEndpointCatalog catalog = CreateCatalog();
            catalog.Endpoints.Add(CreateEndpoint("health.get", "primary", "health", HttpMethod.GET));
            ApiComposition composition = new ApiComposition(environment, catalog);

            ApiEnvironmentStatus resolved = composition.GetEnvironmentStatus("acceptance");
            ApiEnvironmentStatus missing = composition.GetEnvironmentStatus("production");
            ApiEnvironmentStatus invalid = composition.GetEnvironmentStatus("https://secret-host.example.com");

            Assert.IsTrue(resolved.IsResolved);
            Assert.AreEqual("Acceptance", resolved.DisplayName);
            Assert.IsNull(resolved.Message);
            Assert.IsFalse(missing.IsResolved);
            StringAssert.DoesNotContain("http", missing.Message);
            StringAssert.DoesNotContain("secret-host", missing.Message);
            Assert.IsFalse(invalid.IsResolved);
            Assert.AreEqual(string.Empty, invalid.DisplayName);
            StringAssert.DoesNotContain("secret-host", invalid.Message);
        }

        [Test]
        public void Composition_FailsClearlyWhenEnvironmentDoesNotProvideCatalogClient()
        {
            ApiEnvironmentProfile environment = CreateEnvironment(
                "development",
                "Development",
                "https://dev.example.com/api");
            ApiEndpointCatalog catalog = CreateCatalog();
            catalog.Endpoints.Add(CreateEndpoint("media.get", "media", "files/{id}", HttpMethod.GET));
            ApiComposition composition = new ApiComposition(environment, catalog);

            ApiResolvedEndpoint resolved;
            string message;
            bool success = composition.TryResolveEndpoint(
                new ApiEnvironmentId("development"),
                new ApiEndpointId("media.get"),
                out resolved,
                out message);

            Assert.IsFalse(success);
            Assert.IsNull(resolved);
            StringAssert.Contains("does not define client 'media'", message);
        }

        [Test]
        public void Composition_RejectsDuplicateEnvironmentIds()
        {
            ApiEnvironmentProfile first = CreateEnvironment(
                "development",
                "Development A",
                "https://one.example.com");
            ApiEnvironmentProfile second = CreateEnvironment(
                "development",
                "Development B",
                "https://two.example.com");
            ApiEndpointCatalog catalog = CreateCatalog();
            catalog.Endpoints.Add(CreateEndpoint("health.get", "primary", "health", HttpMethod.GET));

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => new ApiComposition(new[] { first, second }, catalog));
            StringAssert.Contains("Duplicate environment ID", exception.Message);
        }

        [Test]
        public void EnvironmentStages_PreserveSerializedValuesAndExposeBuiltInOrder()
        {
            Assert.AreEqual(0, (int)ApiEnvironmentStage.Custom);
            Assert.AreEqual(1, (int)ApiEnvironmentStage.Development);
            Assert.AreEqual(2, (int)ApiEnvironmentStage.Testing);
            Assert.AreEqual(3, (int)ApiEnvironmentStage.Acceptance);
            Assert.AreEqual(4, (int)ApiEnvironmentStage.Production);
            Assert.AreEqual(5, (int)ApiEnvironmentStage.Local);

            CollectionAssert.AreEqual(
                new[]
                {
                    ApiEnvironmentStage.Development,
                    ApiEnvironmentStage.Testing,
                    ApiEnvironmentStage.Acceptance,
                    ApiEnvironmentStage.Production
                },
                ApiEnvironmentStages.Standard);

            CollectionAssert.AreEqual(
                new[]
                {
                    ApiEnvironmentStage.Local,
                    ApiEnvironmentStage.Development,
                    ApiEnvironmentStage.Testing,
                    ApiEnvironmentStage.Acceptance,
                    ApiEnvironmentStage.Production
                },
                ApiEnvironmentStages.All);

            Assert.IsTrue(ApiEnvironmentStages.IsSupported(
                ApiEnvironmentStage.Local));
            Assert.IsTrue(ApiEnvironmentStages.IsSupported(
                ApiEnvironmentStage.Custom));
            Assert.IsFalse(ApiEnvironmentStages.IsSupported(
                (ApiEnvironmentStage)6));
        }

        [Test]
        public void EnvironmentDescriptor_AcceptsLocalAndRejectsUnknownStageValues()
        {
            ApiEnvironmentDescriptor local = Describe(
                "simultria.local",
                ApiEnvironmentStage.Local,
                " Local ");

            Assert.AreEqual(ApiEnvironmentStage.Local, local.Stage);
            Assert.AreEqual("Local", local.DisplayName);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Describe(
                    "unsupported",
                    (ApiEnvironmentStage)6,
                    "Unsupported"));
        }

        [Test]
        public void Composition_PreservesKnownBlankLocalAsUnconfiguredNotCustom()
        {
            ApiEnvironmentProfile local = CreateEnvironment(
                "simultria.local",
                "Local profile",
                string.Empty);
            ApiEndpointCatalog catalog = CreateCatalog();
            catalog.Endpoints.Add(CreateEndpoint(
                "health.get",
                "primary",
                "health",
                HttpMethod.GET));
            ApiComposition composition = new ApiComposition(
                new[] { local },
                catalog,
                new[]
                {
                    Describe(
                        "simultria.local",
                        ApiEnvironmentStage.Local,
                        "Local")
                });

            ApiEnvironmentStatus status =
                composition.GetEnvironmentStatus("simultria.local");

            Assert.AreEqual(
                ApiEnvironmentAvailability.Unconfigured,
                status.Availability);
            Assert.AreEqual(ApiEnvironmentStage.Local, status.Stage);
            Assert.AreEqual("Local", status.DisplayName);
            Assert.AreNotEqual(ApiEnvironmentStage.Custom, status.Stage);

            Assert.IsFalse(composition.TryResolveClient(
                new ApiEnvironmentId("simultria.local"),
                new ApiClientId("primary"),
                out ApiResolvedClient client,
                out string message));
            Assert.IsNull(client);
            StringAssert.Contains("known but not configured", message);
        }

        [Test]
        public void EnvironmentProfileConfiguration_ClassifiesConfiguredBlankAndPartialProfiles()
        {
            ApiEnvironmentProfile configured = CreateEnvironment(
                "development",
                "Development",
                "https://dev.example.com/api");
            ApiEnvironmentProfile notConfigured = CreateEnvironment(
                "testing",
                "Testing",
                "   ");
            ApiEnvironmentProfile partiallyConfigured = CreateEnvironment(
                "acceptance",
                "Acceptance",
                "https://acceptance.example.com/api");
            partiallyConfigured.Clients.Add(new ApiNamedClientDefinition
            {
                ClientId = "media",
                BaseUrl = string.Empty
            });

            string configuredMessage;
            string notConfiguredMessage;
            string partialMessage;

            Assert.AreEqual(
                ApiEnvironmentProfileConfigurationState.Configured,
                configured.ClassifyConfiguration(out configuredMessage));
            Assert.IsNull(configuredMessage);
            Assert.AreEqual(
                ApiEnvironmentProfileConfigurationState.NotConfigured,
                notConfigured.ClassifyConfiguration(out notConfiguredMessage));
            Assert.IsNull(notConfiguredMessage);
            Assert.AreEqual(
                ApiEnvironmentProfileConfigurationState.Invalid,
                partiallyConfigured.ClassifyConfiguration(out partialMessage));
            StringAssert.Contains("partially configured", partialMessage);
        }

        [Test]
        public void Composition_LegacyConstructorKeepsConfiguredAndUnknownStatusBehavior()
        {
            ApiEnvironmentProfile development = CreateEnvironment(
                "development",
                "Development",
                "https://dev.example.com/api");
            ApiEndpointCatalog catalog = CreateCatalog();
            catalog.Endpoints.Add(CreateEndpoint("health.get", "primary", "health", HttpMethod.GET));

            ApiComposition composition = new ApiComposition(development, catalog);

            ApiEnvironmentStatus configured = composition.GetEnvironmentStatus("development");
            ApiEnvironmentStatus unknown = composition.GetEnvironmentStatus("testing");

            Assert.IsTrue(configured.IsResolved);
            Assert.AreEqual(ApiEnvironmentAvailability.Configured, configured.Availability);
            Assert.AreEqual(ApiEnvironmentStage.Custom, configured.Stage);
            Assert.AreEqual("Development", configured.DisplayName);
            Assert.IsNull(configured.Message);
            Assert.IsFalse(unknown.IsResolved);
            Assert.AreEqual(ApiEnvironmentAvailability.Unknown, unknown.Availability);
            Assert.AreEqual(ApiEnvironmentStage.Custom, unknown.Stage);
            Assert.AreEqual("testing", unknown.DisplayName);
            StringAssert.Contains("not registered", unknown.Message);
        }

        [Test]
        public void Composition_DistinguishesConfiguredUnconfiguredAndUnknownEnvironments()
        {
            ApiEnvironmentProfile development = CreateEnvironment(
                "development",
                "Development profile",
                "https://dev.example.com/api");
            ApiEnvironmentProfile testing = CreateEnvironment(
                "testing",
                "Testing profile",
                string.Empty);
            ApiEndpointCatalog catalog = CreateCatalog();
            catalog.Endpoints.Add(CreateEndpoint("health.get", "primary", "health", HttpMethod.GET));
            ApiComposition composition = new ApiComposition(
                new[] { development, testing },
                catalog,
                new[]
                {
                    Describe("development", ApiEnvironmentStage.Development, "Development"),
                    Describe("testing", ApiEnvironmentStage.Testing, "Testing")
                });

            ApiEnvironmentStatus configured = composition.GetEnvironmentStatus("development");
            ApiEnvironmentStatus unconfigured = composition.GetEnvironmentStatus("testing");
            ApiEnvironmentStatus unknown = composition.GetEnvironmentStatus("vendor-preview");

            Assert.AreEqual(ApiEnvironmentAvailability.Configured, configured.Availability);
            Assert.AreEqual(ApiEnvironmentStage.Development, configured.Stage);
            Assert.AreEqual("Development profile", configured.DisplayName);
            Assert.AreEqual(ApiEnvironmentAvailability.Unconfigured, unconfigured.Availability);
            Assert.AreEqual(ApiEnvironmentStage.Testing, unconfigured.Stage);
            Assert.AreEqual("Testing", unconfigured.DisplayName);
            StringAssert.Contains("known but not configured", unconfigured.Message);
            Assert.AreEqual(ApiEnvironmentAvailability.Unknown, unknown.Availability);
            Assert.AreEqual(ApiEnvironmentStage.Custom, unknown.Stage);
            Assert.AreEqual("vendor-preview", unknown.DisplayName);
        }

        [Test]
        public void Composition_DescriptorAwareOverloadSupportsOnlyUnconfiguredKnownProfiles()
        {
            ApiEnvironmentProfile development = CreateEnvironment(
                "development",
                "Development profile",
                string.Empty);
            ApiEnvironmentProfile testing = CreateEnvironment(
                "testing",
                "Testing profile",
                string.Empty);
            ApiEnvironmentProfile acceptance = CreateEnvironment(
                "acceptance",
                "Acceptance profile",
                string.Empty);
            ApiEnvironmentProfile production = CreateEnvironment(
                "production",
                "Production profile",
                string.Empty);
            ApiEndpointCatalog catalog = CreateCatalog();
            catalog.Endpoints.Add(CreateEndpoint("health.get", "primary", "health", HttpMethod.GET));

            ApiComposition composition = new ApiComposition(
                new[] { development, testing, acceptance, production },
                catalog,
                new[]
                {
                    Describe("development", ApiEnvironmentStage.Development, "Development"),
                    Describe("testing", ApiEnvironmentStage.Testing, "Testing"),
                    Describe("acceptance", ApiEnvironmentStage.Acceptance, "Acceptance"),
                    Describe("production", ApiEnvironmentStage.Production, "Production")
                });

            Assert.AreEqual(
                ApiEnvironmentAvailability.Unconfigured,
                composition.GetEnvironmentStatus("development").Availability);
            Assert.AreEqual(
                ApiEnvironmentAvailability.Unconfigured,
                composition.GetEnvironmentStatus("testing").Availability);
            Assert.AreEqual(
                ApiEnvironmentAvailability.Unconfigured,
                composition.GetEnvironmentStatus("acceptance").Availability);
            Assert.AreEqual(
                ApiEnvironmentAvailability.Unconfigured,
                composition.GetEnvironmentStatus("production").Availability);
        }

        [Test]
        public void Composition_LegacyConstructorStillRejectsBlankProfileHosts()
        {
            ApiEnvironmentProfile notConfigured = CreateEnvironment(
                "testing",
                "Testing",
                string.Empty);
            ApiEndpointCatalog catalog = CreateCatalog();
            catalog.Endpoints.Add(CreateEndpoint("health.get", "primary", "health", HttpMethod.GET));

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => new ApiComposition(notConfigured, catalog));

            StringAssert.Contains("absolute HTTP(S) base URL", exception.Message);
        }

        [Test]
        public void Composition_RejectsDuplicateKnownEnvironmentDescriptors()
        {
            ApiEnvironmentProfile development = CreateEnvironment(
                "development",
                "Development",
                "https://dev.example.com/api");
            ApiEndpointCatalog catalog = CreateCatalog();
            catalog.Endpoints.Add(CreateEndpoint("health.get", "primary", "health", HttpMethod.GET));

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => new ApiComposition(
                    new[] { development },
                    catalog,
                    new[]
                    {
                        Describe("testing", ApiEnvironmentStage.Testing, "Testing A"),
                        Describe("testing", ApiEnvironmentStage.Testing, "Testing B")
                    }));

            StringAssert.Contains("Duplicate known environment ID", exception.Message);
        }

        [Test]
        public void Composition_KnownUnconfiguredEnvironmentCannotResolveClientOrEndpoint()
        {
            ApiEnvironmentProfile development = CreateEnvironment(
                "development",
                "Development",
                "https://dev.example.com/api");
            ApiEnvironmentProfile testingProfile = CreateEnvironment(
                "testing",
                "Testing profile",
                string.Empty);
            ApiEndpointCatalog catalog = CreateCatalog();
            catalog.Endpoints.Add(CreateEndpoint("health.get", "primary", "health", HttpMethod.GET));
            ApiComposition composition = new ApiComposition(
                new[] { development, testingProfile },
                catalog,
                new[] { Describe("testing", ApiEnvironmentStage.Testing, "Testing") });
            ApiEnvironmentId testing = new ApiEnvironmentId("testing");

            ApiResolvedClient client;
            string clientMessage;
            bool clientResolved = composition.TryResolveClient(
                testing,
                new ApiClientId("primary"),
                out client,
                out clientMessage);
            ApiResolvedEndpoint endpoint;
            string endpointMessage;
            bool endpointResolved = composition.TryResolveEndpoint(
                testing,
                new ApiEndpointId("health.get"),
                out endpoint,
                out endpointMessage);

            Assert.IsFalse(clientResolved);
            Assert.IsNull(client);
            StringAssert.Contains("known but not configured", clientMessage);
            Assert.IsFalse(endpointResolved);
            Assert.IsNull(endpoint);
            StringAssert.Contains("known but not configured", endpointMessage);
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => composition.ResolveEndpoint(testing, new ApiEndpointId("health.get")));
            StringAssert.Contains("known but not configured", exception.Message);
        }

        [Test]
        public void Composition_RejectsMalformedConfiguredProfileEvenWhenEnvironmentIsKnown()
        {
            ApiEnvironmentProfile malformed = CreateEnvironment(
                "testing",
                "Testing",
                "not-an-absolute-url");
            ApiEndpointCatalog catalog = CreateCatalog();
            catalog.Endpoints.Add(CreateEndpoint("health.get", "primary", "health", HttpMethod.GET));

            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => new ApiComposition(
                    new[] { malformed },
                    catalog,
                    new[] { Describe("testing", ApiEnvironmentStage.Testing, "Testing") }));

            StringAssert.Contains("absolute HTTP(S) base URL", exception.Message);
        }

        [Test]
        public void Composition_ConfiguredKnownEnvironmentsResolveCatalogAgainstDifferentHosts()
        {
            ApiEnvironmentProfile development = CreateEnvironment(
                "development",
                "Development",
                "https://dev.example.com/api/v2");
            ApiEnvironmentProfile production = CreateEnvironment(
                "production",
                "Production",
                "https://api.example.com/api/v2");
            ApiEndpointCatalog catalog = CreateCatalog();
            catalog.Endpoints.Add(CreateEndpoint("health.get", "primary", "health", HttpMethod.GET));
            ApiComposition composition = new ApiComposition(
                new[] { development, production },
                catalog,
                new[]
                {
                    Describe("development", ApiEnvironmentStage.Development, "Development"),
                    Describe("production", ApiEnvironmentStage.Production, "Production")
                });

            ApiResolvedEndpoint developmentEndpoint = composition.ResolveEndpoint(
                new ApiEnvironmentId("development"),
                new ApiEndpointId("health.get"));
            ApiResolvedEndpoint productionEndpoint = composition.ResolveEndpoint(
                new ApiEnvironmentId("production"),
                new ApiEndpointId("health.get"));

            Assert.AreEqual(
                "https://dev.example.com/api/v2/health",
                developmentEndpoint.Endpoint.Path);
            Assert.AreEqual(
                "https://api.example.com/api/v2/health",
                productionEndpoint.Endpoint.Path);
            Assert.AreEqual(
                ApiEnvironmentStage.Development,
                composition.GetEnvironmentStatus("development").Stage);
            Assert.AreEqual(
                ApiEnvironmentStage.Production,
                composition.GetEnvironmentStatus("production").Stage);
        }

        [Test]
        public void RequestPolicyDefinition_InheritsIndividualValuesAndBoundsBackoff()
        {
            ApiRequestPolicy fallback = new ApiRequestPolicy(40, 3, 100, 2f, 500, 0, 0f);
            ApiRequestPolicyDefinition definition = new ApiRequestPolicyDefinition
            {
                TimeoutSeconds = 15,
                RateLimitRequestCountHint = 60,
                RateLimitWindowSecondsHint = 30f
            };

            ApiRequestPolicy resolved = definition.Resolve(fallback);

            Assert.AreEqual(15, resolved.TimeoutSeconds);
            Assert.AreEqual(3, resolved.MaxRetryAttempts);
            Assert.AreEqual(100, resolved.InitialRetryBackoffMilliseconds);
            Assert.AreEqual(500, resolved.GetRetryBackoffMilliseconds(4));
            Assert.AreEqual(60, resolved.RateLimitRequestCountHint);
            Assert.AreEqual(30f, resolved.RateLimitWindowSecondsHint);
        }

        [Test]
        public void RequestBuilder_UsesPolicyTimeoutWhenLegacyOverrideIsAbsent()
        {
            ApiClientConfig config = CreateAsset<ApiClientConfig>();
            config.BaseUrl = "https://example.com";
            config.TimeoutSeconds = 12;
            UnityWebRequestBuilder builder = new UnityWebRequestBuilder(
                config,
                new NewtonsoftApiSerializer(),
                null,
                new ApiCertificateHandlerFactory(ApiCertificateHandlingMode.DefaultValidation));
            ApiRequest request = new ApiRequest("projects")
            {
                RequestPolicy = new ApiRequestPolicy(4, 0, 250, 2f, 5000, 0, 0f)
            };

            using (UnityWebRequest webRequest = builder.BuildAsync(
                       request,
                       ApiResponseFormat.Json,
                       CancellationToken.None).GetAwaiter().GetResult())
            {
                Assert.AreEqual(4, webRequest.timeout);
            }
        }

        private ApiEnvironmentProfile CreateEnvironment(string id,
                                                        string displayName,
                                                        string baseUrl)
        {
            ApiEnvironmentProfile environment = CreateAsset<ApiEnvironmentProfile>();
            environment.EnvironmentId = id;
            environment.DisplayName = displayName;
            environment.Clients.Add(new ApiNamedClientDefinition
            {
                ClientId = "primary",
                BaseUrl = baseUrl
            });
            return environment;
        }

        private ApiEndpointCatalog CreateCatalog()
        {
            ApiEndpointCatalog catalog = CreateAsset<ApiEndpointCatalog>();
            catalog.CatalogId = "example.v1";
            catalog.DisplayName = "Example API";
            return catalog;
        }

        private static ApiEndpointCatalogEntry CreateEndpoint(string endpointId,
                                                              string clientId,
                                                              string route,
                                                              HttpMethod method)
        {
            return new ApiEndpointCatalogEntry
            {
                EndpointId = endpointId,
                ClientId = clientId,
                RouteTemplate = route,
                Method = method
            };
        }

        private static ApiKeyValuePair Pair(string key, string value)
        {
            return new ApiKeyValuePair { Key = key, Value = value };
        }

        private static ApiEnvironmentDescriptor Describe(
            string id,
            ApiEnvironmentStage stage,
            string displayName)
        {
            return new ApiEnvironmentDescriptor(
                new ApiEnvironmentId(id),
                stage,
                displayName);
        }

        private T CreateAsset<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            createdAssets.Add(asset);
            return asset;
        }
    }
}
