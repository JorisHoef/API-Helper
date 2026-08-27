using System;
using System.Collections.Generic;
using Deucarian.API.Configuration;
using Deucarian.API.Models;
using UnityEditor;
using UnityEngine;

namespace Deucarian.API.Editor
{
    /// <summary>Explicit GUID bindings for project-owned API connections.</summary>
    [FilePath(
        "ProjectSettings/DeucarianApiConnections.asset",
        FilePathAttribute.Location.ProjectFolder)]
    public sealed class ApiConnectionProjectSettings :
        ScriptableSingleton<ApiConnectionProjectSettings>
    {
        [Serializable]
        public sealed class Binding
        {
            [SerializeField] private string serviceId = string.Empty;
            [SerializeField] private string settingsGuid = string.Empty;

            public string ServiceId => serviceId ?? string.Empty;
            public string SettingsGuid => settingsGuid ?? string.Empty;

            internal Binding(string service, string guid)
            {
                serviceId = service;
                settingsGuid = guid;
            }
        }

        [SerializeField] private List<Binding> bindings = new List<Binding>();

        public IReadOnlyList<Binding> Bindings => bindings;

        public bool TryBind(
            ApiConnectionSettings settings,
            out string error)
        {
            if (!TryDescribeSettings(
                    settings,
                    out ApiServiceId serviceId,
                    out string guid,
                    out error))
            {
                return false;
            }

            bindings.RemoveAll(candidate =>
                candidate != null && string.Equals(
                    candidate.ServiceId,
                    serviceId.Value,
                    StringComparison.Ordinal));
            bindings.Add(new Binding(serviceId.Value, guid));
            Save(true);
            error = null;
            return true;
        }

        public bool TryResolve(
            ApiServiceId serviceId,
            out ApiConnectionSettings settings,
            out string error)
        {
            settings = null;
            if (serviceId.IsEmpty)
            {
                error = "A stable API service ID is required.";
                return false;
            }

            Binding match = null;
            foreach (Binding candidate in bindings)
            {
                if (candidate == null || !string.Equals(
                        candidate.ServiceId,
                        serviceId.Value,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (match != null)
                {
                    error = "DEU-API-002 Multiple canonical connection " +
                        "settings are bound for service '" + serviceId + "'.";
                    return false;
                }

                match = candidate;
            }

            if (match == null)
            {
                error = "DEU-API-001 Required API connection settings are " +
                    "missing for service '" + serviceId + "'.";
                return false;
            }

            string assetPath = AssetDatabase.GUIDToAssetPath(
                match.SettingsGuid);
            if (string.IsNullOrWhiteSpace(assetPath) ||
                !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                error = "DEU-API-001 The bound connection for service '" +
                    serviceId + "' is missing or package-managed.";
                return false;
            }

            settings = AssetDatabase.LoadAssetAtPath<ApiConnectionSettings>(
                assetPath);
            if (settings == null ||
                !settings.ServiceDefinition.TryGetId(
                    out ApiServiceId actualService) ||
                actualService != serviceId)
            {
                settings = null;
                error = "DEU-API-005 The bound connection settings are " +
                    "incompatible with service '" + serviceId + "'.";
                return false;
            }

            if (!settings.TryValidate(out error))
            {
                error = "DEU-API-003 " + error;
                settings = null;
                return false;
            }

            error = null;
            return true;
        }

        public void Clear(ApiServiceId serviceId)
        {
            if (serviceId.IsEmpty)
            {
                return;
            }

            if (bindings.RemoveAll(candidate =>
                    candidate != null && string.Equals(
                        candidate.ServiceId,
                        serviceId.Value,
                        StringComparison.Ordinal)) > 0)
            {
                Save(true);
            }
        }

        private static bool TryDescribeSettings(
            ApiConnectionSettings settings,
            out ApiServiceId serviceId,
            out string guid,
            out string error)
        {
            serviceId = default(ApiServiceId);
            guid = null;
            if (settings == null || settings.ServiceDefinition == null ||
                !settings.ServiceDefinition.TryGetId(out serviceId))
            {
                error = "Select valid API connection settings.";
                return false;
            }

            string path = AssetDatabase.GetAssetPath(settings);
            if (string.IsNullOrWhiteSpace(path) ||
                !path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                error = "Connection settings must be a project-owned asset " +
                    "inside Assets, not a package asset.";
                return false;
            }

            guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrWhiteSpace(guid))
            {
                error = "The selected connection settings have no asset GUID.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
