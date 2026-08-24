using System;
using Deucarian.API.Configuration;
using Newtonsoft.Json;

namespace Deucarian.API.Core
{
    internal sealed class NewtonsoftApiSerializer : IApiSerializer
    {
        private readonly ApiJsonSerializerOptions options;

        public NewtonsoftApiSerializer(ApiJsonSerializerOptions options = null)
        {
            this.options = options ?? new ApiJsonSerializerOptions();
            Settings = this.options.CreateSettings();
        }

        public NewtonsoftApiSerializer(JsonSerializerSettings settings)
        {
            options = null;
            Settings = settings ?? new ApiJsonSerializerOptions().CreateSettings();
        }

        public JsonSerializerSettings Settings { get; }

        public string Serialize(
            object value,
            ApiJsonPropertyNamingPolicy? propertyNamingOverride = null)
        {
            return JsonConvert.SerializeObject(value, ResolveSettings(propertyNamingOverride));
        }

        public T Deserialize<T>(
            string json,
            ApiJsonPropertyNamingPolicy? propertyNamingOverride = null)
        {
            return JsonConvert.DeserializeObject<T>(json, ResolveSettings(propertyNamingOverride));
        }

        public object Deserialize(
            string json,
            Type type,
            ApiJsonPropertyNamingPolicy? propertyNamingOverride = null)
        {
            return JsonConvert.DeserializeObject(json, type, ResolveSettings(propertyNamingOverride));
        }

        private JsonSerializerSettings ResolveSettings(
            ApiJsonPropertyNamingPolicy? propertyNamingOverride)
        {
            return propertyNamingOverride.HasValue && options != null
                    ? options.CreateSettings(propertyNamingOverride)
                    : Settings;
        }
    }
}
