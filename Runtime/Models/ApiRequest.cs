using System;
using System.Collections.Generic;
using Deucarian.API.Configuration;

namespace Deucarian.API.Models
{
    /// <summary>
    /// Controls whether a request should use the configured authentication provider.
    /// </summary>
    public enum ApiAuthenticationRequirement
    {
        /// <summary>Use the authentication behavior from <c>ApiClientConfig</c>.</summary>
        UseConfigDefault,

        /// <summary>Require a token. The request fails if no token is available.</summary>
        Required,

        /// <summary>Use a token when available, but allow the request to continue without one.</summary>
        Optional,

        /// <summary>Never attach authentication for this request.</summary>
        Disabled
    }

    /// <summary>
    /// Describes how the request body should be written to UnityWebRequest.
    /// </summary>
    public enum ApiRequestBodyFormat
    {
        /// <summary>Serialize the body as JSON.</summary>
        Json = 0,

        /// <summary>Serialize the body as multipart form data.</summary>
        MultipartForm = 1,

        /// <summary>
        /// Legacy raw body mode. Use <see cref="RawText"/> or <see cref="RawBytes"/> for new code.
        /// </summary>
        [System.Obsolete("Use RawText or RawBytes so API can choose safe Content-Type defaults.")]
        Raw = 2,

        /// <summary>Send the body as UTF-8 text.</summary>
        RawText = 3,

        /// <summary>Send the body as raw bytes.</summary>
        RawBytes = 4
    }

    /// <summary>
    /// Advanced request model used by <c>IApiClient.SendAsync&lt;T&gt;</c>.
    /// For simple calls, prefer string endpoints or <see cref="ApiEndpoint"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// ApiRequest request = new ApiRequest("projects", HttpMethod.GET);
    /// request.QueryParameters["include"] = "users";
    /// ApiResult&lt;ProjectDto[]&gt; result = await apiClient.SendAsync&lt;ProjectDto[]&gt;(request);
    /// </code>
    /// </example>
    public class ApiRequest
    {
        /// <summary>
        /// Creates an advanced request.
        /// </summary>
        /// <param name="endpoint">Relative endpoint path or absolute URL.</param>
        /// <param name="method">HTTP method to send.</param>
        /// <param name="authentication">Authentication behavior for this request.</param>
        public ApiRequest(string endpoint,
                          HttpMethod method = HttpMethod.GET,
                          ApiAuthenticationRequirement authentication =
                                  ApiAuthenticationRequirement.UseConfigDefault)
        {
            Endpoint = endpoint;
            Method = method;
            Authentication = authentication;
        }

        /// <summary>Relative path or absolute URL for the request.</summary>
        public string Endpoint { get; }

        /// <summary>HTTP method used by the request.</summary>
        public HttpMethod Method { get; }

        /// <summary>Authentication behavior for this request.</summary>
        public ApiAuthenticationRequirement Authentication { get; }

        /// <summary>Optional body serialized according to <see cref="BodyFormat"/>.</summary>
        public object Body { get; set; }

        /// <summary>Per-request headers. These override config default headers with the same key.</summary>
        public Dictionary<string, string> Headers { get; } = new Dictionary<string, string>();

        /// <summary>Query parameters appended to the endpoint URL.</summary>
        public Dictionary<string, string> QueryParameters { get; } = new Dictionary<string, string>();

        /// <summary>Optional timeout override in seconds. Null uses the client config timeout.</summary>
        public int? TimeoutSeconds { get; set; }

        /// <summary>How the body should be encoded.</summary>
        public ApiRequestBodyFormat BodyFormat { get; set; } = ApiRequestBodyFormat.Json;

        /// <summary>
        /// Response format requested by this call. Auto infers from the generic response type.
        /// </summary>
        public ApiResponseFormat ResponseFormat { get; set; } = ApiResponseFormat.Auto;

        /// <summary>
        /// Optional JSON property-name casing override. Null uses the client configuration.
        /// </summary>
        public ApiJsonPropertyNamingPolicy? JsonPropertyNamingOverride { get; set; }

        /// <summary>
        /// Optional AssetBundle transport options used when <see cref="ResponseFormat"/> resolves to
        /// <see cref="ApiResponseFormat.AssetBundle"/>.
        /// </summary>
        public ApiAssetBundleRequestOptions AssetBundleOptions { get; set; }

        /// <summary>
        /// Resolved environment/client/endpoint policy metadata. The built-in transport applies its
        /// timeout while policy-aware callers and decorators may use its retry and rate-limit hints.
        /// </summary>
        public ApiRequestPolicy RequestPolicy { get; set; }

        /// <summary>
        /// Suppresses request, response, and error logging for this call.
        /// Enable this for credential exchange and token endpoints so neither
        /// request URLs nor response bodies can reach diagnostic output.
        /// </summary>
        public bool SuppressLogging { get; set; }

        /// <summary>Optional callback invoked while the underlying UnityWebRequest transfers data.</summary>
        public Action<ApiTransferProgress> TransferProgress { get; set; }

        /// <summary>
        /// Compatibility escape hatch for legacy callers that pass a bearer token directly.
        /// Prefer an <c>IApiAuthProvider</c> for new code.
        /// </summary>
        public string BearerTokenOverride { get; set; }
    }
}
