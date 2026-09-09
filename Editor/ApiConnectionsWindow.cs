using System;
using System.Collections.Generic;
using Deucarian.API.Configuration;
using Deucarian.API.Models;
using Deucarian.Editor;
using UnityEditor;
using UnityEngine;

namespace Deucarian.API.Editor
{
    /// <summary>Explicit setup and status UI for canonical API connections.</summary>
    public sealed class ApiConnectionsWindow : EditorWindow
    {
        private ApiServiceDefinition serviceDefinition;
        private ApiConnectionSettings selectedSettings;
        private Vector2 scroll;
        private string message;

        public static void Open()
        {
            var window = DeucarianEditorWindowPages.GetStandalone<ApiConnectionsWindow>("API Connections");
            window.minSize = new Vector2(460f, 420f);
            window.Show();
        }

        public static IDeucarianEditorPage CreatePage() =>
            DeucarianEditorImGuiPage.Create<ApiConnectionsWindow>(DeucarianToolIds.ApiConnections, window => window.OnGUI());

        private void OnGUI()
        {
            using (DeucarianEditorWorkbenchPanelScope page =
                   DeucarianEditorWorkbenchGUI.BeginSettingsPage(
                       GUILayout.ExpandHeight(true)))
            {
                scroll = EditorGUILayout.BeginScrollView(scroll);
                DeucarianEditorChrome.DrawPackageHeader(
                    "network",
                    "API Connections",
                    "Bind project-owned environment hosts to package service definitions.");
                EditorGUILayout.HelpBox(
                    "Each service must bind exactly one project-owned settings " +
                    "asset by GUID. Packages provide definitions, never hosts.",
                    MessageType.Info);

                DrawCreateAndBind();
                GUILayout.Space(8f);
                DrawBindings();
                GUILayout.Space(8f);
                DrawSelectedSettings();

                if (!string.IsNullOrWhiteSpace(message))
                {
                    EditorGUILayout.HelpBox(message, MessageType.Info);
                }

                DeucarianEditorChrome.DrawFooterVersion("com.deucarian.api");
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawCreateAndBind()
        {
            EditorGUILayout.LabelField("Explicit setup", EditorStyles.boldLabel);
            serviceDefinition = (ApiServiceDefinition)EditorGUILayout.ObjectField(
                "Service definition",
                serviceDefinition,
                typeof(ApiServiceDefinition),
                false);
            selectedSettings =
                (ApiConnectionSettings)EditorGUILayout.ObjectField(
                    "Connection settings",
                    selectedSettings,
                    typeof(ApiConnectionSettings),
                    false);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create settings"))
                {
                    CreateSettings();
                }

                using (new EditorGUI.DisabledScope(selectedSettings == null))
                {
                    if (GUILayout.Button("Bind selected"))
                    {
                        BindSelected();
                    }
                }
            }
        }

        private void DrawBindings()
        {
            EditorGUILayout.LabelField("Project bindings", EditorStyles.boldLabel);
            var project = ApiConnectionProjectSettings.instance;
            if (project.Bindings.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "DEU-API-001 No API connection is bound.",
                    MessageType.Error);
                return;
            }

            foreach (ApiConnectionProjectSettings.Binding binding in
                project.Bindings)
            {
                if (binding == null)
                {
                    continue;
                }

                string path = AssetDatabase.GUIDToAssetPath(
                    binding.SettingsGuid);
                ApiConnectionSettings settings =
                    AssetDatabase.LoadAssetAtPath<ApiConnectionSettings>(path);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        binding.ServiceId,
                        GUILayout.Width(180f));
                    EditorGUILayout.ObjectField(
                        settings,
                        typeof(ApiConnectionSettings),
                        false);
                    if (GUILayout.Button("Select", GUILayout.Width(58f)))
                    {
                        selectedSettings = settings;
                        Selection.activeObject = settings;
                    }
                    if (GUILayout.Button("Clear", GUILayout.Width(52f)) &&
                        ApiServiceId.TryParse(
                            binding.ServiceId,
                            out ApiServiceId serviceId))
                    {
                        project.Clear(serviceId);
                        GUIUtility.ExitGUI();
                    }
                }

