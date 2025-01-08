﻿using System.Collections.Generic;
using JorisHoef.API.Services.MultipartForm;
using UnityEngine.Networking;

namespace JorisHoef.API.Calls
{
    public class MultipartFormApiCall<TResult> : ApiCall<TResult>
    {
        public MultipartFormApiCall(string url, HttpMethod method, object data = null, bool requiresAuthentication = true) : base(url, method, data, requiresAuthentication) { }

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