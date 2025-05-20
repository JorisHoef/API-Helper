using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using JorisHoef.APIHelper.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace JorisHoef.APIHelper.Calls
{
    public class ApiCall<TResponse>
    {
        protected readonly string _url;
        protected readonly HttpMethod _method;
        protected readonly object _data;
        private readonly bool _requiresAuthentication;
        private readonly string _accessToken;

        protected ApiCall(string url, HttpMethod method, bool requiresAuthentication, object data)
        {
            _url = url;
            _method = method;
            _data = data;
            _requiresAuthentication = requiresAuthentication;
            _accessToken = null;
        }

        protected ApiCall(string url, HttpMethod method, bool requiresAuthentication, object data, string accessToken)
        {
            _url = url;
            _method = method;
            _data = data;
            _requiresAuthentication = requiresAuthentication;
            _accessToken = accessToken;
        }

        public static ApiCall<TResponse> GetApiCall<TResponse>(string url,
                                                               HttpMethod method,
                                                               bool requiresAuthentication,
                                                               object data,
                                                               string accessToken = null)
        {
            if (requiresAuthentication)
            {
                return new ApiCall<TResponse>(url, method, requiresAuthentication, data, accessToken);
            }
            else
            {
                return new ApiCall<TResponse>(url, method, requiresAuthentication, data);
            }
        }

        public async Task<ApiCallResult<TResponse>> Execute(Dictionary<string, string> customHeaders = null)
        {
            if (string.IsNullOrEmpty(_accessToken) && _requiresAuthentication)
            {
                return new ApiCallResult<TResponse>
                {
                    IsSuccess = false,
                    ErrorMessage = "Invalid token, please login",
                    HttpMethod = _method
                };
            }

            using (UnityWebRequest webRequest = PrepareRequest(_accessToken, customHeaders))
            {
                try
                {
                    await SendRequestAsync(webRequest);

                    string rawJson = webRequest.downloadHandler?.text;

                    if (webRequest.result == UnityWebRequest.Result.ConnectionError)
                    {
                        return new ApiCallResult<TResponse>
                        {
                            IsSuccess = false,
                            ErrorMessage = $"Connection error: {webRequest.error} ResponseCode: {webRequest.responseCode}",
                            HttpMethod = _method,
                            RawJson = rawJson
                        };
                    }
                    else if (webRequest.result == UnityWebRequest.Result.ProtocolError)
                    {
                        string errorMessage = await ExtractErrorMessage(webRequest);
                        return new ApiCallResult<TResponse>
                        {
                            IsSuccess = false,
                            ErrorMessage = $"HTTP error, status code {webRequest.responseCode}: {errorMessage}. Response: {rawJson}",
                            HttpMethod = _method,
                            RawJson = rawJson
                        };
                    }

                    var result = await ExtractResultOrCatchException(webRequest);
                    result.HttpMethod = _method;
                    result.RawJson = rawJson;
                    return result;
                }
                catch (Exception ex)
                {
                    string errorMessage = await ExtractErrorMessage(webRequest);
                    string responseText = webRequest.downloadHandler?.text ?? "No response text available";
                    return new ApiCallResult<TResponse>
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Exception during web request: {errorMessage ?? ex.Message}. Response: {responseText}",
                        Exception = ex,
                        HttpMethod = _method,
                        RawJson = responseText
                    };
                }
            }
        }

        protected virtual UnityWebRequest PrepareRequest(string accessToken, Dictionary<string, string> customHeaders = null)
        {
            UnityWebRequest webRequest = new UnityWebRequest(_url, _method.ToString())
            {
                downloadHandler = new DownloadHandlerBuffer()
            };

            if (_data != null && (_method == HttpMethod.POST || _method == HttpMethod.PUT))
            {
                string jsonPayload = JsonConvert.SerializeObject(_data);
                webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonPayload));
                webRequest.SetRequestHeader("Content-Type", "application/json");
            }

            webRequest.SetRequestHeader("Accept", "application/json");

            if (!string.IsNullOrEmpty(accessToken))
            {
                webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");
            }

            if (customHeaders != null)
            {
                foreach (var kvp in customHeaders)
                {
                    webRequest.SetRequestHeader(kvp.Key, kvp.Value);
                }
            }

            return webRequest;
        }

        private static Task<ApiCallResult<TResponse>> ExtractResultOrCatchException(UnityWebRequest sentRequest)
        {
            try
            {
                if ((int)sentRequest.responseCode >= 200 && (int)sentRequest.responseCode < 300)
                {
                    if (sentRequest.downloadHandler != null && !string.IsNullOrEmpty(sentRequest.downloadHandler.text))
                    {
                        TResponse resultData = ParseResponse(sentRequest.downloadHandler.text);
                        if (resultData != null || sentRequest.responseCode == 204)
                        {
                            return Task.FromResult(new ApiCallResult<TResponse> { IsSuccess = true, Data = resultData });
                        }
                    }
                    else
                    {
                        return Task.FromResult(new ApiCallResult<TResponse> { IsSuccess = true });
                    }
                }

                throw new Exception($"Unexpected response code: {sentRequest.responseCode}");
            }
            catch (Exception ex)
            {
                return Task.FromResult(new ApiCallResult<TResponse> { IsSuccess = false, Exception = ex });
            }
        }

        private static TResponse ParseResponse(string rawResponse)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(rawResponse))
                {
                    return JsonConvert.DeserializeObject<TResponse>(rawResponse);
                }
                else
                {
                    throw new Exception("No response received from the server");
                }
            }
            catch (JsonSerializationException ex)
            {
                throw new Exception($"Error in JSON deserialization: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                throw new Exception($"Couldn't parse the JSON response: {ex.Message}", ex);
            }
        }

        private static Task<string> ExtractErrorMessage(UnityWebRequest sentRequest)
        {
            try
            {
                if (!string.IsNullOrEmpty(sentRequest.downloadHandler?.text))
                {
                    var errorResponse = JsonConvert.DeserializeObject<JObject>(sentRequest.downloadHandler.text);
                    if (errorResponse != null)
                    {
                        string errors = errorResponse["errors"]?.ToString() ?? "No detailed errors found";
                        string title = errorResponse["title"]?.ToString();
                        if (!string.IsNullOrEmpty(title))
                        {
                            errors = $"{title}: {errors}";
                        }
                        return Task.FromResult(errors);
                    }
                    return Task.FromResult("No detailed error message found");
                }
                return Task.FromResult("No error message received");
            }
            catch (JsonException ex)
            {
                return Task.FromResult($"Error parsing error message: {ex.Message}");
            }
        }

        private static Task SendRequestAsync(UnityWebRequest webRequest)
        {
            var completionSource = new TaskCompletionSource<object>();

            webRequest.SendWebRequest().completed += _ =>
            {
                if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
                {
                    completionSource.TrySetException(new Exception(webRequest.error));
                }
                else
                {
                    completionSource.TrySetResult(null);
                }
            };

            return completionSource.Task;
        }
    }
}
