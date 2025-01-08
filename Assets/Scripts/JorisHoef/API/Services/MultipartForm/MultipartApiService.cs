using System;
using System.Threading.Tasks;
using JorisHoef.API.Calls;
using JorisHoef.API.Services.Base;
using UnityEngine;

namespace JorisHoef.API.Services.MultipartForm
{
    /// <summary>
    /// For any multiform data
    /// </summary>
    /// <typeparam name="TResponse"></typeparam>
    public abstract class MultipartApiService<TResponse> : ApiService<TResponse>
    {
        public override async Task<ApiCallResult<TResponse>> ExecuteAsync(string endpoint, object data = null, bool requiresAuthentication = false)
        {
            try
            {
                var apiCall = new MultipartFormApiCall<TResponse>(endpoint, this.HttpMethod, data, requiresAuthentication);
        
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