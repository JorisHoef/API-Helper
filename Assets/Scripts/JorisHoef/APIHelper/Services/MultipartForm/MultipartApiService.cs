using System;
using System.Threading.Tasks;
using JorisHoef.APIHelper.Calls;
using JorisHoef.APIHelper.Models;
using JorisHoef.APIHelper.Services.Base;
using UnityEngine;

namespace JorisHoef.APIHelper.Services.MultipartForm
{
    /// <summary>
    /// For any multiform data
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    public class MultipartApiService<TResponse> : ApiService<TResponse>
    {
        public MultipartApiService(HttpMethod httpMethod) : base(httpMethod) { }

        public override async Task<ApiCallResult<TResponse>> ExecuteAsync(string endpoint,
                                                                          bool requiresAuthentication,
                                                                          object data = null,
                                                                          string accessToken = null)
        {
            try
            {
                var apiCall = MultipartFormApiCall<TResponse>.GetApiCall<TResponse>(endpoint,
                                                                                    this.HttpMethod,
                                                                                    requiresAuthentication,
                                                                                    data);
        
                ApiCallResult<TResponse> result = await apiCall.Execute();
                if (!result.IsSuccess)
                {
                    base.HandleApiFailure(result, endpoint);
                }
                return new ApiCallResult<TResponse>
                {
                        IsSuccess = result.IsSuccess,
                        Data = result.IsSuccess ? result.Data : default,
                        ErrorMessage = result.ErrorMessage,
                        Exception = result.Exception,
                        HttpMethod = this.HttpMethod
                };
            }
            catch(Exception ex)
            {
                Debug.LogError(ex);
        
                return new ApiCallResult<TResponse>
                {
                        IsSuccess = false,
                        ErrorMessage = ex.Message,
                        Exception = ex,
                        HttpMethod = this.HttpMethod
                };
            }
        }
    }
}