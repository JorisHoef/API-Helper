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

        /// <summary>
        /// Adds blank project-owned slots for environments introduced by a
        /// newer package service definition. Existing configured slots are
        /// preserved exactly.
        /// </summary>
        public static bool TrySynchronizeProjectSettings(
            ApiConnectionSettings settings,
            out int addedEnvironmentCount,
            out string error)
        {
            return TrySynchronizeProjectSettings(
                settings,
                (environment, owner) =>
                    AssetDatabase.AddObjectToAsset(environment, owner),
                out addedEnvironmentCount,
                out error);
        }

        internal static bool TrySynchronizeProjectSettings(
            ApiConnectionSettings settings,
            Action<ApiEnvironmentProfile, ApiConnectionSettings>
                addObjectToAsset,
            out int addedEnvironmentCount,
            out string error)
        {
            addedEnvironmentCount = 0;
            if (settings == null)
            {
                error = "API connection settings are required.";
                return false;
            }

            if (addObjectToAsset == null)
            {
                error = "An asset synchronization action is required.";
                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(settings);
            if (string.IsNullOrWhiteSpace(assetPath) ||
                !assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                AssetDatabase.LoadMainAssetAtPath(assetPath) != settings)
            {
                error =
                    "Only a project-owned API connection settings asset can be synchronized.";
                return false;
            }

            ApiServiceDefinition definition = settings.ServiceDefinition;
            if (definition == null)
            {
                error = "Assign the package-owned API service definition.";
                return false;
            }

            if (!definition.IsValid(out error) ||
                !definition.TryGetEnvironmentDescriptors(
                    out IReadOnlyList<ApiEnvironmentDescriptor> descriptors,
                    out error) ||
                !definition.TryGetRequiredClientIds(
                    out IReadOnlyList<ApiClientId> clients,
                    out error) ||
                !settings.TryValidate(out error))
            {
                return false;
            }

            var existing = new Dictionary<
                ApiEnvironmentId,
                ApiEnvironmentProfile>();
            foreach (ApiEnvironmentProfile environment in settings.Environments)
            {
                environment.TryGetId(out ApiEnvironmentId environmentId);
                existing.Add(environmentId, environment);
            }

            bool requiresChange =
                settings.Environments.Count != descriptors.Count;
            for (int index = 0;
                 !requiresChange && index < descriptors.Count;
                 index++)
            {
                requiresChange = !ReferenceEquals(
                    settings.Environments[index],
                    existing[descriptors[index].EnvironmentId]);
            }

            if (!requiresChange)
            {
                error = null;
                return true;
            }

            var original = new List<ApiEnvironmentProfile>(
                settings.Environments);
            var additions = new List<ApiEnvironmentProfile>();
            var synchronized = new List<ApiEnvironmentProfile>(
                descriptors.Count);
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Synchronize API Connection Settings");
            try
            {
                Undo.RegisterCompleteObjectUndo(
                    settings,
                    "Synchronize API Connection Settings");
                foreach (ApiEnvironmentDescriptor descriptor in descriptors)
                {
                    if (existing.TryGetValue(
                            descriptor.EnvironmentId,
                            out ApiEnvironmentProfile environment))
                    {
                        synchronized.Add(environment);
                        continue;
                    }

                    environment = CreateEnvironment(descriptor, clients);
                    additions.Add(environment);
                    Undo.RegisterCreatedObjectUndo(
                        environment,
                        "Synchronize API Connection Settings");
                    addObjectToAsset(environment, settings);
                    synchronized.Add(environment);
                }

                settings.SetManagedEnvironments(synchronized);
                addedEnvironmentCount = additions.Count;
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceUpdate);
                Undo.CollapseUndoOperations(undoGroup);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                TryRevertSynchronization(
                    settings,
                    assetPath,
                    original,
                    additions,
                    undoGroup);
                addedEnvironmentCount = 0;
                error = "The API connection settings could not be synchronized (" +
                    exception.GetType().Name + ").";
                return false;
            }
        }

        private static void TryRevertSynchronization(
            ApiConnectionSettings settings,
            string assetPath,
            IReadOnlyList<ApiEnvironmentProfile> original,
            IReadOnlyList<ApiEnvironmentProfile> additions,
            int undoGroup)
        {
            try
            {
                Undo.RevertAllDownToGroup(undoGroup);
            }
            catch (Exception)
            {
            }

            try
            {
                if (settings != null)
                {
                    settings.SetManagedEnvironments(original);
                }
            }
            catch (Exception)
            {
            }

            for (int index = 0; index < additions.Count; index++)
            {
                ApiEnvironmentProfile environment = additions[index];
                if (environment != null)
                {
                    try
                    {
                        Undo.DestroyObjectImmediate(environment);
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            try
            {
                if (settings != null)
                {
                    EditorUtility.SetDirty(settings);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceUpdate);
            }
            catch (Exception)
            {
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
