using System;
using System.Text;
using Deucarian.API.Models;
using UnityEngine;

namespace Deucarian.API.Core
{
    internal sealed class ApiResponseParser : IApiResponseParser
    {
        private readonly IApiSerializer _serializer;

        public ApiResponseParser(IApiSerializer serializer)
        {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public ApiResult<TResponse> Parse<TResponse>(ApiRequest request,
                                                     ApiTransportResponse response,
                                                     ApiResponseFormat responseFormat)
        {
            TResponse data = ParseData<TResponse>(
                    response,
                    responseFormat,
                    request?.JsonPropertyNamingOverride);

            return ApiResult<TResponse>.Success(data,
                                                request?.Method ?? HttpMethod.GET,
                                                response?.StatusCode,
                                                response?.RequestUrl ?? request?.Endpoint,
                                                response?.RawBody);
        }

        private TResponse ParseData<TResponse>(ApiTransportResponse response,
                                               ApiResponseFormat responseFormat,
                                               Configuration.ApiJsonPropertyNamingPolicy? propertyNamingOverride)
        {
            Type responseType = typeof(TResponse);
            string body = response?.RawBody;

            switch (responseFormat)
            {
                case ApiResponseFormat.Text:
                    EnsureType<TResponse>(typeof(string), responseFormat);
                    return (TResponse)(object)(body ?? string.Empty);

                case ApiResponseFormat.Bytes:
                    EnsureType<TResponse>(typeof(byte[]), responseFormat);
                    return (TResponse)(object)(response?.RawBytes
                                               ?? Encoding.UTF8.GetBytes(body ?? string.Empty));

                case ApiResponseFormat.Texture:
                    EnsureTextureType(responseType, responseFormat);
                    if (response?.RawBytes != null
                        && response.RawBytes.Length > 0
                        && response.Texture == null)
                    {
                        throw new InvalidOperationException(
                                "Texture response could not be decoded" + GetRequestUrlSuffix(response) + ".");
                    }

                    return (TResponse)(object)response?.Texture;

                case ApiResponseFormat.AssetBundle:
                    EnsureAssetBundleType(responseType, responseFormat);
                    if (response?.AssetBundle == null)
                    {
                        throw new InvalidOperationException(
                                "AssetBundle response could not be decoded" + GetRequestUrlSuffix(response) + ".");
                    }

                    return (TResponse)(object)response.AssetBundle;

                case ApiResponseFormat.Json:
                case ApiResponseFormat.Auto:
                default:
                    if (string.IsNullOrWhiteSpace(body))
                    {
                        return default;
                    }

                    return _serializer.Deserialize<TResponse>(
                            body,
                            propertyNamingOverride);
            }
        }

        private static void EnsureType<TResponse>(Type expectedType, ApiResponseFormat responseFormat)
        {
            if (typeof(TResponse) != expectedType)
            {
                throw new InvalidOperationException(
                        "ApiResponseFormat." + responseFormat + " requires TResponse " + expectedType.Name + ".");
            }
        }

        private static void EnsureTextureType(Type responseType, ApiResponseFormat responseFormat)
        {
            if (!typeof(Texture2D).IsAssignableFrom(responseType))
            {
                throw new InvalidOperationException(
                        "ApiResponseFormat." + responseFormat + " requires TResponse Texture2D.");
            }
        }

        private static void EnsureAssetBundleType(Type responseType, ApiResponseFormat responseFormat)
        {
            if (!typeof(AssetBundle).IsAssignableFrom(responseType))
            {
                throw new InvalidOperationException(
                        "ApiResponseFormat." + responseFormat + " requires TResponse AssetBundle.");
            }
        }

        private static string GetRequestUrlSuffix(ApiTransportResponse response)
        {
            return string.IsNullOrWhiteSpace(response?.RequestUrl)
                           ? string.Empty
                           : " for " + response.RequestUrl;
        }
    }
}
