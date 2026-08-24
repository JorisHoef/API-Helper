using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.API.Authentication;
using Deucarian.API.Certificates;
using Deucarian.API.Configuration;
using Deucarian.API.Models;
using Deucarian.API.Services.MultipartForm;
using UnityEngine;
using UnityEngine.Networking;

namespace Deucarian.API.Core
{
    internal sealed class UnityWebRequestBuilder : IRequestBuilder
    {
        private readonly ApiClientConfig _config;
        private readonly IApiSerializer _serializer;
        private readonly IApiAuthProvider _authProvider;
        private readonly ICertificateHandlerFactory _certificateHandlerFactory;

        public UnityWebRequestBuilder(ApiClientConfig config,
                                      IApiSerializer serializer,
                                      IApiAuthProvider authProvider,
                                      ICertificateHandlerFactory certificateHandlerFactory)
        {
            _config = config ?? ApiClientConfig.CreateRuntimeDefault();
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _authProvider = authProvider;
            _certificateHandlerFactory = certificateHandlerFactory;
        }

        public async Task<UnityWebRequest> BuildAsync(ApiRequest request,
                                                      ApiResponseFormat responseFormat,
                                                      CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.Endpoint))
            {
                throw new ArgumentException("API request endpoint cannot be empty.", nameof(request));
            }

            string url = BuildUrl(_config.BaseUrl, request.Endpoint, request.QueryParameters);
            UnityWebRequest webRequest = CreateRequest(url, request, responseFormat);

            ApplyTimeout(webRequest, request);
            ApplyCertificateHandler(webRequest, url);
            ApplyHeaders(webRequest, request, responseFormat);

