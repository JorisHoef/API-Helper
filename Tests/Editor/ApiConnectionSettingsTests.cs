using System;
using System.Linq;
using Deucarian.API.Configuration;
using Deucarian.API.Core;
using Deucarian.API.Editor;
using Deucarian.API.Models;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Deucarian.API.Tests
{
    public sealed class ApiConnectionSettingsTests
    {
        private const string TestDirectory =
            "Assets/__DeucarianApiConnectionSettingsTests";
        private const string SettingsPath =
            TestDirectory + "/ApiConnectionSettings.asset";
        private const string CatalogPath =
            TestDirectory + "/EndpointCatalog.asset";
        private const string DefinitionPath =
            TestDirectory + "/ServiceDefinition.asset";
        private static readonly ApiClientId PrimaryClientId =
            new ApiClientId("primary");

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TestDirectory);
            AssetDatabase.CreateFolder(
                "Assets",
                "__DeucarianApiConnectionSettingsTests");
        }

        [TearDown]
        public void TearDown()
        {
            ApiConnectionProjectSettings.instance.Clear(
                new ApiServiceId("example.api"));
            AssetDatabase.DeleteAsset(TestDirectory);
            AssetDatabase.Refresh();
        }

        [Test]
        public void Factory_CreatesCompleteBlankSlotsFromServiceDefinition()
        {
            ApiServiceDefinition definition = CreateServiceDefinitionAsset();
            bool created =
                ApiConnectionSettingsAssetFactory.TryCreateProjectSettings(
                    SettingsPath,
                    definition,
                    out ApiConnectionSettings settings,
                    out string error);

            Assert.IsTrue(created, error);
            Assert.AreSame(definition, settings.ServiceDefinition);
            Assert.AreEqual(4, settings.Environments.Count);
            foreach (ApiEnvironmentProfile environment in settings.Environments)
            {
                Assert.AreEqual(1, environment.Clients.Count);
                Assert.AreEqual(
                    PrimaryClientId.Value,
                    environment.Clients[0].ClientId);
                Assert.IsTrue(
                    string.IsNullOrEmpty(environment.Clients[0].BaseUrl));
                Assert.AreEqual(
                    ApiEnvironmentProfileConfigurationState.NotConfigured,
                    environment.ClassifyConfiguration(out string message));
                Assert.IsNull(message);
            }

            UnityEngine.Object[] assets =
                AssetDatabase.LoadAllAssetsAtPath(SettingsPath);
            Assert.AreEqual(5, assets.Length);
            Assert.AreEqual(
                4,
                assets.OfType<ApiEnvironmentProfile>().Count());
        }

        [Test]
        public void Settings_RoundTripAndComposeProjectHostOverrides()
        {
            ApiServiceDefinition definition = CreateServiceDefinitionAsset();
            Assert.IsTrue(
                ApiConnectionSettingsAssetFactory.TryCreateProjectSettings(
                    SettingsPath,
                    definition,
                    out ApiConnectionSettings settings,
                    out string error),
                error);
            ApiEnvironmentProfile development = settings.Environments[0];
            development.Clients[0].BaseUrl =
                "https://development.example.com/root";
            development.DefaultRequestPolicy.TimeoutSeconds = 41;
            EditorUtility.SetDirty(development);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(
                SettingsPath,
                ImportAssetOptions.ForceUpdate);

            ApiConnectionSettings reloaded =
                AssetDatabase.LoadAssetAtPath<ApiConnectionSettings>(
                    SettingsPath);
            Assert.IsNotNull(reloaded);
            Assert.AreSame(definition, reloaded.ServiceDefinition);
            Assert.IsTrue(
                reloaded.TryCreateComposition(
                    out ApiComposition composition,
                    out string compositionError),
                compositionError);

            ApiResolvedEndpoint endpoint = composition.ResolveEndpoint(
                new ApiEnvironmentId("development"),
                new ApiEndpointId("health.get"));
            Assert.AreEqual(
                "https://development.example.com/root/health",
                endpoint.Endpoint.Path);
            Assert.AreEqual(41, endpoint.RequestPolicy.TimeoutSeconds);
            Assert.AreEqual(
                ApiEnvironmentAvailability.Unconfigured,
                composition.GetEnvironmentStatus("testing").Availability);
        }

        [Test]
        public void Settings_ExplainMissingServiceWithoutResolvingTraffic()
        {
            ApiConnectionSettings settings =
                ScriptableObject.CreateInstance<ApiConnectionSettings>();
            try
            {
                Assert.IsFalse(
                    settings.TryCreateComposition(
                        out ApiComposition composition,
                        out string message));
                Assert.IsNull(composition);
                StringAssert.Contains("service definition", message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void ServiceDefinition_ValidatesTypedIdentityAndClientContract()
        {
            ApiServiceDefinition definition = CreateServiceDefinitionAsset();
            Assert.IsTrue(definition.IsValid(out string message), message);
            Assert.IsTrue(definition.TryGetId(out ApiServiceId serviceId));
            Assert.AreEqual(new ApiServiceId("example.api"), serviceId);
            Assert.IsTrue(
                definition.TryGetEnvironmentDescriptors(
                    out var environments,
                    out message),
                message);
            CollectionAssert.AreEqual(
                ApiEnvironmentStages.Standard,
                environments.Select(environment => environment.Stage));

            definition.RequiredClients.Clear();
            Assert.IsFalse(definition.IsValid(out message));
            StringAssert.Contains("required client", message);
        }

        [Test]
        public void DefinitionOwnership_DistinguishesProjectMissingAndTransient()
        {
            Assert.AreEqual(
                ApiServiceDefinitionOwnership.Missing,
                ApiConnectionSettingsEditor.GetDefinitionOwnership(null));

            ApiServiceDefinition projectDefinition =
                CreateServiceDefinitionAsset();
            Assert.AreEqual(
                ApiServiceDefinitionOwnership.ProjectOwned,
                ApiConnectionSettingsEditor.GetDefinitionOwnership(
                    projectDefinition));

            ApiServiceDefinition transient =
                ScriptableObject.CreateInstance<ApiServiceDefinition>();
            try
            {
                Assert.AreEqual(
                    ApiServiceDefinitionOwnership.External,
                    ApiConnectionSettingsEditor.GetDefinitionOwnership(
                        transient));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(transient);
            }
        }

        [Test]
        public void Inspector_LabelsEveryNamedClientWithoutAssumingPrimary()
        {
            var primary = new ApiNamedClientDefinition
            {
                ClientId = "vendor.primary"
            };
            var media = new ApiNamedClientDefinition
            {
                ClientId = "vendor.media"
            };

            Assert.AreEqual(
                "Base URL",
                ApiConnectionSettingsEditor.GetBaseUrlLabel(primary, 1));
            Assert.AreEqual(
                "vendor.primary Base URL",
                ApiConnectionSettingsEditor.GetBaseUrlLabel(primary, 2));
            Assert.AreEqual(
                "vendor.media Base URL",
                ApiConnectionSettingsEditor.GetBaseUrlLabel(media, 2));
        }

        [Test]
        public void InternalBuildingBlocks_HaveNoNormalAssetCreationMenus()
        {
            AssertNoCreateAssetMenu<ApiConnectionSettings>();
            AssertNoCreateAssetMenu<ApiServiceDefinition>();
            AssertNoCreateAssetMenu<ApiEnvironmentProfile>();
            AssertNoCreateAssetMenu<ApiEndpointCatalog>();
            AssertCreateAssetMenu<ApiClientConfig>(
                "Deucarian/API/Advanced/Building Blocks/Client Config");
            AssertCreateAssetMenu<ApiEndpointDefinition>(
                "Deucarian/API/Advanced/Building Blocks/Endpoint Definition");
        }

        [Test]
        public void Factory_RejectsPathsOutsideProjectAssets()
        {
            ApiServiceDefinition definition = CreateServiceDefinitionAsset();
            Assert.IsFalse(
                ApiConnectionSettingsAssetFactory.TryCreateProjectSettings(
                    "Packages/com.example/Settings.asset",
                    definition,
                    out ApiConnectionSettings settings,
                    out string error));
            Assert.IsNull(settings);
            StringAssert.Contains("inside this project's Assets folder", error);
        }

        [Test]
        public void ProjectBinding_ZeroBindingsFailWithStableIssueCode()
        {
            bool resolved = ApiConnectionProjectSettings.instance.TryResolve(
                new ApiServiceId("example.api"),
                out ApiConnectionSettings settings,
                out string error);

            Assert.IsFalse(resolved);
            Assert.IsNull(settings);
            StringAssert.StartsWith("DEU-API-001", error);
        }

        [Test]
        public void ProjectBinding_OneExplicitGuidBindingResolves()
        {
            ApiServiceDefinition definition = CreateServiceDefinitionAsset();
            Assert.IsTrue(
                ApiConnectionSettingsAssetFactory.TryCreateProjectSettings(
                    SettingsPath,
                    definition,
                    out ApiConnectionSettings created,
                    out string error),
                error);
            Assert.IsTrue(
                ApiConnectionProjectSettings.instance.TryBind(
                    created,
                    out error),
                error);

            Assert.IsTrue(
                ApiConnectionProjectSettings.instance.TryResolve(
                    new ApiServiceId("example.api"),
                    out ApiConnectionSettings resolved,
                    out error),
                error);
            Assert.AreSame(created, resolved);
        }

        [Test]
        public void ProjectBinding_DuplicateRecordsFailAsAmbiguous()
        {
            ApiServiceDefinition definition = CreateServiceDefinitionAsset();
            Assert.IsTrue(
                ApiConnectionSettingsAssetFactory.TryCreateProjectSettings(
                    SettingsPath,
                    definition,
                    out ApiConnectionSettings created,
                    out string error),
                error);
            ApiConnectionProjectSettings project =
                ApiConnectionProjectSettings.instance;
            Assert.IsTrue(project.TryBind(created, out error), error);
            var serialized = new SerializedObject(project);
            SerializedProperty bindings =
                serialized.FindProperty("bindings");
            bindings.InsertArrayElementAtIndex(0);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(
                project.TryResolve(
                    new ApiServiceId("example.api"),
                    out _,
                    out error));
            StringAssert.StartsWith("DEU-API-002", error);
        }

        private static void AssertNoCreateAssetMenu<T>()
        {
            Assert.IsNull(
                Attribute.GetCustomAttribute(
                    typeof(T),
                    typeof(CreateAssetMenuAttribute)),
                typeof(T).Name);
        }

        private static void AssertCreateAssetMenu<T>(string expected)
        {
            var attribute = (CreateAssetMenuAttribute)Attribute.GetCustomAttribute(
                typeof(T),
                typeof(CreateAssetMenuAttribute));
            Assert.IsNotNull(attribute, typeof(T).Name);
            Assert.AreEqual(expected, attribute.menuName);
        }

        private static ApiServiceDefinition CreateServiceDefinitionAsset()
        {
            ApiEndpointCatalog catalog = CreateCatalogAsset();
            ApiServiceDefinition definition =
                ApiServiceDefinition.CreateTransient(
                    "example.api",
                    "Example API",
                    catalog,
                    ApiEnvironmentStages.Standard.Select(
                        stage => new ApiEnvironmentDescriptor(
                            new ApiEnvironmentId(
                                stage.ToString().ToLowerInvariant()),
                            stage,
                            stage.ToString())),
                    new[] { PrimaryClientId },
                    "1.0.0",
                    "sha256:test");
            AssetDatabase.CreateAsset(definition, DefinitionPath);
            return definition;
        }

        private static ApiEndpointCatalog CreateCatalogAsset()
        {
            ApiEndpointCatalog existing =
                AssetDatabase.LoadAssetAtPath<ApiEndpointCatalog>(CatalogPath);
            if (existing != null)
            {
                return existing;
            }

            ApiEndpointCatalog catalog =
                ScriptableObject.CreateInstance<ApiEndpointCatalog>();
            catalog.CatalogId = "example.v1";
            catalog.DisplayName = "Example API";
            catalog.Endpoints.Add(
                new ApiEndpointCatalogEntry
                {
                    EndpointId = "health.get",
                    ClientId = PrimaryClientId.Value,
                    RouteTemplate = "health",
                    Method = HttpMethod.GET,
                    Authentication = ApiAuthenticationRequirement.Required
                });
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            return catalog;
        }
    }
}
