using System;
using System.Collections.Generic;
using Deucarian.API.Models;
using UnityEngine;

namespace Deucarian.API.Configuration
{
    /// <summary>A stable endpoint definition that points to a named client rather than a concrete host.</summary>
    [Serializable]
    public sealed class ApiEndpointCatalogEntry
    {
        [Tooltip("Stable endpoint identifier such as 'projects.list' or 'reports.download'.")]
        [SerializeField] private string endpointId;

        [Tooltip("Stable named client ID resolved from the selected environment profile.")]
        [SerializeField] private string clientId;

        [Tooltip("Relative route template. Braced path parameters are resolved through ApiEndpoint.WithPathParameter.")]
        [SerializeField] private string routeTemplate;

        [Tooltip("HTTP method used by this endpoint.")]
        [SerializeField] private HttpMethod method = HttpMethod.GET;

        [Tooltip("Authentication requirement. Token acquisition and session lifecycle remain external to API.")]
        [SerializeField] private ApiAuthenticationRequirement authentication =
                ApiAuthenticationRequirement.UseConfigDefault;

        [Tooltip("Expected response format. Auto infers from the requested response type.")]
        [SerializeField] private ApiResponseFormat responseFormat = ApiResponseFormat.Auto;

        [Tooltip("Headers layered over named-client defaults. Do not store credentials here.")]
        [SerializeField] private List<ApiKeyValuePair> defaultHeaders = new List<ApiKeyValuePair>();

        [Tooltip("Query values applied to every request created from this endpoint.")]
        [SerializeField] private List<ApiKeyValuePair> defaultQueryParameters =
                new List<ApiKeyValuePair>();

        [Tooltip("Optional policy values layered over environment and named-client defaults.")]
        [SerializeField] private ApiRequestPolicyDefinition requestPolicy = new ApiRequestPolicyDefinition();

        [Tooltip("Suppress API request, response, and error logging for sensitive endpoints.")]
        [SerializeField] private bool suppressLogging;

        /// <summary>Stable endpoint identifier.</summary>
        public string EndpointId { get => endpointId; set => endpointId = value; }

        /// <summary>Named client identifier resolved from an environment profile.</summary>
        public string ClientId { get => clientId; set => clientId = value; }

        /// <summary>Relative route template, optionally containing braced path parameters.</summary>
        public string RouteTemplate { get => routeTemplate; set => routeTemplate = value; }

        /// <summary>HTTP method used by this endpoint.</summary>
        public HttpMethod Method { get => method; set => method = value; }

        /// <summary>Authentication behavior passed through the existing request pipeline.</summary>
        public ApiAuthenticationRequirement Authentication
        {
            get => authentication;
            set => authentication = value;
        }

        /// <summary>Expected response format.</summary>
        public ApiResponseFormat ResponseFormat { get => responseFormat; set => responseFormat = value; }

        /// <summary>Headers layered over named-client defaults.</summary>
        public List<ApiKeyValuePair> DefaultHeaders => defaultHeaders;

        /// <summary>Query values applied to requests created from this entry.</summary>
        public List<ApiKeyValuePair> DefaultQueryParameters => defaultQueryParameters;

        /// <summary>Policy values layered over environment and named-client defaults.</summary>
        public ApiRequestPolicyDefinition RequestPolicy
        {
            get => requestPolicy;
            set => requestPolicy = value ?? new ApiRequestPolicyDefinition();
        }

        /// <summary>True when API logging must be suppressed for this endpoint.</summary>
        public bool SuppressLogging { get => suppressLogging; set => suppressLogging = value; }