            string bearerToken = await ResolveBearerTokenAsync(request, cancellationToken);
            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                webRequest.SetRequestHeader("Authorization", "Bearer " + bearerToken);
            }

            return webRequest;
        }

        private UnityWebRequest CreateRequest(string url,
                                              ApiRequest request,
                                              ApiResponseFormat responseFormat)
        {
            if (responseFormat == ApiResponseFormat.AssetBundle)
            {
                return CreateAssetBundleRequest(url, request);
            }

            if (request.BodyFormat == ApiRequestBodyFormat.MultipartForm)
            {
                return CreateMultipartRequest(url, request, responseFormat);
            }

            UnityWebRequest webRequest = new UnityWebRequest(url, request.Method.ToString())
            {
                    downloadHandler = CreateDownloadHandler(responseFormat)
            };

            if (RequestCanHaveBody(request.Method) && request.Body != null)
            {
                byte[] payload = CreatePayload(request);
                webRequest.uploadHandler = new UploadHandlerRaw(payload);
                webRequest.disposeUploadHandlerOnDispose = true;
            }

            return webRequest;
        }

        private static UnityWebRequest CreateAssetBundleRequest(string url, ApiRequest request)
        {
            if (request.Method != HttpMethod.GET)
            {
                throw new InvalidOperationException("AssetBundle responses are supported for GET requests only.");
            }

            if (request.Body != null)
            {
                throw new InvalidOperationException("AssetBundle GET requests cannot include a request body.");
            }

            ApiAssetBundleRequestOptions options = request.AssetBundleOptions;
            uint crc = options != null ? options.Crc : 0;

            if (options == null || options.CacheMode == ApiAssetBundleCacheMode.Disabled)
            {
                return UnityWebRequestAssetBundle.GetAssetBundle(url, crc);
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            return UnityWebRequestAssetBundle.GetAssetBundle(url, crc);
#else
            Hash128 hash;
            if (TryGetCacheHash(options.CacheHash, out hash))
            {
                if (!string.IsNullOrWhiteSpace(options.CacheKey))
                {
                    return UnityWebRequestAssetBundle.GetAssetBundle(
                        url,
                        new CachedAssetBundle(options.CacheKey.Trim(), hash),
                        crc);
                }

                return UnityWebRequestAssetBundle.GetAssetBundle(url, hash, crc);
            }

            if (options.CacheVersion.HasValue)
            {
                return UnityWebRequestAssetBundle.GetAssetBundle(url, options.CacheVersion.Value, crc);
            }

            return UnityWebRequestAssetBundle.GetAssetBundle(url, crc);
#endif
        }

        private UnityWebRequest CreateMultipartRequest(string url,
                                                       ApiRequest request,
                                                       ApiResponseFormat responseFormat)
        {
            if (request.Method != HttpMethod.POST)
            {
                throw new InvalidOperationException("MultipartForm requests are supported for POST requests only.");
            }

            List<IMultipartFormSection> form = CreateMultipartForm(request.Body);
            UnityWebRequest webRequest = UnityWebRequest.Post(url, form);
            webRequest.downloadHandler?.Dispose();
            webRequest.downloadHandler = CreateDownloadHandler(responseFormat);
            return webRequest;
        }

        private static DownloadHandler CreateDownloadHandler(ApiResponseFormat responseFormat)
        {
            switch (responseFormat)
            {
                case ApiResponseFormat.Texture:
                    return new DownloadHandlerTexture(true);
                default:
                    return new DownloadHandlerBuffer();
            }
        }

        private byte[] CreatePayload(ApiRequest request)
        {
#pragma warning disable CS0618
            switch (request.BodyFormat)
            {
                case ApiRequestBodyFormat.RawBytes:
                    if (request.Body is byte[] bytes)
                    {
                        return bytes;
                    }

                    throw new InvalidOperationException("RawBytes request bodies must be byte[].");

                case ApiRequestBodyFormat.RawText:
                    return Encoding.UTF8.GetBytes(request.Body?.ToString() ?? string.Empty);

                case ApiRequestBodyFormat.Raw:
                    if (request.Body is byte[] legacyBytes)
                    {
                        return legacyBytes;
                    }

                    return Encoding.UTF8.GetBytes(request.Body?.ToString() ?? string.Empty);

                case ApiRequestBodyFormat.Json:
                    return Encoding.UTF8.GetBytes(
                            _serializer.Serialize(
                                request.Body,
                                request.JsonPropertyNamingOverride));

                case ApiRequestBodyFormat.MultipartForm:
                default:
                    throw new InvalidOperationException("MultipartForm bodies must be created with UnityWebRequest.Post.");
            }
#pragma warning restore CS0618
        }

        private static List<IMultipartFormSection> CreateMultipartForm(object body)
        {
            if (body is IMultiFormPropertyAdder multiFormPropertyAdder)
            {
                List<IMultipartFormSection> form = new List<IMultipartFormSection>();
                multiFormPropertyAdder.AddPropertiesToForm(form);
                return form;
            }

            if (body is List<IMultipartFormSection> formSections)
            {
                return formSections;
            }

            if (body is IEnumerable<IMultipartFormSection> enumerableSections)
            {
                return new List<IMultipartFormSection>(enumerableSections);
            }

            throw new InvalidOperationException(
                    "MultipartForm request bodies must implement IMultiFormPropertyAdder or provide IMultipartFormSection entries.");
        }

        private void ApplyTimeout(UnityWebRequest webRequest, ApiRequest request)
        {
            int timeout = request.TimeoutSeconds.HasValue
                                  ? request.TimeoutSeconds.Value
                                  : request.RequestPolicy != null
                                          ? request.RequestPolicy.TimeoutSeconds
                                          : _config.TimeoutSeconds;
            if (timeout > 0)
            {
                webRequest.timeout = timeout;
            }
        }

        private void ApplyCertificateHandler(UnityWebRequest webRequest, string url)
        {
            CertificateHandler handler = _certificateHandlerFactory?.CreateFor(url);
            if (handler == null)
            {
                return;
            }

            webRequest.certificateHandler = handler;
            webRequest.disposeCertificateHandlerOnDispose = true;
        }

        private void ApplyHeaders(UnityWebRequest webRequest,
                                  ApiRequest request,
                                  ApiResponseFormat responseFormat)
        {
            Dictionary<string, string> headers =
                    new Dictionary<string, string>(_config.GetDefaultHeaderDictionary(),
                                                   StringComparer.OrdinalIgnoreCase);

            if (request.Headers != null)
            {
                foreach (KeyValuePair<string, string> header in request.Headers)
                {
                    if (!string.IsNullOrWhiteSpace(header.Key))
                    {
                        headers[header.Key] = header.Value;
                    }
                }
            }

            if (!ContainsHeader(headers, "Accept"))
            {
                headers["Accept"] = ApiResponseFormatUtility.GetAcceptHeader(responseFormat);
            }

            string defaultContentType =
                    ApiResponseFormatUtility.GetDefaultContentType(request.BodyFormat, request.Body);
            if (RequestCanHaveBody(request.Method)
                && request.Body != null
                && !string.IsNullOrWhiteSpace(defaultContentType)
                && !ContainsHeader(headers, "Content-Type"))
            {
                headers["Content-Type"] = defaultContentType;
            }

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (!string.IsNullOrWhiteSpace(header.Key))
                {
                    webRequest.SetRequestHeader(header.Key, header.Value);
                }
            }
        }

        private async Task<string> ResolveBearerTokenAsync(ApiRequest request,
                                                           CancellationToken cancellationToken)
        {
            if (request.Authentication == ApiAuthenticationRequirement.Disabled)
            {
                return null;
            }

            bool configRequestsAuth = _config.AuthenticationMode == ApiAuthenticationMode.BearerToken;
            bool shouldAuthenticate = request.Authentication == ApiAuthenticationRequirement.Required
                                      || request.Authentication == ApiAuthenticationRequirement.Optional
                                      || (request.Authentication == ApiAuthenticationRequirement.UseConfigDefault
                                          && configRequestsAuth);
            bool isRequired = request.Authentication == ApiAuthenticationRequirement.Required
                              || (request.Authentication == ApiAuthenticationRequirement.UseConfigDefault
                                  && configRequestsAuth);

            if (!shouldAuthenticate)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(request.BearerTokenOverride))
            {
                return request.BearerTokenOverride;
            }

            if (_authProvider == null)
            {
                if (isRequired)
                {
                    throw new ApiAuthenticationException("Authentication is required, but no API auth provider is configured.");
                }

                return null;
            }

            string token = await _authProvider.GetAccessTokenAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(token) && isRequired)
            {
                throw new ApiAuthenticationException("Authentication is required, but the API auth provider returned an empty token.");
            }

            return token;
        }

        private static string BuildUrl(string baseUrl,
                                       string endpoint,
                                       Dictionary<string, string> queryParameters)
        {
            string url = IsAbsoluteUrl(endpoint) || string.IsNullOrWhiteSpace(baseUrl)
                                 ? endpoint
                                 : baseUrl.TrimEnd('/') + "/" + endpoint.TrimStart('/');

            if (queryParameters == null || queryParameters.Count == 0)
            {
                return url;
            }

            StringBuilder builder = new StringBuilder(url);
            builder.Append(url.Contains("?") ? "&" : "?");

            bool first = true;
            foreach (KeyValuePair<string, string> pair in queryParameters)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                if (!first)
                {
                    builder.Append('&');
                }

                builder.Append(UnityWebRequest.EscapeURL(pair.Key));
                builder.Append('=');
                builder.Append(UnityWebRequest.EscapeURL(pair.Value ?? string.Empty));
                first = false;
            }

            return builder.ToString();
        }

        private static bool IsAbsoluteUrl(string endpoint)
        {
            return Uri.TryCreate(endpoint, UriKind.Absolute, out _);
        }

        private static bool RequestCanHaveBody(HttpMethod method)
        {
            return method == HttpMethod.POST || method == HttpMethod.PUT || method == HttpMethod.PATCH;
        }

        private static bool TryGetCacheHash(string cacheHash, out Hash128 hash)
        {
            hash = default(Hash128);
            if (string.IsNullOrWhiteSpace(cacheHash))
            {
                return false;
            }

            try
            {
                hash = Hash128.Parse(cacheHash.Trim());
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool ContainsHeader(Dictionary<string, string> headers, string key)
        {
            foreach (string existingKey in headers.Keys)
            {
                if (string.Equals(existingKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
