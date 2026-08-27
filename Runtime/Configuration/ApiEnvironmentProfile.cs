using System;
using System.Collections.Generic;
using Deucarian.API.Models;
using UnityEngine;

namespace Deucarian.API.Configuration
{
    /// <summary>
    /// Describes whether an environment profile is ready to resolve clients,
    /// intentionally awaiting host configuration, or malformed.
    /// </summary>
    public enum ApiEnvironmentProfileConfigurationState
    {
        /// <summary>The profile contains invalid or partially configured data.</summary>
        Invalid = 0,

        /// <summary>The profile has valid IDs and policies, but every client host is blank.</summary>
        NotConfigured = 1,

        /// <summary>Every named client has a valid absolute HTTP(S) host.</summary>
        Configured = 2
    }

    /// <summary>Environment-specific base URL and defaults for one named API client.</summary>
    [Serializable]
    public sealed class ApiNamedClientDefinition
    {
        [Tooltip("Stable client identifier referenced by endpoint catalogs, for example 'primary' or 'media'.")]
        [SerializeField] private string clientId;

        [Tooltip("Absolute HTTP(S) base URL for this client in this environment.")]
        [SerializeField] private string baseUrl;

        [Tooltip("Non-secret headers applied before endpoint and request headers.")]
        [SerializeField] private List<ApiKeyValuePair> defaultHeaders = new List<ApiKeyValuePair>();

        [Tooltip("Optional policy values layered over the environment defaults.")]
        [SerializeField] private ApiRequestPolicyDefinition requestPolicy = new ApiRequestPolicyDefinition();

        /// <summary>Stable client identifier referenced by endpoint catalogs.</summary>
        public string ClientId { get => clientId; set => clientId = value; }

        /// <summary>Absolute HTTP(S) base URL for this environment.</summary>
        public string BaseUrl { get => baseUrl; set => baseUrl = value; }

        /// <summary>Non-secret headers applied to this named client.</summary>
        public List<ApiKeyValuePair> DefaultHeaders => defaultHeaders;

        /// <summary>Policy values layered over environment defaults.</summary>
        public ApiRequestPolicyDefinition RequestPolicy
        {
            get => requestPolicy;
            set => requestPolicy = value ?? new ApiRequestPolicyDefinition();
        }

        internal bool IsValid(out string message)
        {
            ApiClientId parsedClientId;
            if (!ApiClientId.TryParse(clientId, out parsedClientId))
            {
                message = "Named client has an invalid stable client ID: '" + (clientId ?? string.Empty) + "'.";
                return false;
            }

            Uri uri;
            if (!Uri.TryCreate(baseUrl?.Trim(), UriKind.Absolute, out uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || string.IsNullOrWhiteSpace(uri.Host)
                || !string.IsNullOrEmpty(uri.UserInfo)
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.Fragment))
            {
                message = "Named client '" + parsedClientId
                          + "' requires an absolute HTTP(S) base URL without credentials, query, or fragment.";
                return false;
            }

            if (requestPolicy != null && !requestPolicy.IsValid(out message))
            {
                message = "Named client '" + parsedClientId + "' has an invalid request policy: " + message;
                return false;
            }

            message = null;
            return true;
        }
    }

    /// <summary>
    /// A vendor-neutral API environment profile. It maps stable named clients to environment-specific
    /// base URLs without storing active selection or authentication/session state.
    /// </summary>
    public sealed class ApiEnvironmentProfile : ScriptableObject
    {
        [Tooltip("Stable environment identifier such as 'development', 'acceptance', or 'production'.")]
        [SerializeField] private string environmentId;

        [Tooltip("Human-friendly environment name. This is safe to show in developer UI.")]
        [SerializeField] private string displayName;

        [Tooltip("Default request policy for clients and endpoints in this environment.")]
        [SerializeField] private ApiRequestPolicyDefinition defaultRequestPolicy =
                new ApiRequestPolicyDefinition();

        [Tooltip("Named API clients and their environment-specific base URLs.")]
        [SerializeField] private List<ApiNamedClientDefinition> clients =
                new List<ApiNamedClientDefinition>();

        /// <summary>Stable environment identifier.</summary>
        public string EnvironmentId { get => environmentId; set => environmentId = value; }

        /// <summary>Human-friendly environment name safe for status UI.</summary>
        public string DisplayName { get => displayName; set => displayName = value; }

        /// <summary>Policy values layered over package defaults.</summary>
        public ApiRequestPolicyDefinition DefaultRequestPolicy
        {
            get => defaultRequestPolicy;
            set => defaultRequestPolicy = value ?? new ApiRequestPolicyDefinition();
        }

        /// <summary>Named clients available in this environment.</summary>
        public List<ApiNamedClientDefinition> Clients => clients;

        /// <summary>Returns the validated, typed environment ID.</summary>
        public bool TryGetId(out ApiEnvironmentId id)
        {
            return ApiEnvironmentId.TryParse(environmentId, out id);
        }

        /// <summary>Finds a named client without resolving or exposing it through global state.</summary>
        public bool TryGetClient(ApiClientId id, out ApiNamedClientDefinition client)
        {
            if (clients != null)
            {
                foreach (ApiNamedClientDefinition candidate in clients)
                {
                    ApiClientId candidateId;
                    if (candidate != null
                        && ApiClientId.TryParse(candidate.ClientId, out candidateId)
                        && candidateId == id)
                    {
                        client = candidate;
                        return true;
                    }
                }
            }

            client = null;
            return false;
        }