        internal bool IsValid(out string message)
        {
            ApiEndpointId parsedEndpointId;
            if (!ApiEndpointId.TryParse(endpointId, out parsedEndpointId))
            {
                message = "Endpoint entry has an invalid stable endpoint ID: '"
                          + (endpointId ?? string.Empty) + "'.";
                return false;
            }

            ApiClientId parsedClientId;
            if (!ApiClientId.TryParse(clientId, out parsedClientId))
            {
                message = "Endpoint '" + parsedEndpointId + "' has an invalid client ID: '"
                          + (clientId ?? string.Empty) + "'.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(routeTemplate))
            {
                message = "Endpoint '" + parsedEndpointId + "' requires a route template.";
                return false;
            }

            Uri absoluteRoute;
            if (Uri.TryCreate(routeTemplate.Trim(), UriKind.Absolute, out absoluteRoute))
            {
                message = "Endpoint '" + parsedEndpointId
                          + "' must use a relative route template so environments retain host ownership.";
                return false;
            }

            if (requestPolicy != null && !requestPolicy.IsValid(out message))
            {
                message = "Endpoint '" + parsedEndpointId + "' has an invalid request policy: " + message;
                return false;
            }

            message = null;
            return true;
        }
    }

    /// <summary>
    /// Vendor-neutral route catalog. It owns stable endpoint IDs and request metadata while environment
    /// profiles own concrete hosts.
    /// </summary>
    public sealed class ApiEndpointCatalog : ScriptableObject
    {
        [Tooltip("Stable catalog identifier, for example 'building-api.v2'.")]
        [SerializeField] private string catalogId;

        [Tooltip("Human-friendly catalog name.")]
        [SerializeField] private string displayName;

        [Tooltip("Stable endpoint definitions in this catalog.")]
        [SerializeField] private List<ApiEndpointCatalogEntry> endpoints =
                new List<ApiEndpointCatalogEntry>();

        /// <summary>Stable catalog identifier.</summary>
        public string CatalogId { get => catalogId; set => catalogId = value; }

        /// <summary>Human-friendly catalog name.</summary>
        public string DisplayName { get => displayName; set => displayName = value; }

        /// <summary>Endpoint definitions in this catalog.</summary>
        public List<ApiEndpointCatalogEntry> Endpoints => endpoints;

        /// <summary>Returns the validated, typed catalog ID.</summary>
        public bool TryGetId(out ApiCatalogId id)
        {
            return ApiCatalogId.TryParse(catalogId, out id);
        }

        /// <summary>Finds an endpoint definition by stable ID.</summary>
        public bool TryGetEndpoint(ApiEndpointId id, out ApiEndpointCatalogEntry endpoint)
        {
            if (endpoints != null)
            {
                foreach (ApiEndpointCatalogEntry candidate in endpoints)
                {
                    ApiEndpointId candidateId;
                    if (candidate != null
                        && ApiEndpointId.TryParse(candidate.EndpointId, out candidateId)
                        && candidateId == id)
                    {
                        endpoint = candidate;
                        return true;
                    }
                }
            }

            endpoint = null;
            return false;
        }

        /// <summary>Validates the catalog ID, endpoint IDs, routes, and duplicate entries.</summary>
        public bool IsValid(out string message)
        {
            ApiCatalogId parsedCatalogId;
            if (!ApiCatalogId.TryParse(catalogId, out parsedCatalogId))
            {
                message = "Endpoint catalog has an invalid stable catalog ID: '"
                          + (catalogId ?? string.Empty) + "'.";
                return false;
            }

            if (endpoints == null || endpoints.Count == 0)
            {
                message = "Endpoint catalog '" + parsedCatalogId + "' must define at least one endpoint.";
                return false;
            }

            HashSet<ApiEndpointId> ids = new HashSet<ApiEndpointId>();
            foreach (ApiEndpointCatalogEntry endpoint in endpoints)
            {
                if (endpoint == null)
                {
                    message = "Catalog '" + parsedCatalogId + "' contains a null endpoint entry.";
                    return false;
                }

                string endpointMessage;
                if (!endpoint.IsValid(out endpointMessage))
                {
                    message = "Catalog '" + parsedCatalogId + "' contains an invalid endpoint. "
                              + endpointMessage;
                    return false;
                }

                ApiEndpointId endpointIdValue;
                ApiEndpointId.TryParse(endpoint.EndpointId, out endpointIdValue);
                if (!ids.Add(endpointIdValue))
                {
                    message = "Catalog '" + parsedCatalogId + "' contains duplicate endpoint ID '"
                              + endpointIdValue + "'.";
                    return false;
                }
            }

            message = null;
            return true;
        }

        private void OnValidate()
        {
            catalogId = catalogId?.Trim();
            displayName = displayName?.Trim();
        }
    }
}
