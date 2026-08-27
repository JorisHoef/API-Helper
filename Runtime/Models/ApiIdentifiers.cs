using System;
using UnityEngine;

namespace Deucarian.API.Models
{
    internal static class ApiIdentifierUtility
    {
        private const int MaximumLength = 128;

        internal static string Parse(string value, string parameterName)
        {
            string normalized;
            if (!TryNormalize(value, out normalized))
            {
                throw new ArgumentException(
                    "API identifiers must be 1-128 lowercase characters, start and end with a letter or number, and contain only letters, numbers, '.', '_' or '-'.",
                    parameterName);
            }

            return normalized;
        }

        internal static bool TryNormalize(string value, out string normalized)
        {
            normalized = value?.Trim();
            if (string.IsNullOrEmpty(normalized) || normalized.Length > MaximumLength)
            {
                normalized = null;
                return false;
            }

            if (!IsLetterOrNumber(normalized[0]) || !IsLetterOrNumber(normalized[normalized.Length - 1]))
            {
                normalized = null;
                return false;
            }

            for (int index = 0; index < normalized.Length; index++)
            {
                char character = normalized[index];
                if (!IsLetterOrNumber(character)
                    && character != '.'
                    && character != '_'
                    && character != '-')
                {
                    normalized = null;
                    return false;
                }
            }

            return true;
        }

        private static bool IsLetterOrNumber(char character)
        {
            return (character >= 'a' && character <= 'z')
                   || (character >= '0' && character <= '9');
        }
    }

    /// <summary>A stable, serializable identifier for an API environment.</summary>
    [Serializable]
    public struct ApiEnvironmentId : IEquatable<ApiEnvironmentId>
    {
        [SerializeField] private string value;

        /// <summary>Creates a validated environment identifier.</summary>
        public ApiEnvironmentId(string value)
        {
            this.value = ApiIdentifierUtility.Parse(value, nameof(value));
        }

        /// <summary>The normalized identifier value.</summary>
        public string Value => value ?? string.Empty;

        /// <summary>True when this is the default, unassigned identifier.</summary>
        public bool IsEmpty => string.IsNullOrEmpty(value);

        /// <summary>Attempts to parse a stable environment identifier.</summary>
        public static bool TryParse(string candidate, out ApiEnvironmentId identifier)
        {
            string normalized;
            if (ApiIdentifierUtility.TryNormalize(candidate, out normalized))
            {
                identifier = new ApiEnvironmentId { value = normalized };
                return true;
            }

            identifier = default(ApiEnvironmentId);
            return false;
        }

        public bool Equals(ApiEnvironmentId other) => string.Equals(value, other.value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ApiEnvironmentId other && Equals(other);
        public override int GetHashCode() => value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        public override string ToString() => Value;
        public static bool operator ==(ApiEnvironmentId left, ApiEnvironmentId right) => left.Equals(right);
        public static bool operator !=(ApiEnvironmentId left, ApiEnvironmentId right) => !left.Equals(right);
    }

    /// <summary>A stable, serializable identifier for a named API client.</summary>
    [Serializable]
    public struct ApiClientId : IEquatable<ApiClientId>
    {
        [SerializeField] private string value;

        /// <summary>Creates a validated client identifier.</summary>
        public ApiClientId(string value)
        {
            this.value = ApiIdentifierUtility.Parse(value, nameof(value));
        }

        /// <summary>The normalized identifier value.</summary>
        public string Value => value ?? string.Empty;
        /// <summary>True when this is the default, unassigned identifier.</summary>
        public bool IsEmpty => string.IsNullOrEmpty(value);

        /// <summary>Attempts to parse a stable client identifier.</summary>
        public static bool TryParse(string candidate, out ApiClientId identifier)
        {
            string normalized;
            if (ApiIdentifierUtility.TryNormalize(candidate, out normalized))
            {
                identifier = new ApiClientId { value = normalized };
                return true;
            }

            identifier = default(ApiClientId);
            return false;
        }

        public bool Equals(ApiClientId other) => string.Equals(value, other.value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ApiClientId other && Equals(other);
        public override int GetHashCode() => value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        public override string ToString() => Value;
        public static bool operator ==(ApiClientId left, ApiClientId right) => left.Equals(right);
        public static bool operator !=(ApiClientId left, ApiClientId right) => !left.Equals(right);
    }

