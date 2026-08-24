using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Deucarian.API.Configuration
{
    /// <summary>
    /// Serializable Newtonsoft JSON options used by <see cref="ApiClientConfig"/>.
    /// </summary>
    [Serializable]
    public sealed class ApiJsonSerializerOptions
    {
        [Tooltip("Property-name casing used for JSON request and response DTOs. Explicit JsonProperty names always win.")]
        [SerializeField] private ApiJsonPropertyNamingPolicy propertyNaming =
                ApiJsonPropertyNamingPolicy.SnakeCase;
        [SerializeField] private NullValueHandling nullValueHandling = NullValueHandling.Ignore;
        [SerializeField] private DefaultValueHandling defaultValueHandling = DefaultValueHandling.Include;
        [SerializeField] private MissingMemberHandling missingMemberHandling = MissingMemberHandling.Ignore;
        [SerializeField] private ReferenceLoopHandling referenceLoopHandling = ReferenceLoopHandling.Ignore;
        [SerializeField] private Formatting formatting = Formatting.None;

        /// <summary>Controls the default casing of JSON DTO property names.</summary>
        public ApiJsonPropertyNamingPolicy PropertyNaming
        {
            get => propertyNaming;
            set => propertyNaming = value;
        }

        /// <summary>Controls whether null values are included in serialized JSON.</summary>
        public NullValueHandling NullValueHandling
        {
            get => nullValueHandling;
            set => nullValueHandling = value;
        }

        /// <summary>Controls whether default values are included in serialized JSON.</summary>
        public DefaultValueHandling DefaultValueHandling
        {
            get => defaultValueHandling;
            set => defaultValueHandling = value;
        }

        /// <summary>Controls how unknown JSON members are handled during deserialization.</summary>
        public MissingMemberHandling MissingMemberHandling
        {
            get => missingMemberHandling;
            set => missingMemberHandling = value;
        }

        /// <summary>Controls how reference loops are handled during serialization.</summary>
        public ReferenceLoopHandling ReferenceLoopHandling
        {
            get => referenceLoopHandling;
            set => referenceLoopHandling = value;
        }

        /// <summary>Controls whether JSON output is compact or indented.</summary>
        public Formatting Formatting
        {
            get => formatting;
            set => formatting = value;
        }

        /// <summary>
        /// Creates runtime Newtonsoft settings from the serialized options.
        /// </summary>
        /// <returns>A new <see cref="JsonSerializerSettings"/> instance.</returns>
        public JsonSerializerSettings CreateSettings()
        {
            return CreateSettings(null);
        }

        /// <summary>
        /// Creates runtime Newtonsoft settings, optionally overriding only the property naming policy.
        /// </summary>
        /// <param name="propertyNamingOverride">Per-request naming policy, or null for the configured default.</param>
        /// <returns>A new <see cref="JsonSerializerSettings"/> instance.</returns>
        public JsonSerializerSettings CreateSettings(
            ApiJsonPropertyNamingPolicy? propertyNamingOverride)
        {
            ApiJsonPropertyNamingPolicy effectiveNaming =
                    propertyNamingOverride ?? propertyNaming;
            return new JsonSerializerSettings
            {
                    ContractResolver = new DefaultContractResolver
                    {
                            NamingStrategy =
                                    ApiJsonPropertyNamingStrategyFactory.Create(effectiveNaming)
                    },
                    NullValueHandling = nullValueHandling,
                    DefaultValueHandling = defaultValueHandling,
                    MissingMemberHandling = missingMemberHandling,
                    ReferenceLoopHandling = referenceLoopHandling,
                    Formatting = formatting
            };
        }
    }
}
