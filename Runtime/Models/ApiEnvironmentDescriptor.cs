using System;
using System.Collections.Generic;

namespace Deucarian.API.Models
{
    /// <summary>Vendor-neutral lifecycle stage for an API deployment.</summary>
    public enum ApiEnvironmentStage
    {
        /// <summary>No built-in deployment stage has been assigned.</summary>
        Custom = 0,

        /// <summary>Developer-facing integration environment.</summary>
        Development = 1,

        /// <summary>Automated or manual test environment.</summary>
        Testing = 2,

        /// <summary>User-acceptance environment.</summary>
        Acceptance = 3,

        /// <summary>Live production environment.</summary>
        Production = 4,

        /// <summary>Package-defined local developer environment.</summary>
        Local = 5
    }

    /// <summary>Shared ordering and validation for built-in deployment stages.</summary>
    public static class ApiEnvironmentStages
    {
        private static readonly IReadOnlyList<ApiEnvironmentStage> standard =
            Array.AsReadOnly(new[]
            {
                ApiEnvironmentStage.Development,
                ApiEnvironmentStage.Testing,
                ApiEnvironmentStage.Acceptance,
                ApiEnvironmentStage.Production
            });

        private static readonly IReadOnlyList<ApiEnvironmentStage> all =
            Array.AsReadOnly(new[]
            {
                ApiEnvironmentStage.Local,
                ApiEnvironmentStage.Development,
                ApiEnvironmentStage.Testing,
                ApiEnvironmentStage.Acceptance,
                ApiEnvironmentStage.Production
            });

        /// <summary>The four conventional remote deployment stages in order.</summary>
        public static IReadOnlyList<ApiEnvironmentStage> Standard => standard;

        /// <summary>
        /// All first-class stages in user-facing order: Local followed by the
        /// four conventional remote deployment stages. Custom is intentionally
        /// excluded because it represents an unknown or integration-defined stage.
        /// </summary>
        public static IReadOnlyList<ApiEnvironmentStage> All => all;

        /// <summary>Returns whether a serialized value is a supported stage.</summary>
        public static bool IsSupported(ApiEnvironmentStage stage)
        {
            switch (stage)
            {
                case ApiEnvironmentStage.Custom:
                case ApiEnvironmentStage.Development:
                case ApiEnvironmentStage.Testing:
                case ApiEnvironmentStage.Acceptance:
                case ApiEnvironmentStage.Production:
                case ApiEnvironmentStage.Local:
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Safe metadata for a known environment, including environments that do
    /// not yet have a configured host or client profile.
    /// </summary>
    public sealed class ApiEnvironmentDescriptor
    {
        public ApiEnvironmentDescriptor(
            ApiEnvironmentId environmentId,
            ApiEnvironmentStage stage,
            string displayName)
        {
            if (environmentId.IsEmpty)
            {
                throw new ArgumentException(
                    "A known API environment requires a stable ID.",
                    nameof(environmentId));
            }

            if (!ApiEnvironmentStages.IsSupported(stage))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stage),
                    "The API environment stage is not supported.");
            }

            EnvironmentId = environmentId;
            Stage = stage;
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? environmentId.Value
                : displayName.Trim();
        }

        /// <summary>Stable vendor- or product-owned environment ID.</summary>
        public ApiEnvironmentId EnvironmentId { get; }

        /// <summary>Vendor-neutral deployment stage.</summary>
        public ApiEnvironmentStage Stage { get; }

        /// <summary>Safe human-friendly label that must not contain connection details.</summary>
        public string DisplayName { get; }
    }
}
