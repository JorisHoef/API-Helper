using System;
using System.Collections.Generic;
using Deucarian.API.Configuration;
using Deucarian.API.Models;

namespace Deucarian.API.Core
{
    /// <summary>Sanitized availability of a requested API environment.</summary>
    public enum ApiEnvironmentAvailability
    {
        /// <summary>The identifier is not registered or declared as known.</summary>
        Unknown = 0,

        /// <summary>The identifier is known but has no configured environment profile.</summary>
        Unconfigured = 1,

        /// <summary>The identifier has a validated environment profile.</summary>
        Configured = 2
    }

    /// <summary>Sanitized environment resolution state suitable for status UI.</summary>
    public sealed class ApiEnvironmentStatus
    {
        internal ApiEnvironmentStatus(ApiEnvironmentId environmentId,
                                      string displayName,
                                      ApiEnvironmentStage stage,
                                      ApiEnvironmentAvailability availability,
                                      string message)
        {
            EnvironmentId = environmentId;
            DisplayName = displayName ?? string.Empty;
            Stage = stage;
            Availability = availability;
            Message = message;
        }

        /// <summary>Requested environment identifier, or empty when the supplied value was invalid.</summary>
        public ApiEnvironmentId EnvironmentId { get; }

        /// <summary>Safe display label; empty when the supplied identifier was invalid.</summary>
        public string DisplayName { get; }

        /// <summary>Vendor-neutral lifecycle stage, or Custom when unknown.</summary>
        public ApiEnvironmentStage Stage { get; }

        /// <summary>Whether the environment is configured, unconfigured, or unknown.</summary>
        public ApiEnvironmentAvailability Availability { get; }

        /// <summary>True only when the environment has a validated profile.</summary>
        public bool IsResolved =>
            Availability == ApiEnvironmentAvailability.Configured;

        /// <summary>Safe diagnostic message for unresolved state, otherwise null.</summary>
        public string Message { get; }
    }

    /// <summary>A named client resolved for one environment.</summary>
    public sealed class ApiResolvedClient
    {
        internal ApiResolvedClient(ApiEnvironmentId environmentId,
                                   string environmentDisplayName,
                                   ApiClientId clientId,
                                   string baseUrl,
                                   IDictionary<string, string> defaultHeaders,
                                   ApiRequestPolicy requestPolicy)
        {
            EnvironmentId = environmentId;
            EnvironmentDisplayName = environmentDisplayName ?? environmentId.Value;
            ClientId = clientId;
            BaseUrl = baseUrl;
            DefaultHeaders = new Dictionary<string, string>(
                defaultHeaders ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase);
            RequestPolicy = requestPolicy ?? ApiRequestPolicy.Default;
        }

        /// <summary>Environment used for this resolution.</summary>
        public ApiEnvironmentId EnvironmentId { get; }

        /// <summary>Human-friendly environment label.</summary>
        public string EnvironmentDisplayName { get; }

        /// <summary>Resolved named-client identifier.</summary>
        public ApiClientId ClientId { get; }

        /// <summary>Resolved absolute base URL. Do not expose this through generic status UI.</summary>
        public string BaseUrl { get; }

        /// <summary>Resolved non-secret client headers.</summary>
        public IReadOnlyDictionary<string, string> DefaultHeaders { get; }

        /// <summary>Policy after environment and client overlays.</summary>
        public ApiRequestPolicy RequestPolicy { get; }
    }

    /// <summary>A catalog endpoint composed with a selected environment and named client.</summary>
    public sealed class ApiResolvedEndpoint
    {
        internal ApiResolvedEndpoint(ApiCatalogId catalogId,
                                     ApiEndpointId endpointId,
                                     ApiResolvedClient client,
                                     ApiEndpoint endpoint,
                                     ApiRequestPolicy requestPolicy)
        {
            CatalogId = catalogId;
            EndpointId = endpointId;
            Client = client ?? throw new ArgumentNullException(nameof(client));
            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            RequestPolicy = requestPolicy ?? ApiRequestPolicy.Default;
        }

        /// <summary>Catalog containing the endpoint.</summary>
        public ApiCatalogId CatalogId { get; }

        /// <summary>Stable endpoint identifier.</summary>
        public ApiEndpointId EndpointId { get; }

        /// <summary>Named client resolved for the selected environment.</summary>
        public ApiResolvedClient Client { get; }

        /// <summary>Existing API endpoint model with a resolved absolute route.</summary>
        public ApiEndpoint Endpoint { get; }

        /// <summary>Policy after environment, client, and endpoint overlays.</summary>
        public ApiRequestPolicy RequestPolicy { get; }

        /// <summary>Creates an advanced request through the existing ApiEndpoint pipeline.</summary>
        public ApiRequest CreateRequest(object body = null)
        {
            return Endpoint.CreateRequest(body);
        }
    }
}
