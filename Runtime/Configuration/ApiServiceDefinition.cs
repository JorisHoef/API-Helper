using System;
using System.Collections.Generic;
using Deucarian.API.Models;
using UnityEngine;

namespace Deucarian.API.Configuration
{
    /// <summary>
    /// Serializable safe metadata for one service-owned environment. It never
    /// contains a deployment host, credential, or active environment.
    /// </summary>
    [Serializable]
    public sealed class ApiEnvironmentDescriptorDefinition
    {
        [SerializeField] private string environmentId;
        [SerializeField] private ApiEnvironmentStage stage;
        [SerializeField] private string displayName;

        public string EnvironmentId
        {
            get => environmentId;
            set => environmentId = value;
        }

        public ApiEnvironmentStage Stage
        {
            get => stage;
            set => stage = value;
        }

        public string DisplayName
        {
            get => displayName;
            set => displayName = value;
        }

        public bool TryCreateDescriptor(
            out ApiEnvironmentDescriptor descriptor,
            out string message)
        {
            descriptor = null;
            if (!ApiEnvironmentId.TryParse(environmentId, out ApiEnvironmentId id))
            {
                message = "Known environment has an invalid stable ID: '" +
                    (environmentId ?? string.Empty) + "'.";
                return false;
            }

            try
            {
                descriptor = new ApiEnvironmentDescriptor(id, stage, displayName);
                message = null;
                return true;
            }
            catch (ArgumentOutOfRangeException exception)
            {
                message = exception.Message;
                return false;
            }
        }

