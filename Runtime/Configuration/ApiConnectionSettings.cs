using System;
using System.Collections.Generic;
using Deucarian.API.Core;
using Deucarian.API.Models;
using UnityEngine;

namespace Deucarian.API.Configuration
{
    /// <summary>
    /// Project-owned connection settings for one package-owned API service.
    /// The asset contains deployment hosts and safe request policy overrides,
    /// but never credentials or active authentication state.
    /// </summary>
    public sealed class ApiConnectionSettings : ScriptableObject
    {
        [Tooltip("Package- or integration-owned service contract. This asset must not contain deployment hosts.")]
        [SerializeField] private ApiServiceDefinition serviceDefinition;

        [Tooltip("Managed environment slots containing project-owned named-client base URLs.")]
        [SerializeField] private List<ApiEnvironmentProfile> environments =
            new List<ApiEnvironmentProfile>();

        /// <summary>The credential-free service contract for this connection.</summary>
        public ApiServiceDefinition ServiceDefinition
        {
            get => serviceDefinition;
            set => serviceDefinition = value;
        }

        /// <summary>Project-owned environment connection slots.</summary>
        public IReadOnlyList<ApiEnvironmentProfile> Environments => environments;

        /// <summary>
        /// Creates a validated API composition. Unconfigured environment slots
        /// remain visible but cannot resolve traffic.
        /// </summary>
        public ApiComposition CreateComposition()
        {
            if (!TryValidateShape(
                    out IReadOnlyList<ApiEnvironmentDescriptor> descriptors,
                    out string message))
            {
                throw new InvalidOperationException(message);
            }

            return new ApiComposition(
                environments,
                serviceDefinition.EndpointCatalog,
                descriptors);
        }

        /// <summary>Attempts to create a composition with an actionable error.</summary>
        public bool TryCreateComposition(
            out ApiComposition composition,
            out string message)
        {
            try
            {
                composition = CreateComposition();
                message = null;
                return true;
            }
            catch (ArgumentException exception)
            {
                composition = null;
                message = exception.Message;
                return false;
            }
            catch (InvalidOperationException exception)
            {
                composition = null;
                message = exception.Message;
                return false;
            }
        }

        /// <summary>Validates service compatibility and every managed slot.</summary>
        public bool TryValidate(out string message)
        {
            return TryValidateShape(out _, out message);
        }

        /// <summary>Creates unsaved settings for integrations, factories, and tests.</summary>
        public static ApiConnectionSettings CreateTransient(
            IEnumerable<ApiEnvironmentProfile> environmentProfiles,
            ApiServiceDefinition definition)
        {
            ApiConnectionSettings settings =
                CreateInstance<ApiConnectionSettings>();
            settings.environments.Clear();
            if (environmentProfiles != null)
            {
                settings.environments.AddRange(environmentProfiles);
            }

            settings.serviceDefinition = definition;
            return settings;
        }

        private bool TryValidateShape(
            out IReadOnlyList<ApiEnvironmentDescriptor> descriptors,
            out string message)
        {
            descriptors = null;
            if (serviceDefinition == null)
            {
                message = "Assign the package-owned API service definition.";
                return false;
            }

            if (!serviceDefinition.IsValid(out message) ||
                !serviceDefinition.TryGetEnvironmentDescriptors(
                    out descriptors,
                    out message) ||
                !serviceDefinition.TryGetRequiredClientIds(
                    out IReadOnlyList<ApiClientId> requiredClients,
                    out message))
            {
                return false;
            }

            if (environments == null)
            {
                message = "API connection settings have no environment slots.";
                return false;
            }

            var expectedEnvironments = new HashSet<ApiEnvironmentId>();
            foreach (ApiEnvironmentDescriptor descriptor in descriptors)
            {
                expectedEnvironments.Add(descriptor.EnvironmentId);
            }

            var suppliedEnvironments = new HashSet<ApiEnvironmentId>();
            foreach (ApiEnvironmentProfile environment in environments)
            {
                if (environment == null ||
                    !environment.TryGetId(out ApiEnvironmentId environmentId))
                {
                    message =
                        "API connection settings contain an invalid environment slot.";
                    return false;
                }

                if (!expectedEnvironments.Contains(environmentId))
                {
                    message = "Environment '" + environmentId +
                        "' is not declared by service '" +
                        serviceDefinition.ServiceId + "'.";
                    return false;
                }

                if (!suppliedEnvironments.Add(environmentId))
                {
                    message = "Duplicate environment connection '" +
                        environmentId + "'.";
                    return false;
                }

                ApiEnvironmentProfileConfigurationState state =
                    environment.ClassifyConfiguration(out message);
                if (state == ApiEnvironmentProfileConfigurationState.Invalid)
                {
                    return false;
                }

                foreach (ApiClientId requiredClient in requiredClients)
                {
                    if (!environment.TryGetClient(requiredClient, out _))
                    {
                        message = "Environment '" + environmentId +
                            "' is missing required client '" +
                            requiredClient + "'.";
                        return false;
                    }
                }
            }

            foreach (ApiEnvironmentId expected in expectedEnvironments)
            {
                if (!suppliedEnvironments.Contains(expected))
                {
                    message = "API connection settings are missing environment '" +
                        expected + "'.";
                    return false;
                }
            }

            message = null;
            return true;
        }
    }
}
