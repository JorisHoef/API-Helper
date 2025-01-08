using System;

namespace JorisHoef.APIHelper.Models
{
    /// <summary>
    /// Represents the result of an API call.
    /// </summary>
    /// <typeparam name="T">The type of data returned in the API call result.</typeparam>
    public class ApiCallResult<T>
    {
        public bool IsSuccess { get; set; }
        public T Data { get; set; }
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
        public HttpMethod HttpMethod { get; set; }
    }
}