        public static ApiEnvironmentDescriptorDefinition FromDescriptor(
            ApiEnvironmentDescriptor descriptor)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            return new ApiEnvironmentDescriptorDefinition
            {
                EnvironmentId = descriptor.EnvironmentId.Value,
                Stage = descriptor.Stage,
                DisplayName = descriptor.DisplayName
            };
        }
    }

    /// <summary>A named client required by every configured environment.</summary>
    [Serializable]
    public sealed class ApiRequiredClientDefinition
    {
        [SerializeField] private string clientId;
        [SerializeField] private string displayName;

        public string ClientId
        {
            get => clientId;
            set => clientId = value;
        }

        public string DisplayName
        {
            get => displayName;
            set => displayName = value;
        }

        internal bool TryGetId(out ApiClientId id, out string message)
        {
            if (!ApiClientId.TryParse(clientId, out id))
            {
                message = "Required client has an invalid stable ID: '" +
                    (clientId ?? string.Empty) + "'.";
                return false;
            }

            message = null;
            return true;
        }
    }

    /// <summary>
    /// Package- or integration-owned, credential-free API contract. Concrete
    /// deployment hosts and active environment selection never belong here.
    /// </summary>
    public sealed class ApiServiceDefinition : ScriptableObject
    {
        [SerializeField] private string serviceId;
        [SerializeField] private string displayName;
        [SerializeField] private ApiEndpointCatalog endpointCatalog;
        [SerializeField] private List<ApiEnvironmentDescriptorDefinition>
            knownEnvironments = new List<ApiEnvironmentDescriptorDefinition>();
        [SerializeField] private List<ApiRequiredClientDefinition>
            requiredClients = new List<ApiRequiredClientDefinition>();
        [SerializeField] private string sourceVersion;
        [SerializeField] private string sourceFingerprint;

        public string ServiceId
        {
            get => serviceId;
            set => serviceId = value;
        }

        public string DisplayName
        {
            get => displayName;
            set => displayName = value;
        }

        public ApiEndpointCatalog EndpointCatalog
        {
            get => endpointCatalog;
            set => endpointCatalog = value;
        }

        public List<ApiEnvironmentDescriptorDefinition> KnownEnvironments =>
            knownEnvironments;

        public List<ApiRequiredClientDefinition> RequiredClients =>
            requiredClients;

        public string SourceVersion
        {
            get => sourceVersion;
            set => sourceVersion = value;
        }

        public string SourceFingerprint
        {
            get => sourceFingerprint;
            set => sourceFingerprint = value;
        }

        public bool TryGetId(out ApiServiceId id)
        {
            return ApiServiceId.TryParse(serviceId, out id);
        }

        public bool TryGetEnvironmentDescriptors(
            out IReadOnlyList<ApiEnvironmentDescriptor> descriptors,
            out string message)
        {
            var resolved = new List<ApiEnvironmentDescriptor>();
            var ids = new HashSet<ApiEnvironmentId>();
            if (knownEnvironments == null || knownEnvironments.Count == 0)
            {
                descriptors = null;
                message = "API service '" + (serviceId ?? string.Empty) +
                    "' must define at least one known environment.";
                return false;
            }

            foreach (ApiEnvironmentDescriptorDefinition definition in
                knownEnvironments)
            {
                if (definition == null)
                {
                    descriptors = null;
                    message =
                        "Known environment metadata contains a null entry.";
                    return false;
                }

                if (!definition.TryCreateDescriptor(
                        out ApiEnvironmentDescriptor descriptor,
                        out message))
                {
                    descriptors = null;
                    return false;
                }

                if (!ids.Add(descriptor.EnvironmentId))
                {
                    descriptors = null;
                    message = "Duplicate known environment ID '" +
                        descriptor.EnvironmentId + "'.";
                    return false;
                }

                resolved.Add(descriptor);
            }

            descriptors = resolved;
            message = null;
            return true;
        }

        public bool TryGetRequiredClientIds(
            out IReadOnlyList<ApiClientId> clientIds,
            out string message)
        {
            var resolved = new List<ApiClientId>();
            var ids = new HashSet<ApiClientId>();
            if (requiredClients == null || requiredClients.Count == 0)
            {
                clientIds = null;
                message = "API service '" + (serviceId ?? string.Empty) +
                    "' must define at least one required client.";
                return false;
            }

            foreach (ApiRequiredClientDefinition definition in requiredClients)
            {
                if (definition == null)
                {
                    clientIds = null;
                    message = "Required client metadata contains a null entry.";
                    return false;
                }

                if (!definition.TryGetId(out ApiClientId id, out message))
                {
                    clientIds = null;
                    return false;
                }

                if (!ids.Add(id))
                {
                    clientIds = null;
                    message = "Duplicate required client ID '" + id + "'.";
                    return false;
                }

                resolved.Add(id);
            }

            clientIds = resolved;
            message = null;
            return true;
        }

        public bool IsValid(out string message)
        {
            if (!ApiServiceId.TryParse(serviceId, out ApiServiceId parsedId))
            {
                message = "API service definition has an invalid stable ID: '" +
                    (serviceId ?? string.Empty) + "'.";
                return false;
            }

            if (endpointCatalog == null)
            {
                message = "API service '" + parsedId +
                    "' requires an endpoint catalog.";
                return false;
            }

            if (!endpointCatalog.IsValid(out message))
            {
                message = "API service '" + parsedId +
                    "' requires a valid endpoint catalog. " + message;
                return false;
            }

            if (!TryGetEnvironmentDescriptors(out _, out message) ||
                !TryGetRequiredClientIds(
                    out IReadOnlyList<ApiClientId> clientIds,
                    out message))
            {
                return false;
            }

            var required = new HashSet<ApiClientId>(clientIds);
            foreach (ApiEndpointCatalogEntry endpoint in endpointCatalog.Endpoints)
            {
                if (!ApiClientId.TryParse(endpoint.ClientId, out ApiClientId id) ||
                    !required.Contains(id))
                {
                    message = "Endpoint '" + endpoint.EndpointId +
                        "' uses client '" + endpoint.ClientId +
                        "' which is not declared by service '" + parsedId + "'.";
                    return false;
                }
            }

            message = null;
            return true;
        }

        public static ApiServiceDefinition CreateTransient(
            string id,
            string name,
            ApiEndpointCatalog catalog,
            IEnumerable<ApiEnvironmentDescriptor> environments,
            IEnumerable<ApiClientId> clients,
            string version = null,
            string fingerprint = null)
        {
            ApiServiceDefinition definition =
                CreateInstance<ApiServiceDefinition>();
            definition.serviceId = id;
            definition.displayName = name;
            definition.endpointCatalog = catalog;
            definition.sourceVersion = version;
            definition.sourceFingerprint = fingerprint;
            definition.knownEnvironments.Clear();
            definition.requiredClients.Clear();

            if (environments != null)
            {
                foreach (ApiEnvironmentDescriptor environment in environments)
                {
                    definition.knownEnvironments.Add(
                        ApiEnvironmentDescriptorDefinition.FromDescriptor(
                            environment));
                }
            }

            if (clients != null)
            {
                foreach (ApiClientId client in clients)
                {
                    definition.requiredClients.Add(
                        new ApiRequiredClientDefinition
                        {
                            ClientId = client.Value,
                            DisplayName = client.Value
                        });
                }
            }

            return definition;
        }

        private void OnValidate()
        {
            serviceId = serviceId?.Trim();
            displayName = displayName?.Trim();
            sourceVersion = sourceVersion?.Trim();
            sourceFingerprint = sourceFingerprint?.Trim();
        }
    }
}
