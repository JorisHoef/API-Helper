using System;
using System.Collections.Generic;
using Deucarian.API.Configuration;
using Deucarian.API.Models;
using Deucarian.Editor;
using UnityEditor;
using UnityEngine;

namespace Deucarian.API.Editor
{
    internal enum ApiServiceDefinitionOwnership
    {
        Missing = 0,
        ProjectOwned = 1,
        PackageManaged = 2,
        External = 3
    }

    [CustomEditor(typeof(ApiConnectionSettings))]
    internal sealed class ApiConnectionSettingsEditor : UnityEditor.Editor
    {
        private bool showAdvanced;

        public override UnityEngine.UIElements.VisualElement CreateInspectorGUI() =>

            DeucarianEditorInspector.Create(OnInspectorGUI);


        public override void OnInspectorGUI()
        {
            var settings = (ApiConnectionSettings)target;
            bool projectOwned = IsProjectOwned(settings);

            DeucarianEditorTextGUI.LabelField(
                "API Connection Settings",
                DeucarianEditorWorkbenchGUI.BoldLabelStyle);
            DeucarianEditorTextGUI.LabelField(
                "This project owns deployment hosts. The referenced service " +
                "definition owns stable environments, named clients, routes, " +
                "methods, and authentication requirements.",
                DeucarianEditorWorkbenchGUI.LabelStyle);
            EditorGUILayout.Space();

            DrawServiceDefinition(settings);
            EditorGUILayout.Space();
            DrawEnvironments(settings, projectOwned);
            EditorGUILayout.Space();
            DrawAdvanced(settings, projectOwned);

            if (!projectOwned)
            {
                EditorGUILayout.Space();
                DeucarianEditorTextGUI.HelpBox(
                    "Package-managed and transient connection settings are " +
                    "read-only. Use the integration's explicit setup action " +
                    "to create project-owned settings.",
                    MessageType.Info);
            }
        }

        private void DrawServiceDefinition(ApiConnectionSettings settings)
        {
            using (new EditorGUILayout.VerticalScope(DeucarianEditorStyles.SectionBox))
            {
                DeucarianEditorTextGUI.LabelField("API Service", DeucarianEditorWorkbenchGUI.BoldLabelStyle);
                serializedObject.Update();
                SerializedProperty property =
                    serializedObject.FindProperty("serviceDefinition");
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(
                        property,
                        new GUIContent("Service Definition"));
                }

                ApiServiceDefinition definition = settings.ServiceDefinition;
                ApiServiceDefinitionOwnership ownership =
                    GetDefinitionOwnership(definition);
                switch (ownership)
                {
                    case ApiServiceDefinitionOwnership.PackageManaged:
                        DrawState(
                            "Package",
                            "The installed integration owns this generated, " +
                            "credential-free contract.",
                            DeucarianEditorStatus.Success,
                            MessageType.Info);
                        break;
                    case ApiServiceDefinitionOwnership.ProjectOwned:
                        DrawState(
                            "Project override",
                            "This project forks the package contract. Updates " +
                            "must be reviewed and applied explicitly.",
                            DeucarianEditorStatus.Warning,
                            MessageType.Warning);
                        break;
                    case ApiServiceDefinitionOwnership.External:
                        DrawState(
                            "Runtime",
                            "The service definition is not a project or package asset.",
                            DeucarianEditorStatus.Info,
                            MessageType.Info);
                        break;
                    default:
                        DrawState(
                            "Missing",
                            "Open Deucarian Control Center and select an installed API integration.",
                            DeucarianEditorStatus.Error,
                            MessageType.Error);
                        break;
                }

                if (definition != null)
                {
                    DeucarianEditorTextGUI.LabelField(
                        "Service ID",
                        definition.ServiceId ?? string.Empty);
                    DeucarianEditorTextGUI.LabelField(
                        "Source version",
                        string.IsNullOrWhiteSpace(definition.SourceVersion)
                            ? "Not provided"
                            : definition.SourceVersion);
                }
            }
        }