        /// <summary>
        /// Classifies a profile without treating an intentionally blank set of
        /// client hosts as malformed. A mixture of blank and populated hosts is
        /// invalid so partially configured profiles can never resolve traffic.
        /// </summary>
        public ApiEnvironmentProfileConfigurationState ClassifyConfiguration(
            out string message)
        {
            ApiEnvironmentId parsedEnvironmentId;
            if (!ApiEnvironmentId.TryParse(environmentId, out parsedEnvironmentId))
            {
                message = "Environment profile has an invalid stable environment ID: '"
                          + (environmentId ?? string.Empty) + "'.";
                return ApiEnvironmentProfileConfigurationState.Invalid;
            }

            if (defaultRequestPolicy != null && !defaultRequestPolicy.IsValid(out message))
            {
                message = "Environment '" + parsedEnvironmentId
                          + "' has an invalid default policy: " + message;
                return ApiEnvironmentProfileConfigurationState.Invalid;
            }

            if (clients == null || clients.Count == 0)
            {
                message = "Environment '" + parsedEnvironmentId
                          + "' must define at least one named client.";
                return ApiEnvironmentProfileConfigurationState.Invalid;
            }

            bool hasBlankHost = false;
            bool hasConfiguredHost = false;
            HashSet<ApiClientId> ids = new HashSet<ApiClientId>();
            foreach (ApiNamedClientDefinition client in clients)
            {
                if (client == null)
                {
                    message = "Environment '" + parsedEnvironmentId
                              + "' contains a null client entry.";
                    return ApiEnvironmentProfileConfigurationState.Invalid;
                }

                ApiClientId parsedClientId;
                if (!ApiClientId.TryParse(client.ClientId, out parsedClientId))
                {
                    message = "Environment '" + parsedEnvironmentId
                              + "' contains an invalid client. Named client has an invalid stable client ID: '"
                              + (client.ClientId ?? string.Empty) + "'.";
                    return ApiEnvironmentProfileConfigurationState.Invalid;
                }

                if (!ids.Add(parsedClientId))
                {
                    message = "Environment '" + parsedEnvironmentId
                              + "' contains duplicate client ID '" + parsedClientId + "'.";
                    return ApiEnvironmentProfileConfigurationState.Invalid;
                }

                string policyMessage;
                if (client.RequestPolicy != null
                    && !client.RequestPolicy.IsValid(out policyMessage))
                {
                    message = "Environment '" + parsedEnvironmentId
                              + "' contains an invalid client. Named client '"
                              + parsedClientId + "' has an invalid request policy: "
                              + policyMessage;
                    return ApiEnvironmentProfileConfigurationState.Invalid;
                }

                if (string.IsNullOrWhiteSpace(client.BaseUrl))
                {
                    hasBlankHost = true;
                    continue;
                }

                string clientMessage;
                if (!client.IsValid(out clientMessage))
                {
                    message = "Environment '" + parsedEnvironmentId
                              + "' contains an invalid client. " + clientMessage;
                    return ApiEnvironmentProfileConfigurationState.Invalid;
                }

                hasConfiguredHost = true;
            }

            if (hasBlankHost && hasConfiguredHost)
            {
                message = "Environment '" + parsedEnvironmentId
                          + "' is partially configured; every named client must either have a valid host or all hosts must be blank.";
                return ApiEnvironmentProfileConfigurationState.Invalid;
            }

            message = null;
            return hasBlankHost
                ? ApiEnvironmentProfileConfigurationState.NotConfigured
                : ApiEnvironmentProfileConfigurationState.Configured;
        }

        /// <summary>Validates stable IDs, client uniqueness, URLs, and policy data.</summary>
        public bool IsValid(out string message)
        {
            ApiEnvironmentId parsedEnvironmentId;
            if (!ApiEnvironmentId.TryParse(environmentId, out parsedEnvironmentId))
            {
                message = "Environment profile has an invalid stable environment ID: '"
                          + (environmentId ?? string.Empty) + "'.";
                return false;
            }

            if (defaultRequestPolicy != null && !defaultRequestPolicy.IsValid(out message))
            {
                message = "Environment '" + parsedEnvironmentId + "' has an invalid default policy: " + message;
                return false;
            }

            if (clients == null || clients.Count == 0)
            {
                message = "Environment '" + parsedEnvironmentId + "' must define at least one named client.";
                return false;
            }

            HashSet<ApiClientId> ids = new HashSet<ApiClientId>();
            foreach (ApiNamedClientDefinition client in clients)
            {
                if (client == null)
                {
                    message = "Environment '" + parsedEnvironmentId + "' contains a null client entry.";
                    return false;
                }

                string clientMessage;
                if (!client.IsValid(out clientMessage))
                {
                    message = "Environment '" + parsedEnvironmentId + "' contains an invalid client. "
                              + clientMessage;
                    return false;
                }

                ApiClientId clientIdValue;
                ApiClientId.TryParse(client.ClientId, out clientIdValue);
                if (!ids.Add(clientIdValue))
                {
                    message = "Environment '" + parsedEnvironmentId + "' contains duplicate client ID '"
                              + clientIdValue + "'.";
                    return false;
                }
            }

            message = null;
            return true;
        }

        private void OnValidate()
        {
            environmentId = environmentId?.Trim();
            displayName = displayName?.Trim();
        }
    }
}
