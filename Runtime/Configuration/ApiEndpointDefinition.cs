using System.Collections.Generic;
using Deucarian.API.Models;
using UnityEngine;

namespace Deucarian.API.Configuration
{
    /// <summary>
    /// Designer-friendly endpoint asset. It converts to <see cref="ApiEndpoint"/> at runtime
    /// so code-defined and ScriptableObject endpoints use the same request path.
    /// </summary>
    /// <example>
    /// <code>
    /// ApiResult&lt;ProjectDto[]&gt; result =
    ///     await apiClient.SendAsync&lt;ProjectDto[]&gt;(getProjectsEndpoint, cancellationToken);
    /// </code>
    /// </example>
    [CreateAssetMenu(menuName = "Deucarian/API/Advanced/Building Blocks/Endpoint Definition", fileName = "ApiEndpointDefinition")]
    public sealed class ApiEndpointDefinition : ScriptableObject
    {
        [Tooltip("Friendly name shown in project assets and inspector fields.")]
        [SerializeField] private string displayName;

        [Tooltip("Relative endpoint path such as 'projects', or an absolute URL for external resources.")]
        [SerializeField] private string path;

        [Tooltip("HTTP method used by this endpoint.")]
        [SerializeField] private HttpMethod method = HttpMethod.GET;

        [Tooltip("Authentication behavior for this endpoint. Use Config Default to follow the ApiClientConfig.")]
        [SerializeField] private ApiAuthenticationRequirement authentication =
                ApiAuthenticationRequirement.UseConfigDefault;

        [Tooltip("Optional timeout override in seconds. Set 0 to use the ApiClientConfig timeout.")]
        [SerializeField] private int timeoutOverrideSeconds;

        [Tooltip("Response format for this endpoint. Auto infers from the generic response type.")]
        [SerializeField] private ApiResponseFormat responseFormat = ApiResponseFormat.Auto;

        [Tooltip("Override the client JSON property-name casing for this endpoint only.")]
        [SerializeField] private bool overrideJsonPropertyNaming;

        [Tooltip("JSON property-name casing used when the endpoint override is enabled.")]
        [SerializeField] private ApiJsonPropertyNamingPolicy jsonPropertyNaming =
                ApiJsonPropertyNamingPolicy.SnakeCase;

        [Tooltip("Headers applied to requests created from this endpoint.")]
        [SerializeField] private List<ApiKeyValuePair> defaultHeaders = new List<ApiKeyValuePair>();

        [Tooltip("Query parameters applied to requests created from this endpoint.")]
        [SerializeField] private List<ApiKeyValuePair> defaultQueryParameters = new List<ApiKeyValuePair>();

        /// <summary>Friendly inspector/display name.</summary>
        public string DisplayName
        {
            get => displayName;
            set => displayName = value;
        }

        /// <summary>Relative endpoint path or absolute URL.</summary>
        public string Path
        {
            get => path;
            set => path = value;
        }

        /// <summary>HTTP method used by this endpoint.</summary>
        public HttpMethod Method
        {
            get => method;
            set => method = value;
        }

        /// <summary>Authentication behavior used by this endpoint.</summary>
        public ApiAuthenticationRequirement Authentication
        {
            get => authentication;
            set => authentication = value;
        }

        /// <summary>Optional timeout override in seconds. 0 means use the client config timeout.</summary>
        public int TimeoutOverrideSeconds
        {
            get => timeoutOverrideSeconds;
            set => timeoutOverrideSeconds = value < 0 ? 0 : value;
        }

        /// <summary>Response format hint for requests created from this endpoint.</summary>
        public ApiResponseFormat ResponseFormat
        {
            get => responseFormat;
            set => responseFormat = value;
        }

        /// <summary>Whether this endpoint overrides the client JSON naming policy.</summary>
        public bool OverrideJsonPropertyNaming
        {
            get => overrideJsonPropertyNaming;
            set => overrideJsonPropertyNaming = value;
        }

        /// <summary>JSON naming policy used when <see cref="OverrideJsonPropertyNaming"/> is true.</summary>
        public ApiJsonPropertyNamingPolicy JsonPropertyNaming
        {
            get => jsonPropertyNaming;
            set => jsonPropertyNaming = value;
        }

        /// <summary>Headers applied to requests created from this endpoint.</summary>
        public List<ApiKeyValuePair> DefaultHeaders => defaultHeaders;

        /// <summary>Query parameters applied to requests created from this endpoint.</summary>
        public List<ApiKeyValuePair> DefaultQueryParameters => defaultQueryParameters;

        /// <summary>
        /// Converts this asset to the shared code endpoint model.
        /// </summary>
        /// <returns>A new <see cref="ApiEndpoint"/> with this asset's settings.</returns>
        public ApiEndpoint ToEndpoint()
        {
            Dictionary<string, string> headers =
                    new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string> query = new Dictionary<string, string>();
            ApiClientConfig.AddPairsToDictionary(defaultHeaders, headers);
            ApiClientConfig.AddPairsToDictionary(defaultQueryParameters, query);

            return new ApiEndpoint(path,
                                   method,
                                   authentication,
                                   timeoutOverrideSeconds > 0 ? (int?)timeoutOverrideSeconds : null,
                                   headers,
                                   query,
                                   responseFormat,
                                   jsonPropertyNamingOverride:
                                           overrideJsonPropertyNaming
                                                   ? jsonPropertyNaming
                                                   : (ApiJsonPropertyNamingPolicy?)null);
        }

        /// <summary>
        /// Creates an ApiRequest using the same path as code-defined ApiEndpoint objects.
        /// </summary>
        /// <param name="body">Optional request body.</param>
        /// <returns>A new <see cref="ApiRequest"/> configured from this asset.</returns>
        public ApiRequest CreateRequest(object body = null)
        {
            return ToEndpoint().CreateRequest(body);
        }

        /// <summary>
        /// Checks whether this endpoint asset is ready to create requests.
        /// </summary>
        /// <param name="message">Validation message when the definition is invalid.</param>
        /// <returns>True when the endpoint can create requests without unresolved placeholders.</returns>
        public bool IsValid(out string message)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                message = "Endpoint path cannot be empty.";
                return false;
            }

            string unresolvedParameter = ApiEndpoint.GetFirstUnresolvedPathParameter(path);
            if (!string.IsNullOrEmpty(unresolvedParameter))
            {
                message = "ScriptableObject endpoint path cannot contain unresolved parameter "
                          + unresolvedParameter + ". Use a code-defined ApiEndpoint for path parameters.";
                return false;
            }

            message = null;
            return true;
        }

        private void OnValidate()
        {
            if (timeoutOverrideSeconds < 0)
            {
                timeoutOverrideSeconds = 0;
            }

            if (!string.IsNullOrWhiteSpace(path))
            {
                path = path.Trim();
            }
        }
    }
}