        private static void DrawEnvironments(
            ApiConnectionSettings settings,
            bool projectOwned)
        {
            DeucarianEditorTextGUI.LabelField("Environments", DeucarianEditorWorkbenchGUI.BoldLabelStyle);
            ApiServiceDefinition definition = settings.ServiceDefinition;
            if (definition == null)
            {
                DeucarianEditorTextGUI.HelpBox(
                    "A service definition is required.",
                    MessageType.Error);
                return;
            }

            if (!definition.TryGetEnvironmentDescriptors(
                    out IReadOnlyList<ApiEnvironmentDescriptor> descriptors,
                    out string error))
            {
                DeucarianEditorTextGUI.HelpBox(error, MessageType.Error);
                return;
            }

            foreach (ApiEnvironmentDescriptor descriptor in descriptors)
            {
                DrawEnvironment(settings, descriptor, projectOwned);
                EditorGUILayout.Space(2f);
            }
        }

        private static void DrawEnvironment(
            ApiConnectionSettings settings,
            ApiEnvironmentDescriptor descriptor,
            bool projectOwned)
        {
            using (new EditorGUILayout.VerticalScope(DeucarianEditorStyles.SectionBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DeucarianEditorTextGUI.LabelField(
                        descriptor.DisplayName,
                        DeucarianEditorWorkbenchGUI.BoldLabelStyle);
                    GUILayout.FlexibleSpace();
                    DeucarianEditorTextGUI.LabelField(
                        descriptor.Stage.ToString(),
                        DeucarianEditorWorkbenchGUI.MiniLabelStyle,
                        GUILayout.Width(90f));
                }

                ApiEnvironmentProfile environment = FindEnvironment(
                    settings.Environments,
                    descriptor.EnvironmentId);
                if (environment == null)
                {
                    DrawState(
                        "Not configured",
                        "The package now provides environment '" +
                        descriptor.EnvironmentId +
                        "', but this older project asset has no connection slot yet.",
                        DeucarianEditorStatus.Warning,
                        MessageType.Warning);
                    if (projectOwned && DeucarianEditorActionGUI.Button(
                            "Add Missing Connection Slots"))
                    {
                        if (!ApiConnectionSettingsAssetFactory
                                .TrySynchronizeProjectSettings(
                                    settings,
                                    out int addedCount,
                                    out string error))
                        {
                            EditorUtility.DisplayDialog(
                                "Synchronize API Connection Settings",
                                error,
                                "OK");
                        }
                        else
                        {
                            EditorUtility.DisplayDialog(
                                "Synchronize API Connection Settings",
                                addedCount == 0
                                    ? "The connection settings are already synchronized."
                                    : addedCount == 1
                                    ? "Added one blank package environment slot."
                                    : "Added " + addedCount +
                                      " blank package environment slots.",
                                "OK");
                        }
                    }
                    return;
                }

                IReadOnlyList<ApiNamedClientDefinition> clients =
                    environment.Clients;
                bool canEdit = projectOwned && IsProjectOwned(environment);
                for (int index = 0; index < clients.Count; index++)
                {
                    ApiNamedClientDefinition client = clients[index];
                    if (client == null)
                    {
                        DeucarianEditorTextGUI.HelpBox(
                            "Named client " + (index + 1) + " is missing.",
                            MessageType.Error);
                        continue;
                    }

                    using (new EditorGUI.DisabledScope(!canEdit))
                    {
                        EditorGUI.BeginChangeCheck();
                        string baseUrl = DeucarianEditorInputGUI.TextField(
                            GetBaseUrlLabel(client, clients.Count),
                            client.BaseUrl ?? string.Empty);
                        if (EditorGUI.EndChangeCheck())
                        {
                            Undo.RecordObject(
                                environment,
                                "Configure API environment host");
                            client.BaseUrl = baseUrl;
                            EditorUtility.SetDirty(environment);
                        }
                    }
                }

                ApiEnvironmentProfileConfigurationState state =
                    environment.ClassifyConfiguration(out string message);
                switch (state)
                {
                    case ApiEnvironmentProfileConfigurationState.Configured:
                        DrawState(
                            "Configured",
                            "Every required client has a valid HTTP(S) base URL.",
                            DeucarianEditorStatus.Success,
                            MessageType.Info);
                        break;
                    case ApiEnvironmentProfileConfigurationState.NotConfigured:
                        DrawState(
                            "Missing",
                            "Requests remain blocked until all required hosts are configured.",
                            DeucarianEditorStatus.Warning,
                            MessageType.Info);
                        break;
                    default:
                        DrawState(
                            "Invalid",
                            message ?? "This environment is invalid.",
                            DeucarianEditorStatus.Error,
                            MessageType.Error);
                        break;
                }
            }
        }

