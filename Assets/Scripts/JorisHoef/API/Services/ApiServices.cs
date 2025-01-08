using System.Threading.Tasks;
using JorisHoef.API.Services.Base;
using JorisHoef.API.Services.MultipartForm;

namespace JorisHoef.API.Services
{
    /// <summary>
    /// Encapsulates all ApiServices
    /// </summary>
    public class ApiServices
    {
        /// <summary>
        /// POST request
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="data"></param>
        /// <param name="requiresAuthentication"></param>
        /// <typeparam name="TResponse">The type we expect in the response</typeparam>
        /// <returns></returns>
        public Task<ApiCallResult<TResponse>> PostAsync<TResponse>(string endpoint, object data, bool requiresAuthentication)
        {
            var postService = new PostApiService<TResponse>();
            return postService.ExecuteAsync(endpoint, data, requiresAuthentication);
        }
        
        /// <summary>
        /// POST request for multiform
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="data"></param>
        /// <param name="requiresAuthentication"></param>
        /// <typeparam name="TResponse">The type we expect in the response</typeparam>
        /// <returns></returns>
        public Task<ApiCallResult<TResponse>> PostMultipartAsync<TResponse>(string endpoint, object data, bool requiresAuthentication)
        {
            var postMultipartService = new MultipartPostApiService<TResponse>();
            return postMultipartService.ExecuteAsync(endpoint, data, requiresAuthentication);
        }
        
        /// <summary>
        /// PUT request
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="data"></param>
        /// <param name="requiresAuthentication"></param>
        /// <typeparam name="TResponse">The type we expect in the response</typeparam>
        /// <returns></returns>
        public Task<ApiCallResult<TResponse>> PutAsync<TResponse>(string endpoint, object data, bool requiresAuthentication)
        {
            var putService = new PutApiService<TResponse>();
            return putService.ExecuteAsync(endpoint, data, requiresAuthentication);
        }
        
        /// <summary>
        /// GET request
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="requiresAuthentication"></param>
        /// <typeparam name="TResponse">The type we expect in the response</typeparam>
        /// <returns></returns>
        public Task<ApiCallResult<TResponse>> GetAsync<TResponse>(string endpoint, bool requiresAuthentication)
        {
            var getService = new GetApiService<TResponse>();
            return getService.ExecuteAsync(endpoint, requiresAuthentication: requiresAuthentication);
        }

        /// <summary>
        /// DELETE request
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="requiresAuthentication"></param>
        /// <typeparam name="TResponse">The type we expect in the response</typeparam>
        /// <returns></returns>
        public Task<ApiCallResult<TResponse>> DeleteAsync<TResponse>(string endpoint, bool requiresAuthentication)
        {
            var deleteService = new DeleteApiService<TResponse>();
            return deleteService.ExecuteAsync(endpoint, requiresAuthentication: requiresAuthentication);
        }
    }
}