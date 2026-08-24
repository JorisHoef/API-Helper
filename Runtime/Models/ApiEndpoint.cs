using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Deucarian.API.Configuration;
using UnityEngine.Networking;

namespace Deucarian.API.Models
{
    /// <summary>
    /// Lightweight code-defined endpoint description.
    /// Use raw string constants for simple routes, and ApiEndpoint when method/auth/defaults
    /// should travel with the endpoint.
    /// </summary>
    /// <example>
    /// <code>
    /// ApiEndpoint endpoint = new ApiEndpoint("projects/{id}", HttpMethod.GET)
    ///     .WithPathParameter("id", projectId);
    /// </code>
    /// </example>
    public sealed class ApiEndpoint
    {
        private static readonly Regex PathParameterRegex =
                new Regex(@"\{(?<name>[A-Za-z0-9_]+)\}", RegexOptions.Compiled);

        /// <summary>
        /// Creates a code-defined endpoint.
        /// </summary>
        /// <param name="path">Relative endpoint path or absolute URL. Placeholders such as <c>{id}</c> can be replaced with <see cref="WithPathParameter"/>.</param>
        /// <param name="method">HTTP method used when requests are created from this endpoint.</param>
        /// <param name="authentication">Authentication behavior for requests created from this endpoint.</param>
        /// <param name="timeoutSeconds">Optional timeout override in seconds.</param>
        /// <param name="defaultHeaders">Optional headers copied to requests created from this endpoint.</param>
        /// <param name="defaultQueryParameters">Optional query parameters copied to requests created from this endpoint.</param>
        /// <param name="responseFormat">Optional response format hint. Auto infers from the generic response type.</param>
        /// <param name="requestPolicy">Optional resolved request policy metadata.</param>
        /// <param name="suppressLogging">True when requests created from this endpoint must not reach API logs.</param>
        /// <param name="jsonPropertyNamingOverride">Optional request/response DTO naming override.</param>
        public ApiEndpoint(string path,
                           HttpMethod method = HttpMethod.GET,
                           ApiAuthenticationRequirement authentication =
                                   ApiAuthenticationRequirement.UseConfigDefault,
                           int? timeoutSeconds = null,
                           IEnumerable<KeyValuePair<string, string>> defaultHeaders = null,
                           IEnumerable<KeyValuePair<string, string>> defaultQueryParameters = null,
                           ApiResponseFormat responseFormat = ApiResponseFormat.Auto,
                           ApiRequestPolicy requestPolicy = null,
                           bool suppressLogging = false,
                           ApiJsonPropertyNamingPolicy? jsonPropertyNamingOverride = null)
        {
            Path = path;
            Method = method;
            Authentication = authentication;
            TimeoutSeconds = timeoutSeconds;
            DefaultHeaders = Copy(defaultHeaders, StringComparer.OrdinalIgnoreCase);
            DefaultQueryParameters = Copy(defaultQueryParameters, StringComparer.Ordinal);
            ResponseFormat = responseFormat;
            RequestPolicy = requestPolicy;
            SuppressLogging = suppressLogging;
            JsonPropertyNamingOverride = jsonPropertyNamingOverride;
        }

        /// <summary>Relative path or absolute URL. Supports placeholders such as <c>projects/{id}</c>.</summary>
        public string Path { get; }

        /// <summary>HTTP method for this endpoint.</summary>
        public HttpMethod Method { get; }

        /// <summary>Authentication behavior for this endpoint.</summary>
        public ApiAuthenticationRequirement Authentication { get; }

        /// <summary>Optional timeout override in seconds.</summary>
        public int? TimeoutSeconds { get; }

        /// <summary>Response format hint for requests created from this endpoint.</summary>
        public ApiResponseFormat ResponseFormat { get; }

        /// <summary>Headers copied onto requests created from this endpoint.</summary>
        public IReadOnlyDictionary<string, string> DefaultHeaders { get; }

        /// <summary>Query parameters copied onto requests created from this endpoint.</summary>
        public IReadOnlyDictionary<string, string> DefaultQueryParameters { get; }

        /// <summary>Optional resolved environment/client/endpoint policy metadata.</summary>
        public ApiRequestPolicy RequestPolicy { get; }

        /// <summary>True when requests created from this endpoint must not reach API logs.</summary>
        public bool SuppressLogging { get; }

        /// <summary>Optional request/response DTO naming override.</summary>
        public ApiJsonPropertyNamingPolicy? JsonPropertyNamingOverride { get; }

        /// <summary>
        /// Creates a request from this endpoint. Throws if the path still contains unresolved placeholders.
        /// </summary>
        /// <param name="body">Optional request body.</param>
        /// <returns>A new <see cref="ApiRequest"/> populated from this endpoint.</returns>
        public ApiRequest CreateRequest(object body = null)
        {
            EnsureNoUnresolvedPathParameters(Path);

            ApiRequest request = new ApiRequest(Path, Method, Authentication)
            {
                    Body = body,
                    TimeoutSeconds = TimeoutSeconds,
                    ResponseFormat = ResponseFormat,
                    RequestPolicy = RequestPolicy,
                    SuppressLogging = SuppressLogging,
                    JsonPropertyNamingOverride = JsonPropertyNamingOverride
            };

            CopyInto(DefaultHeaders, request.Headers);
            CopyInto(DefaultQueryParameters, request.QueryParameters);
            return request;
        }