    /// <summary>A stable, serializable identifier for an API service.</summary>
    [Serializable]
    public struct ApiServiceId : IEquatable<ApiServiceId>
    {
        [SerializeField] private string value;

        /// <summary>Creates a validated service identifier.</summary>
        public ApiServiceId(string value)
        {
            this.value = ApiIdentifierUtility.Parse(value, nameof(value));
        }

        /// <summary>The normalized identifier value.</summary>
        public string Value => value ?? string.Empty;

        /// <summary>True when this is the default, unassigned identifier.</summary>
        public bool IsEmpty => string.IsNullOrEmpty(value);

        /// <summary>Attempts to parse a stable service identifier.</summary>
        public static bool TryParse(string candidate, out ApiServiceId identifier)
        {
            if (ApiIdentifierUtility.TryNormalize(candidate, out string normalized))
            {
                identifier = new ApiServiceId { value = normalized };
                return true;
            }

            identifier = default(ApiServiceId);
            return false;
        }

        public bool Equals(ApiServiceId other) =>
            string.Equals(value, other.value, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is ApiServiceId other && Equals(other);

        public override int GetHashCode() =>
            value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);

        public override string ToString() => Value;

        public static bool operator ==(ApiServiceId left, ApiServiceId right) =>
            left.Equals(right);

        public static bool operator !=(ApiServiceId left, ApiServiceId right) =>
            !left.Equals(right);
    }

    /// <summary>A stable, serializable identifier for an endpoint catalog.</summary>
    [Serializable]
    public struct ApiCatalogId : IEquatable<ApiCatalogId>
    {
        [SerializeField] private string value;

        /// <summary>Creates a validated catalog identifier.</summary>
        public ApiCatalogId(string value)
        {
            this.value = ApiIdentifierUtility.Parse(value, nameof(value));
        }

        /// <summary>The normalized identifier value.</summary>
        public string Value => value ?? string.Empty;
        /// <summary>True when this is the default, unassigned identifier.</summary>
        public bool IsEmpty => string.IsNullOrEmpty(value);

        /// <summary>Attempts to parse a stable catalog identifier.</summary>
        public static bool TryParse(string candidate, out ApiCatalogId identifier)
        {
            string normalized;
            if (ApiIdentifierUtility.TryNormalize(candidate, out normalized))
            {
                identifier = new ApiCatalogId { value = normalized };
                return true;
            }

            identifier = default(ApiCatalogId);
            return false;
        }

        public bool Equals(ApiCatalogId other) => string.Equals(value, other.value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ApiCatalogId other && Equals(other);
        public override int GetHashCode() => value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        public override string ToString() => Value;
        public static bool operator ==(ApiCatalogId left, ApiCatalogId right) => left.Equals(right);
        public static bool operator !=(ApiCatalogId left, ApiCatalogId right) => !left.Equals(right);
    }

    /// <summary>A stable, serializable identifier for a catalog endpoint.</summary>
    [Serializable]
    public struct ApiEndpointId : IEquatable<ApiEndpointId>
    {
        [SerializeField] private string value;

        /// <summary>Creates a validated endpoint identifier.</summary>
        public ApiEndpointId(string value)
        {
            this.value = ApiIdentifierUtility.Parse(value, nameof(value));
        }

        /// <summary>The normalized identifier value.</summary>
        public string Value => value ?? string.Empty;
        /// <summary>True when this is the default, unassigned identifier.</summary>
        public bool IsEmpty => string.IsNullOrEmpty(value);

        /// <summary>Attempts to parse a stable endpoint identifier.</summary>
        public static bool TryParse(string candidate, out ApiEndpointId identifier)
        {
            string normalized;
            if (ApiIdentifierUtility.TryNormalize(candidate, out normalized))
            {
                identifier = new ApiEndpointId { value = normalized };
                return true;
            }

            identifier = default(ApiEndpointId);
            return false;
        }

        public bool Equals(ApiEndpointId other) => string.Equals(value, other.value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ApiEndpointId other && Equals(other);
        public override int GetHashCode() => value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        public override string ToString() => Value;
        public static bool operator ==(ApiEndpointId left, ApiEndpointId right) => left.Equals(right);
        public static bool operator !=(ApiEndpointId left, ApiEndpointId right) => !left.Equals(right);
    }
}
