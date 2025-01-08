using System.Collections.Generic;
using JorisHoef.APIHelper.Services.MultipartForm;
using UnityEngine.Networking;

namespace JorisHoef.APIHelper.Calls
{
    public class MultipartFormApiCall<TResult> : ApiCall<TResult>
    {
        private MultipartFormApiCall(string url, HttpMethod method, bool requiresAuthentication, object data) : base(url, method, requiresAuthentication, data) { }
        private MultipartFormApiCall(string url, HttpMethod method, bool requiresAuthentication, object data, string accessToken) : base(url, method, requiresAuthentication, data, accessToken) { }

        public new static ApiCall<TResponse> GetApiCall<TResponse>(string url,
                                                                   HttpMethod method,
                                                                   bool requiresAuthentication,
                                                                   object data,
                                                                   string tokenToSend = null)
        {
            if (requiresAuthentication)
            {
                return new MultipartFormApiCall<TResponse>(url, method, requiresAuthentication, data, tokenToSend);
            }
            else
            {
                return new MultipartFormApiCall<TResponse>(url, method, requiresAuthentication, data);
            }
        }

        protected override UnityWebRequest PrepareRequest(string accessToken)
        {
            if (base._method == HttpMethod.POST && base._data is IMultiFormPropertyAdder multiFormPropertyAdder)
            {
                List<IMultipartFormSection> form = new List<IMultipartFormSection>();
                multiFormPropertyAdder.AddPropertiesToForm(form);
        
                UnityWebRequest webRequest = UnityWebRequest.Post(this._url, form);

                if (!string.IsNullOrEmpty(accessToken))
                {
                    webRequest.SetRequestHeader("Authorization", $"Bearer {accessToken}");
                }
        
                webRequest.SetRequestHeader("Accept", "application/json");

                return webRequest;
            }
            else
            {
                return base.PrepareRequest(accessToken);
            }
        }
    }
}