        private void DrawAdvanced(
            ApiConnectionSettings settings,
            bool projectOwned)
        {
            showAdvanced = DeucarianEditorInputGUI.Foldout(
                showAdvanced,
                "Advanced policies",
                true);
            if (!showAdvanced)
            {
                return;
            }

            DeucarianEditorTextGUI.HelpBox(
                "Identifiers are managed by the service definition. Do not " +
                "store credentials or secret headers in this asset.",
                MessageType.Info);
            using (new EditorGUI.DisabledScope(!projectOwned))
            {
                foreach (ApiEnvironmentProfile environment in
                    settings.Environments)
                {
                    if (environment == null)
                    {
                        continue;
                    }

                    EditorGUILayout.Space(2f);
                    DeucarianEditorTextGUI.LabelField(
                        environment.DisplayName ?? environment.name,
                        DeucarianEditorWorkbenchGUI.BoldLabelStyle);
                    using (new EditorGUI.DisabledScope(
                               !IsProjectOwned(environment)))
                    {
                        var environmentObject = new SerializedObject(environment);
                        environmentObject.Update();
                        EditorGUILayout.PropertyField(
                            environmentObject.FindProperty("defaultRequestPolicy"),
                            new GUIContent("Environment Policy"),
                            true);
                        EditorGUILayout.PropertyField(
                            environmentObject.FindProperty("clients"),
                            new GUIContent("Named Clients"),
                            true);
                        environmentObject.ApplyModifiedProperties();
                    }
                }
            }
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

        internal static string GetBaseUrlLabel(
            ApiNamedClientDefinition client,
            int clientCount)
        {
            if (clientCount <= 1)
            {
                return "Base URL";
            }

            string clientId = client?.ClientId?.Trim();
            return string.IsNullOrWhiteSpace(clientId)
                ? "Client Base URL"
                : clientId + " Base URL";
        }

        private static void DrawState(
            string label,
            string message,
            DeucarianEditorStatus status,
            MessageType messageType)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DeucarianEditorTextGUI.LabelField("Status", GUILayout.Width(116f));
                DeucarianEditorStatusBadge.Draw(
                    label,
                    status,
                    GUILayout.Width(128f));
            }

            DeucarianEditorTextGUI.HelpBox(message, messageType);
        }

        internal static ApiServiceDefinitionOwnership GetDefinitionOwnership(
            ApiServiceDefinition definition)
        {
            if (definition == null)
            {
                return ApiServiceDefinitionOwnership.Missing;
            }

            string path = AssetDatabase.GetAssetPath(definition)
                ?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(path))
            {
                return ApiServiceDefinitionOwnership.External;
            }

            if (path.StartsWith("Packages/", StringComparison.Ordinal))
            {
                return ApiServiceDefinitionOwnership.PackageManaged;
            }

            return path.StartsWith("Assets/", StringComparison.Ordinal)
                ? ApiServiceDefinitionOwnership.ProjectOwned
                : ApiServiceDefinitionOwnership.External;
        }

        private static bool IsProjectOwned(UnityEngine.Object value)
        {
            string path = AssetDatabase.GetAssetPath(value)
                ?.Replace('\\', '/');
            return !string.IsNullOrWhiteSpace(path) &&
                path.StartsWith("Assets/", StringComparison.Ordinal);
        }
    }
}
