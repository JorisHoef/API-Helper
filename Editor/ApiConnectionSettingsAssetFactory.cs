using System;
using System.Collections.Generic;
using System.IO;
using Deucarian.API.Configuration;
using Deucarian.API.Models;
using UnityEditor;
using UnityEngine;

namespace Deucarian.API.Editor
{
    /// <summary>
    /// Creates complete project-owned settings for a supplied service. Vendor
    /// integrations call this from an explicit setup action or asset menu.
    /// </summary>
    public static class ApiConnectionSettingsAssetFactory
    {
        public static bool TryCreateProjectSettings(
            string assetPath,
            ApiServiceDefinition serviceDefinition,
            out ApiConnectionSettings settings,
            out string error)
        {
            settings = null;
            if (!TryNormalizeProjectAssetPath(
                    assetPath,
                    out string normalizedPath,
                    out error))
            {
                return false;
            }

            if (serviceDefinition == null)
            {
                error =
                    "A valid package-owned API service definition is required.";
                return false;
            }

            if (!serviceDefinition.IsValid(out error))
            {
                error = "The API service definition is invalid. " + error;
                return false;
            }

            if (AssetDatabase.LoadMainAssetAtPath(normalizedPath) != null)
            {
                error = "An asset already exists at the selected path.";
                return false;
            }

            if (!serviceDefinition.TryGetEnvironmentDescriptors(
                    out IReadOnlyList<ApiEnvironmentDescriptor> descriptors,
                    out error) ||
                !serviceDefinition.TryGetRequiredClientIds(
                    out IReadOnlyList<ApiClientId> clients,
                    out error))
            {
                return false;
            }

            var environments = new List<ApiEnvironmentProfile>();
            bool createdRootAsset = false;
            try
            {
                foreach (ApiEnvironmentDescriptor descriptor in descriptors)
                {
                    environments.Add(CreateEnvironment(descriptor, clients));
                }

                settings = ApiConnectionSettings.CreateTransient(
                    environments,
                    serviceDefinition);
                settings.name = Path.GetFileNameWithoutExtension(normalizedPath);
                AssetDatabase.CreateAsset(settings, normalizedPath);
                createdRootAsset = true;
                foreach (ApiEnvironmentProfile environment in environments)
                {
                    AssetDatabase.AddObjectToAsset(environment, settings);
                }

                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(
                    normalizedPath,
                    ImportAssetOptions.ForceUpdate);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                if (createdRootAsset)
                {
                    AssetDatabase.DeleteAsset(normalizedPath);
                }

                DestroyTransient(settings);
                foreach (ApiEnvironmentProfile environment in environments)
                {
                    DestroyTransient(environment);
                }

                settings = null;
                error = "The API connection settings could not be created (" +
                    exception.GetType().Name + ").";
                return false;
            }
        }

        internal static ApiEnvironmentProfile CreateEnvironment(
            ApiEnvironmentDescriptor descriptor,
            IReadOnlyList<ApiClientId> requiredClients)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            ApiEnvironmentProfile environment =
                ScriptableObject.CreateInstance<ApiEnvironmentProfile>();
            environment.name = descriptor.DisplayName;
            environment.EnvironmentId = descriptor.EnvironmentId.Value;
            environment.DisplayName = descriptor.DisplayName;
            foreach (ApiClientId clientId in requiredClients)
            {
                environment.Clients.Add(
                    new ApiNamedClientDefinition
                    {
                        ClientId = clientId.Value,
                        BaseUrl = string.Empty
                    });
            }

            return environment;
        }

        private static bool TryNormalizeProjectAssetPath(
            string assetPath,
            out string normalizedPath,
            out string error)
        {
            normalizedPath = assetPath?.Trim().Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(normalizedPath) ||
                !normalizedPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !string.Equals(
                    Path.GetExtension(normalizedPath),
                    ".asset",
                    StringComparison.OrdinalIgnoreCase))
            {
                error =
                    "Choose a new .asset path inside this project's Assets folder.";
                normalizedPath = null;
                return false;
            }

            error = null;
            return true;
        }

        private static void DestroyTransient(UnityEngine.Object value)
        {
            if (value != null && !AssetDatabase.Contains(value))
            {
                Undo.DestroyObjectImmediate(value);
            }
        }
    }
}