                string error = null;
                if (!ApiServiceId.TryParse(
                        binding.ServiceId,
                        out ApiServiceId id))
                {
                    error = "DEU-API-005 The stored service ID is invalid.";
                }
                else if (!project.TryResolve(id, out _, out error))
                {
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    EditorGUILayout.HelpBox(error, MessageType.Error);
                }
            }
        }

        private void DrawSelectedSettings()
        {
            if (selectedSettings == null)
            {
                return;
            }

            EditorGUILayout.LabelField(
                "Environment hosts",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Blank environments remain visibly unconfigured and cannot " +
                "resolve traffic. No environment is selected implicitly.",
                MessageType.None);
            ApiServiceDefinition definition =
                selectedSettings.ServiceDefinition;
            if (definition == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign the package-owned API service definition.",
                    MessageType.Error);
                return;
            }

            if (!definition.TryGetEnvironmentDescriptors(
                    out IReadOnlyList<ApiEnvironmentDescriptor> descriptors,
                    out string descriptorError))
            {
                EditorGUILayout.HelpBox(
                    descriptorError,
                    MessageType.Error);
                return;
            }

            int missingSlotCount = 0;
            foreach (ApiEnvironmentDescriptor descriptor in descriptors)
            {
                ApiEnvironmentProfile environment = FindEnvironment(
                    selectedSettings.Environments,
                    descriptor.EnvironmentId);
                if (environment == null)
                {
                    missingSlotCount++;
                    EditorGUILayout.LabelField(
                        descriptor.DisplayName,
                        EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox(
                        "This package environment is known but its project " +
                        "connection slot has not been added yet.",
                        MessageType.Warning);
                    continue;
                }

                EditorGUILayout.LabelField(
                    descriptor.DisplayName,
                    EditorStyles.boldLabel);
                foreach (ApiNamedClientDefinition client in environment.Clients)
                {
                    if (client == null)
                    {
                        continue;
                    }

                    string next = EditorGUILayout.TextField(
                        client.ClientId,
                        client.BaseUrl ?? string.Empty);
                    if (!string.Equals(
                            next,
                            client.BaseUrl,
                            StringComparison.Ordinal))
                    {
                        Undo.RecordObject(environment, "Configure API host");
                        client.BaseUrl = next.Trim();
                        EditorUtility.SetDirty(environment);
                        EditorUtility.SetDirty(selectedSettings);
                    }
                }
            }

            bool requiresSynchronization =
                RequiresSynchronization(selectedSettings, descriptors);
            if (requiresSynchronization &&
                GUILayout.Button(
                    missingSlotCount > 0
                        ? "Add Missing Connection Slots"
                        : "Synchronize Connection Slot Order"))
            {
                SynchronizeSelectedSettings();
            }

            if (selectedSettings.TryValidate(out string validMessage))
            {
                EditorGUILayout.HelpBox(
                    missingSlotCount > 0
                        ? "Connection shape is compatible. Add the missing " +
                          "package slots to configure every environment."
                        : "Connection shape is valid. Blank environments remain " +
                          "unavailable until configured.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(validMessage, MessageType.Error);
            }

            if (GUILayout.Button("Save connection settings"))
            {
                AssetDatabase.SaveAssets();
                message = "Connection settings saved.";
            }
        }

        private void SynchronizeSelectedSettings()
        {
            string assetPath = AssetDatabase.GetAssetPath(selectedSettings);
            if (!ApiConnectionSettingsAssetFactory.TrySynchronizeProjectSettings(
                    selectedSettings,
                    out int addedCount,
                    out string error))
            {
                message = error;
                return;
            }

            if (!string.IsNullOrWhiteSpace(assetPath))
            {
                selectedSettings =
                    AssetDatabase.LoadAssetAtPath<ApiConnectionSettings>(
                        assetPath);
            }

            message = addedCount == 0
                ? "Connection slots synchronized with package order."
                : addedCount == 1
                    ? "Added one blank package environment slot."
                    : "Added " + addedCount +
                      " blank package environment slots.";
            GUIUtility.ExitGUI();
        }

        private static bool RequiresSynchronization(
            ApiConnectionSettings settings,
            IReadOnlyList<ApiEnvironmentDescriptor> descriptors)
        {
            if (settings.Environments.Count != descriptors.Count)
            {
                return true;
            }

            for (int index = 0; index < descriptors.Count; index++)
            {
                ApiEnvironmentProfile expected = FindEnvironment(
                    settings.Environments,
                    descriptors[index].EnvironmentId);
                if (!ReferenceEquals(settings.Environments[index], expected))
                {
                    return true;
                }
            }

            return false;
        }

        private static ApiEnvironmentProfile FindEnvironment(
            IReadOnlyList<ApiEnvironmentProfile> environments,
            ApiEnvironmentId environmentId)
        {
            if (environments != null)
            {
                foreach (ApiEnvironmentProfile environment in environments)
                {
                    if (environment != null &&
                        environment.TryGetId(out ApiEnvironmentId candidate) &&
                        candidate == environmentId)
                    {
                        return environment;
                    }
                }
            }

            return null;
        }

        private void CreateSettings()
        {
            if (serviceDefinition == null)
            {
                message = "Select a package-owned service definition first.";
                return;
            }

            string path = EditorUtility.SaveFilePanelInProject(
                "Create API Connection Settings",
                serviceDefinition.name + "ConnectionSettings",
                "asset",
                "Choose a project-owned asset path.");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (!ApiConnectionSettingsAssetFactory.TryCreateProjectSettings(
                    path,
                    serviceDefinition,
                    out selectedSettings,
                    out message))
            {
                return;
            }

            BindSelected();
            Selection.activeObject = selectedSettings;
        }

        private void BindSelected()
        {
            if (ApiConnectionProjectSettings.instance.TryBind(
                    selectedSettings,
                    out string error))
            {
                message = "Bound project connection settings by asset GUID.";
            }
            else
            {
                message = error;
            }
        }

        [SettingsProvider]
        private static SettingsProvider CreateSettingsProvider()
        {
            return new SettingsProvider(
                "Project/Deucarian/API Connections",
                SettingsScope.Project)
            {
                label = "API Connections",
                guiHandler = _ =>
                {
                    EditorGUILayout.HelpBox(
                        "Configure explicit GUID bindings and environment " +
                        "hosts in the API Connections tool.",
                        MessageType.Info);
                    if (GUILayout.Button("Open API Connections"))
                    {
                        Open();
                    }
                }
            };
        }
    }
}
