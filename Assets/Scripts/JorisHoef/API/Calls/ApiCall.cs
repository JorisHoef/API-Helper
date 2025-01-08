using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace JorisHoef.API.Calls
{
    public class ApiCall<TResponse>
    {
        protected readonly string _url;
        protected readonly HttpMethod _method;
        protected readonly object _data;

        private readonly bool _requiresAuthentication;

        private string AccessToken => !string.IsNullOrEmpty(ApiSession.Instance.AccessToken) ? ApiSession.Instance.AccessToken : null;

        public ApiCall(string url, HttpMethod method, object data = null, bool requiresAuthentication = true)
        {
            _url = url;
            _method = method;
            _data = data;
            _requiresAuthentication = requiresAuthentication;
        }

        public async Task<ApiCallResult<TResponse>> Execute()
        {
            string tokenToSend = AccessToken;

            if (string.IsNullOrEmpty(tokenToSend) && _requiresAuthentication)
            {
                return new ApiCallResult<TResponse>
                {
                    IsSuccess = false,
                    ErrorMessage = "Invalid token, please login",
                    HttpMethod = _method
                };
            }

            using (UnityWebRequest webRequest = PrepareRequest(tokenToSend))
            {
                try
                {
                    await SendRequestAsync(webRequest);

                    if (webRequest.result == UnityWebRequest.Result.ConnectionError)
                    {
                        return new ApiCallResult<TResponse>
                        {
                            IsSuccess = false,
                            ErrorMessage = $"Connection error: {webRequest.error} ResponseCode: {webRequest.responseCode}",
                            HttpMethod = _method,
                        };
                    }
                    else if (webRequest.result == UnityWebRequest.Result.ProtocolError)
                    {
                        string errorMessage = await ExtractErrorMessage(webRequest);
                        string responseText = webRequest.downloadHandler?.text ?? "No response text available";
                        return new ApiCallResult<TResponse>
                        {
                            IsSuccess = false,
                            ErrorMessage = $"HTTP error, status code {webRequest.responseCode}: {errorMessage}. Response: {responseText}",
                            HttpMethod = _method
                        };
                    }

                    var result = await ExtractResultOrCatchException(webRequest);
                    result.HttpMethod = _method;
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
                        HttpMethod = _method
                    };
                }
            }
        }

        protected virtual UnityWebRequest PrepareRequest(string tokenToSend)
        {
            try
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

                if (!string.IsNullOrEmpty(tokenToSend))
                {
                    webRequest.SetRequestHeader("Authorization", $"Bearer {tokenToSend}");
                }

                return webRequest;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error in PrepareRequest: {ex.Message}", ex);
            }
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
                    // Check if the error response contains detailed error information
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

            webRequest.SendWebRequest().completed += operation =>
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