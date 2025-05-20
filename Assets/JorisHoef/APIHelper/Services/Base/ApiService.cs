using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using JorisHoef.APIHelper.Calls;
using JorisHoef.APIHelper.Models;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace JorisHoef.APIHelper.Services.Base
{
    /// <summary>
    /// Don't have to create a new apiservice for each call, can just call ApiServices class which will instantiate these services when required
    /// </summary>
    /// <typeparam name="TResponse">Represents type we expect in response from Backend</typeparam>
    /// <remarks>
    /// TReponse type does not have to match the Body type, the Body we send has nothing to do with this type!
    /// </remarks>
    public class ApiService<TResponse>
    {
        protected HttpMethod HttpMethod { get; }

        public ApiService(HttpMethod httpMethod)
        {
            HttpMethod = httpMethod;
        }

        public virtual async Task<ApiCallResult<TResponse>> ExecuteAsync(
                string endpoint,
                bool requiresAuthentication,
                object data = null,
                string accessToken = null,
                Dictionary<string, string> customHeaders = null)
        {
            try
            {
                var apiCall = ApiCall<TResponse>.GetApiCall<TResponse>(
                                                                       endpoint,
                                                                       this.HttpMethod,
                                                                       requiresAuthentication,
                                                                       data,
                                                                       accessToken);

                ApiCallResult<TResponse> result = await apiCall.Execute(customHeaders);

#if UNITY_EDITOR
                if (APIDebugSettings.LogRawJson && result.IsSuccess && !string.IsNullOrEmpty(result.RawJson))
                {
                    Debug.Log(result.RawJson);
                }
#endif

                if (!result.IsSuccess)
                {
                    HandleApiFailure(result, endpoint);
                }

                string extractedMessage = result.IsSuccess
                                                  ? null
                                                  : TryExtractJsonMessage(result.ErrorMessage) ?? result.ErrorMessage;

                return new ApiCallResult<TResponse>
                {
                        IsSuccess = result.IsSuccess,
                        Data = result.IsSuccess ? result.Data : default,
                        ErrorMessage = extractedMessage,
                        Exception = result.Exception,
                        HttpMethod = this.HttpMethod,
                        RawJson = result.RawJson
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception occurred while making API call to {endpoint}: {ex.Message}\nStack Trace: {ex.StackTrace}");
                return new ApiCallResult<TResponse>
                {
                        IsSuccess = false,
                        ErrorMessage = ex.Message,
                        Exception = ex,
                        HttpMethod = this.HttpMethod
                };
            }
        }

        protected void HandleApiFailure(ApiCallResult<TResponse> result, string endpoint)
        {
            if (result == null)
            {
                Debug.LogError($"Error making API call to {endpoint}: Result is null, likely due to a failed API call or a null response.");
            }
            else
            {
                var errorMessage = string.IsNullOrEmpty(result.ErrorMessage) ? "No error message provided" : result.ErrorMessage;
                var exceptionDetails = result.Exception?.ToString() ?? "No exception details available";

                Debug.LogError($"Error making API call to {endpoint} - HTTP Method: {result.HttpMethod} - Message: {errorMessage} - Exception: {exceptionDetails}");

                if (result.Exception is HttpRequestException httpRequestException)
                {
                    Debug.LogError($"HTTP Request Error: {httpRequestException.Message}");
                }
            }
        }

        /// <summary>
        /// Tries to extract the message from the JSON object if the errorMessage contains one.
        /// It looks for "Response:" in the string and attempts to parse the JSON that follows.
        /// </summary>
        /// <param name="errorMessage">The full error message string.</param>
        /// <returns>The extracted message if available; otherwise, null.</returns>
        private string TryExtractJsonMessage(string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
                return null;

            try
            {
                int responseIndex = errorMessage.IndexOf("Response:", StringComparison.OrdinalIgnoreCase);
                if (responseIndex >= 0)
                {
                    int jsonStart = errorMessage.IndexOf('{', responseIndex);
                    if (jsonStart >= 0)
                    {
                        string jsonSubstring = errorMessage.Substring(jsonStart);
                        var json = JObject.Parse(jsonSubstring);
                        if (json.TryGetValue("message", StringComparison.OrdinalIgnoreCase, out JToken messageToken))
                        {
                            return messageToken.ToString();
                        }
                    }
                }
                else
                {
                    int firstBrace = errorMessage.IndexOf('{');
                    if (firstBrace >= 0)
                    {
                        string jsonSubstring = errorMessage.Substring(firstBrace);
                        var json = JObject.Parse(jsonSubstring);
                        if (json.TryGetValue("message", StringComparison.OrdinalIgnoreCase, out JToken messageToken))
                        {
                            return messageToken.ToString();
                        }
                    }
                }
            }
            catch (Exception parseEx)
            {
                Debug.LogWarning($"Failed to parse error message JSON: {parseEx.Message}");
            }
            return null;
        }
    }
}