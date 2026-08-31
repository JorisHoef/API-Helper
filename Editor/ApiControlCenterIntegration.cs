using System.Collections.Generic;
using Deucarian.API.Configuration;
using Deucarian.API.Models;
using Deucarian.Editor;
using UnityEditor;

namespace Deucarian.API.Editor
{
    [InitializeOnLoad]
    internal static class ApiControlCenterIntegration
    {
        private const string PackageId = "com.deucarian.api";

        static ApiControlCenterIntegration()
        {
            DeucarianToolRegistry.Register(new DeucarianToolDescriptor(
                DeucarianToolIds.ApiConnections,
                "API Connections",
                "Configure project-owned connections for package-owned services.",
                DeucarianControlCenterArea.Connections,
                ApiConnectionsWindow.Open,
                PackageId,
                "cloudconnect",
                new[] { "api", "environment", "service", "connection" },
                10));
            DeucarianControlCenterRegistry.RegisterCardProvider(
                new ApiControlCenterCardProvider());
        }
    }

    internal sealed class ApiControlCenterCardProvider :
        IDeucarianControlCenterCardProvider
    {
        public string Id => "com.deucarian.api.status";

        public IEnumerable<DeucarianControlCenterCard> Capture(
            DeucarianControlCenterContext context)
        {
            int bindingCount = 0;
            int resolvedCount = 0;
            int invalidCount = 0;
            int configuredEnvironmentCount = 0;
            int unconfiguredEnvironmentCount = 0;
            foreach (ApiConnectionProjectSettings.Binding binding in
                ApiConnectionProjectSettings.instance.Bindings)
            {
                if (binding == null)
                {
                    invalidCount++;
                    continue;
                }

                bindingCount++;
                if (!ApiServiceId.TryParse(
                        binding.ServiceId,
                        out ApiServiceId serviceId) ||
                    !ApiConnectionProjectSettings.instance.TryResolve(
                        serviceId,
                        out ApiConnectionSettings settings,
                        out _))
                {
                    invalidCount++;
                    continue;
                }

                resolvedCount++;
                foreach (ApiEnvironmentProfile environment in settings.Environments)
                {
                    if (environment != null &&
                        environment.ClassifyConfiguration(out _) ==
                        ApiEnvironmentProfileConfigurationState.Configured)
                    {
                        configuredEnvironmentCount++;
                    }
                    else
                    {
                        unconfiguredEnvironmentCount++;
                    }
                }
            }

            yield return CreateConnectionsCard(
                bindingCount,
                resolvedCount,
                invalidCount,
                configuredEnvironmentCount,
                unconfiguredEnvironmentCount);
            yield return CreateDeveloperCard(APIDebugSettingsMenu.LogRawJsonEnabled);
        }

        internal static DeucarianControlCenterCard CreateConnectionsCard(
            int bindingCount,
            int resolvedCount,
            int invalidCount,
            int configuredEnvironmentCount,
            int unconfiguredEnvironmentCount)
        {
            DeucarianControlCenterStatus status = invalidCount > 0
                ? DeucarianControlCenterStatus.Error
                : bindingCount == 0 || configuredEnvironmentCount == 0
                    ? DeucarianControlCenterStatus.Warning
                    : DeucarianControlCenterStatus.Success;
            string statusText = invalidCount > 0
                ? invalidCount + " invalid binding(s)"
                : bindingCount == 0
                    ? "No services bound"
                    : configuredEnvironmentCount == 0
                        ? "Hosts are not configured"
                        : "Connections configured";

            return new DeucarianControlCenterCard(
                "api.connections",
                DeucarianControlCenterArea.Connections,
                "API Connections",
                "Project-owned connection readiness without hosts or credentials.",
                "com.deucarian.api",
                status,
                statusText,
                10,
                new[]
                {
                    resolvedCount + " of " + bindingCount + " service binding(s) resolve.",
                    configuredEnvironmentCount + " configured and " +
                    unconfiguredEnvironmentCount + " unconfigured environment(s)."
                },
                new[]
                {
                    new DeucarianControlCenterAction(
                        "api.open-connections",
                        "Open API Connections",
                        ApiConnectionsWindow.Open,
                        "Configure project-owned environment hosts.")
                },
                new[] { "api", "connections", "services", "environments", "hosts" });
        }

        internal static DeucarianControlCenterCard CreateDeveloperCard(bool rawJsonEnabled)
        {
            return new DeucarianControlCenterCard(
                "api.developer-settings",
                DeucarianControlCenterArea.Developer,
                "API Developer Settings",
                "Advanced diagnostic controls for API development.",
                "com.deucarian.api",
                rawJsonEnabled
                    ? DeucarianControlCenterStatus.Warning
                    : DeucarianControlCenterStatus.Info,
                rawJsonEnabled ? "Raw JSON logging enabled" : "Raw JSON logging disabled",
                20,
                new[]
                {
                    rawJsonEnabled
                        ? "Response bodies may be written to local diagnostics."
                        : "Response-body logging is off."
                },
                new[]
                {
                    new DeucarianControlCenterAction(
                        "api.toggle-raw-json",
                        rawJsonEnabled ? "Disable Raw JSON Logging" : "Enable Raw JSON Logging",
                        APIDebugSettingsMenu.ToggleLogRawJson,
                        "Change the local API response logging preference.",
                        new[] { "json", "logging", "debug" },
                        true)
                },
                new[] { "api", "developer", "raw json", "logging" });
        }
    }
}
