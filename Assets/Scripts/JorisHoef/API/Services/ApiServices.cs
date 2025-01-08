using System.Threading.Tasks;
using JorisHoef.API.Services.Base;
using JorisHoef.API.Services.MultipartForm;

namespace JorisHoef.API.Services
{
    /// <summary>
    /// Encapsulates all ApiServices
    /// </summary>
    public static class ApiServices
    {
        /// <summary>
        /// POST request for multiform
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="data"></param>
        /// <param name="requiresAuthentication"></param>
        /// <typeparam name="TResponse">The type we expect in the response</typeparam>
        /// <returns></returns>
        public static Task<ApiCallResult<TResponse>> PostMultipartAsync<TResponse>(string endpoint,
                                                                                    object data,
                                                                                    bool requiresAuthentication,
                                                                                    string accessToken = null)
        {
            var postMultipartService = new MultipartApiService<TResponse>(HttpMethod.POST);
            return postMultipartService.ExecuteAsync(endpoint, requiresAuthentication, data, accessToken);
        }

        /// <summary>
        /// POST request
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="data"></param>
        /// <param name="requiresAuthentication"></param>
        /// <typeparam name="TResponse">The type we expect in the response</typeparam>
        /// <returns></returns>
        public static Task<ApiCallResult<TResponse>> PostAsync<TResponse>(string endpoint,
                                                                          object data,
                                                                          bool requiresAuthentication,
                                                                          string accessToken = null)
        {
            var postService = new ApiService<TResponse>(HttpMethod.POST);
            return postService.ExecuteAsync(endpoint, requiresAuthentication, data, accessToken);
        }
        
        /// <summary>
        /// PUT request
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="data"></param>
        /// <param name="requiresAuthentication"></param>
        /// <typeparam name="TResponse">The type we expect in the response</typeparam>
        /// <returns></returns>
        public static Task<ApiCallResult<TResponse>> PutAsync<TResponse>(string endpoint,
                                                                         object data,
                                                                         bool requiresAuthentication,
                                                                         string accessToken = null)
        {
            var putService = new ApiService<TResponse>(HttpMethod.PUT);
            return putService.ExecuteAsync(endpoint, requiresAuthentication, data, accessToken);
        }
        
        /// <summary>
        /// GET request
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="requiresAuthentication"></param>
        /// <typeparam name="TResponse">The type we expect in the response</typeparam>
        /// <returns></returns>
        public static Task<ApiCallResult<TResponse>> GetAsync<TResponse>(string endpoint,
                                                                         bool requiresAuthentication,
                                                                         string accessToken = null)
        {
            var getService = new ApiService<TResponse>(HttpMethod.GET);
            return getService.ExecuteAsync(endpoint, requiresAuthentication, accessToken);
        }

        /// <summary>
        /// DELETE request
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="requiresAuthentication"></param>
        /// <typeparam name="TResponse">The type we expect in the response</typeparam>
        /// <returns></returns>
        public static Task<ApiCallResult<TResponse>> DeleteAsync<TResponse>(string endpoint,
                                                                            bool requiresAuthentication,
                                                                            string accessToken = null)
        {
            var deleteService = new ApiService<TResponse>(HttpMethod.DELETE);
            return deleteService.ExecuteAsync(endpoint, requiresAuthentication, accessToken);
        }
    }
}