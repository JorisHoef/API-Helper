using System;
using System.Net.Http;
using System.Threading.Tasks;
using JorisHoef.API.Calls;
using UnityEngine;

namespace JorisHoef.API.Services.Base
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

        public virtual async Task<ApiCallResult<TResponse>> ExecuteAsync(string endpoint,
                                                                         bool requiresAuthentication,
                                                                         object data = null,
                                                                         string accessToken = null)
        {
            try
            {
                var apiCall = ApiCall<TResponse>.GetApiCall<TResponse>(endpoint,
                                                                       this.HttpMethod,
                                                                       requiresAuthentication,
                                                                       data,
                                                                       accessToken);

                ApiCallResult<TResponse> result = await apiCall.Execute();
        
                if (!result.IsSuccess)
                {
                    this.HandleApiFailure(result, endpoint);
                }

                return new ApiCallResult<TResponse>
                {
                        IsSuccess = result.IsSuccess,
                        Data = result.IsSuccess ? result.Data : default,
                        ErrorMessage = result.IsSuccess ? null : result.ErrorMessage,
                        Exception = result.Exception,
                        HttpMethod = this.HttpMethod
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

    }
}