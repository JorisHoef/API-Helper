using System;
using Deucarian.API.Configuration;
using Newtonsoft.Json;

namespace Deucarian.API.Core
{
    internal interface IApiSerializer
    {
        JsonSerializerSettings Settings { get; }
        string Serialize(object value, ApiJsonPropertyNamingPolicy? propertyNamingOverride = null);
        T Deserialize<T>(string json, ApiJsonPropertyNamingPolicy? propertyNamingOverride = null);
        object Deserialize(string json,
                           Type type,
                           ApiJsonPropertyNamingPolicy? propertyNamingOverride = null);
    }
}