        /// <summary>
        /// Returns a copy with one path placeholder replaced and URL escaped.
        /// </summary>
        /// <param name="name">Placeholder name without braces, for example <c>id</c> for <c>{id}</c>.</param>
        /// <param name="value">Value to URL escape and insert into the path.</param>
        /// <returns>A copy of this endpoint with the placeholder replaced.</returns>
        /// <exception cref="ArgumentException">Thrown when the placeholder name is empty or does not exist in the path.</exception>
        public ApiEndpoint WithPathParameter(string name, object value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Path parameter name cannot be empty.", nameof(name));
            }

            string placeholder = "{" + name + "}";
            if (Path == null || !Path.Contains(placeholder))
            {
                throw new ArgumentException("Endpoint path does not contain parameter " + placeholder + ".", nameof(name));
            }

            string escapedValue = UnityWebRequest.EscapeURL(value?.ToString() ?? string.Empty)
                                                  .Replace("+", "%20");
            return WithPath(Path.Replace(placeholder, escapedValue));
        }

        /// <summary>
        /// Returns a copy with all supplied path placeholders replaced and URL escaped.
        /// </summary>
        /// <param name="parameters">Placeholder names and values to insert into the path.</param>
        /// <returns>A copy of this endpoint with all supplied placeholders replaced.</returns>
        public ApiEndpoint WithPathParameters(IDictionary<string, object> parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            ApiEndpoint endpoint = this;
            foreach (KeyValuePair<string, object> parameter in parameters)
            {
                endpoint = endpoint.WithPathParameter(parameter.Key, parameter.Value);
            }

            return endpoint;
        }

        /// <summary>Returns a copy with an extra default header.</summary>
        /// <param name="key">Header name.</param>
        /// <param name="value">Header value.</param>
        /// <returns>A copy of this endpoint with the header added or replaced.</returns>
        public ApiEndpoint WithHeader(string key, string value)
        {
            Dictionary<string, string> headers =
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            CopyInto(DefaultHeaders, headers);
            headers[key] = value;
            return new ApiEndpoint(Path,
                                   Method,
                                   Authentication,
                                   TimeoutSeconds,
                                   headers,
                                   DefaultQueryParameters,
                                   ResponseFormat,
                                   RequestPolicy,
                                   SuppressLogging,
                                   JsonPropertyNamingOverride);
        }

        /// <summary>Returns a copy with an extra default query parameter.</summary>
        /// <param name="key">Query parameter name.</param>
        /// <param name="value">Query parameter value.</param>
        /// <returns>A copy of this endpoint with the query parameter added or replaced.</returns>
        public ApiEndpoint WithQueryParameter(string key, string value)
        {
            Dictionary<string, string> query = new Dictionary<string, string>();
            CopyInto(DefaultQueryParameters, query);
            query[key] = value;
            return new ApiEndpoint(Path,
                                   Method,
                                   Authentication,
                                   TimeoutSeconds,
                                   DefaultHeaders,
                                   query,
                                   ResponseFormat,
                                   RequestPolicy,
                                   SuppressLogging,
                                   JsonPropertyNamingOverride);
        }

        private ApiEndpoint WithPath(string path)
        {
            return new ApiEndpoint(path,
                                   Method,
                                   Authentication,
                                   TimeoutSeconds,
                                   DefaultHeaders,
                                   DefaultQueryParameters,
                                   ResponseFormat,
                                   RequestPolicy,
                                   SuppressLogging,
                                   JsonPropertyNamingOverride);
        }

        internal static bool HasUnresolvedPathParameters(string path)
        {
            return !string.IsNullOrEmpty(path) && PathParameterRegex.IsMatch(path);
        }

        internal static string GetFirstUnresolvedPathParameter(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            Match match = PathParameterRegex.Match(path);
            return match.Success ? match.Value : null;
        }

        private static void EnsureNoUnresolvedPathParameters(string path)
        {
            string unresolved = GetFirstUnresolvedPathParameter(path);
            if (!string.IsNullOrEmpty(unresolved))
            {
                throw new InvalidOperationException("Endpoint path contains unresolved parameter " + unresolved + ".");
            }
        }

        private static IReadOnlyDictionary<string, string> Copy(
                IEnumerable<KeyValuePair<string, string>> source,
                IEqualityComparer<string> comparer)
        {
            Dictionary<string, string> copy =
                    new Dictionary<string, string>(comparer ?? StringComparer.Ordinal);
            if (source == null)
            {
                return copy;
            }

            foreach (KeyValuePair<string, string> pair in source)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key))
                {
                    copy[pair.Key] = pair.Value;
                }
            }

            return copy;
        }

        private static void CopyInto(IReadOnlyDictionary<string, string> source,
                                     IDictionary<string, string> destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> pair in source)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key))
                {
                    destination[pair.Key] = pair.Value;
                }
            }
        }
    }
}
